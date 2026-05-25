# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

KeyPulse Signal is a .NET 8 WPF (Windows-only) desktop app that tracks USB keyboard/mouse connections and per-device input activity. It uses WMI watchers for device arrival/removal, Windows Raw Input (`WM_INPUT`) for activity capture, and SQLite + EF Core 9 for persistence. MVVM with `Microsoft.Extensions.DependencyInjection`; Serilog for logging; OxyPlot for charts.

**`AGENTS.md` is the authoritative architecture reference** — read it for data flow, lifecycle semantics, crash recovery, and per-subsystem detail. This file covers commands and the orientation needed to find your way around.

## Commands

```powershell
dotnet build -c Debug          # build (Debug = windowed; Release = tray/background)
dotnet run -c Debug            # run locally
dotnet csharpier .             # format (printWidth 120, see .csharpierrc.json)

# EF Core migrations (run from repo root)
dotnet ef migrations add <Name>
dotnet ef migrations remove
dotnet ef database update

.\scripts\Build-Release.ps1 [-Version "1.2.0"]   # publish + Inno Setup installer (needs iscc.exe on PATH)
```

- There is **no test project / no automated tests** — verification is manual by running the app.
- Migrations run automatically on startup via `DataService`; you usually only `add` a migration, not `database update` by hand.
- **Build fails if a KeyPulse instance is running** (locked `KeyPulse Signal.exe`) — stop it first.
- `--startup` launch arg forces tray/background mode.

## Architecture orientation

All services are **DI singletons** registered in `App.xaml.cs` (`OnStartup`). The key ones:

- **`AppTimerService`** — owns the shared UI-thread timers (1s / 30s / hourly) and broadcasts tick events, so transient view-models don't each spin up timers.
- **`UsbMonitorService`** — WMI device monitoring, insert-burst deduplication, connection lifecycle; owns the `ObservableCollection`s the UI binds to.
- **`RawInputService`** — hidden `WM_INPUT` window capturing per-device keystrokes/clicks/movement into in-memory minute buckets, flushed to the DB every minute.
- **`DataService`** — single source for all DB ops; runs migrations, enables WAL, performs crash recovery and snapshot rebuild on startup.
- **`DailyStatsService`** — write-through projector maintaining per-day per-device aggregates from `DeviceEvents` + `ActivitySnapshots`.

### Data model (four tables)

- **`DeviceEvents`** — immutable append-only lifecycle log; the source of truth for connection history.
- **`Devices`** — mutable fast-read snapshot for the UI (derived from the event log).
- **`ActivitySnapshots`** — immutable per-(device, minute) input buckets.
- **`DailyDeviceStats`** — mutable per-day aggregates derived from the two above.

DB lives at `%AppData%\KeyPulse Signal\keypulse-data.db` (Release) or `...\Test\keypulse-data.db` (Debug) — Debug and Release use **separate databases**.

## Conventions that bite

- **`IsConnected` ≠ `IsActive`**: `IsConnected` = device has an open session (`SessionStartedAt.HasValue`); `IsActive` = transient raw-input hold-state for highlighting. Don't conflate them.
- **Time helpers in `TimeFormatter`**: `NormalizeUtcMinute` (UTC, used by the daily-stats projector) and `TruncateToMinute` (preserves `DateTimeKind`, used by `RawInputService` for local-time buckets) are **not interchangeable**. Use `ToLocalDay()` rather than rolling your own date conversion.
- **Dispatcher safety**: services with background callbacks that touch UI-bound collections (`UsbMonitorService`, `RawInputService`) must guard with `ShutdownDispose.IsDispatcherUsable(dispatcher)` before `Invoke`/`BeginInvoke`. Pure services (`DataService`, `DailyStatsService`, helpers) don't need this.
- **Logging in helpers**: pure helpers (`RelayCommand`, `ObservableObject`, `TimeFormatter`) stay log-free; helpers touching FS/PowerShell/SetupAPI/crash-recovery should log failures.
- **`ObservableObject`** (in `Helpers/`) auto-marshals `PropertyChanged` to the UI thread.
- **Transient view-models** (e.g. `DashboardViewModel`, `CalendarViewModel`) subscribe to `AppTimerService` ticks and must unsubscribe on teardown since the view is destroyed/recreated on tab switches.