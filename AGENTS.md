# AGENTS.md: KeyPulse Architecture Guide

## Quick Overview

**KeyPulse** is a .NET 8 WPF desktop app for tracking USB keyboard and mouse connections on Windows and recording
per-device input activity. It uses WMI event watchers for device arrival/removal, Windows Raw Input for per-device
keyboard/mouse activity, and SQLite+EF Core for persistence. The app is single-instance and supports optional
tray/background mode.

**Key Tech Stack**: WPF, EF Core 9, SQLite, System.Management (WMI), Windows Raw Input (`WM_INPUT`), Dependency
Injection

---

## Architecture & Data Flow

### Major Components

1. **AppTimerService** (Singleton)
    - Owns all periodic UI-thread timers: 1 second, 30 seconds (dashboard refresh), and hourly (update check)
    - Broadcasts `SecondTick`, `ThirtySecondTick`, and `HourlyTick` events to subscriber services and view-models
    - Keeps transient view-models lean by centralizing timer ownership
    - Allows timers to persist across view lifecycle changes (e.g., tab switches)
    - See: `Services/AppTimerService.cs`

2. **UsbMonitorService** (Singleton)
    - Monitors USB device insertion/removal via WMI `__InstanceCreationEvent` and `__InstanceDeletionEvent`
    - Maintains `DeviceList` and `DeviceEventList` as `ObservableCollection<T>` instances for UI binding
    - Screens for HID keyboard/mouse devices only (`kbdhid`, `mouhid` services)
    - Aggregates bursty insert callbacks into one logical `Connected` event per device burst
    - Writes app/device lifecycle events and keeps the in-memory device snapshot in sync
    - Owns the heartbeat timer for crash recovery (separate from UI timers)
    - See: `Services/UsbMonitorService.cs`

3. **RawInputService** (Singleton)
    - Registers hidden `WM_INPUT` listeners using `RIDEV_INPUTSINK`, so activity is captured even in tray/background
      mode
    - Maps raw input handles back to KeyPulse `DeviceId` values
    - Tracks per-device keystrokes, mouse clicks, and mouse-movement-active seconds in one-minute buckets
    - Flushes completed minute buckets to `ActivitySnapshots` every minute and flushes the current partial minute on
      shutdown
    - Raises `ActivityStateChanged` so the UI can highlight currently active devices
    - See: `Services/RawInputService.cs`

3. **DataService** (Singleton)
    - Single source for all database operations
    - Runs migrations and enables SQLite WAL mode on startup
    - Crash recovery: detects unclean shutdowns and writes missing `AppEnded`/`ConnectionEnded` events
    - Rebuilds persisted device connection-duration snapshots from the event log on startup
    - Persists and queries minute-level `ActivitySnapshot` rows
    - See: `Services/DataService.cs`

4. **DailyStatsService** (Singleton)
    - Maintains `DailyDeviceStats` table from two write-through sources:
        - **DeviceEvents**: on every closing lifecycle event, recomputes that day's `SessionCount`, `ConnectionDuration`, `LongestSessionDuration` with a full non-cumulative replay of the day's events
        - **ActivitySnapshots**: minute-delayed projector flushes closed minute buckets to `Keystrokes`, `MouseClicks`, `MouseMovementSeconds`, `ActiveMinutes`, `DistinctActiveHours`, `PeakInputHour`
    - `RecomputeDailyDeviceStatsForRange(from, to)` provides an idempotent full rebuild for any date range
    - `RebuildGapOnStartup()` reads `LastCleanShutdownAt` from `AppMeta` and rebuilds the offline gap since last clean shutdown; bounded to the current month
    - `WriteLastCleanShutdownAt()` persists a clean-shutdown marker on normal exit
    - See: `Services/DailyStatsService.cs`

4. **ApplicationDbContext** (`DbContext`)
    - Four tables: `Devices`, `DeviceEvents`, `ActivitySnapshots`, and `DailyDeviceStats`
    - Database stored at `%AppData%\KeyPulse\keypulse-data.db` in Release and `%AppData%\KeyPulse\Test\keypulse-data.db` in Debug/testing builds
    - Unique constraint on `DeviceEvents(DeviceId, EventTime, EventType)` prevents duplicate lifecycle events
    - Unique constraint on `ActivitySnapshots(DeviceId, Minute)` prevents duplicate minute buckets
    - See: `Data/ApplicationDbContext.cs`

### Data Persistence Model

- **DeviceEvents** = immutable, append-only log of app/device lifecycle transitions (source of truth for connection
  history)
- **Devices** = mutable, fast-read snapshot used by the UI (`DeviceName`, `DeviceType`, `LastConnectedAt`, stored
  `ConnectionDuration`)
- **ActivitySnapshots** = immutable minute buckets storing `Keystrokes`, `MouseClicks`, and `MouseActiveSeconds`
- **DailyDeviceStats** = mutable per-day per-device aggregates (`SessionCount`, `ConnectionDuration`, `Keystrokes`, activity stats etc.) derived from DeviceEvents and ActivitySnapshots; rebuilt on startup for the offline gap
- Updates flow in two directions:
    - lifecycle changes append to `DeviceEvents` and update the corresponding `Device` snapshot
    - raw input activity accumulates in memory, then flushes to `ActivitySnapshots`

### Startup Sequence (See `App.OnStartup`, `UsbMonitorService`, and `RawInputService`)

1. Register unhandled-exception cleanup hooks.
2. Mutex check (single-instance enforcement). If another instance exists, signal it to restore/focus and exit.
3. Resolve startup mode from build configuration (Debug foreground, Release tray), with launch args able to force tray
   mode.
4. Build the DI container.
5. Resolve `UsbMonitorService` (which also resolves `DataService`). During construction:
    - database migrations run,
    - SQLite WAL mode is enabled,
    - `DataService.RecoverFromCrash()` backfills missing close events if needed,
    - `DataService.RebuildDeviceSnapshots()` recomputes persisted `ConnectionDuration` and clears stale `SessionStartedAt`,
    - historical `Devices` / `DeviceEvents` are loaded,
    - the heartbeat timer starts.
6. Show the main window immediately or initialize the tray icon, depending on background mode.
7. Await `UsbMonitorService.StartAsync()`:
    - `SetCurrentDevicesFromSystem()` writes `AppStarted`, snapshots currently connected HID devices, and emits
      `ConnectionStarted` for each one,
    - then WMI watchers are started for live insert/remove events.
8. Resolve `RawInputService` and call `Start()` to create the hidden message-only window and begin receiving `WM_INPUT`.

### Shutdown / Disposal Ownership

- During normal application exit, DI owns disposal of singleton services via `ServiceProvider.Dispose()`.
- Singleton `Dispose()` methods are idempotent and may still be called from crash/unhandled-exception cleanup paths.
- Duplicate dispose attempts should return immediately and log a `Debug` message rather than performing cleanup twice.

---

## Critical Patterns & Conventions

### Event Types & Lifecycle

Event types are defined in `Models/DeviceEvent.cs` and categorized by `EventTypeExtensions`:

- **Opening**: `ConnectionStarted`, `Connected`
- **Closing**: `ConnectionEnded`, `Disconnected`
- **App-level**: `AppStarted`, `AppEnded`

Device state management is centralized in `UsbMonitorService.AddDeviceEvent()`:

- opening events set `SessionStartedAt`
- closing events commit elapsed time into stored connection duration and clear `SessionStartedAt`
- app-level events are logged without touching per-device state

### Device Identification

- **Format**: `USB\VID_xxxx&PID_xxxx` (parsed from WMI `DeviceID`)
- **Classification**: `UsbDeviceClassifier.GetInterfaceSignal()` probes WMI for `Service`, `ClassGuid`, `PNPClass`
- **Type Resolution**: Multiple HID interfaces expected per device; wait for ≥2 signals before determining type (
  keyboard vs. mouse)

### Property Binding & Threading

- **ObservableObject** base class (in `Helpers/`) wraps `INotifyPropertyChanged`
- Automatically marshals property changes to UI thread via `Application.Current.Dispatcher`
- See `Models/Device.cs` for example: `SessionStartedAt` triggers dependent notifications for `IsConnected` and
  `ConnectionDuration`
- **Important**: `IsConnected` and `IsActive` are different concepts:
    - `IsConnected` means the device currently has an open connection session (`SessionStartedAt.HasValue`)
    - `IsActive` is a transient raw-input hold-state flag used to highlight devices while keys/buttons are currently
      held
- **Important**: `ConnectionDuration` is computed while connected; it displays the stored value plus elapsed time since
  `SessionStartedAt`

### Raw Input Activity Semantics

- `RawInputService` creates one in-memory bucket per `(DeviceId, Minute)`.
- **Keyboard**: every key-down increments `Keystrokes`; key-up only updates hold state.
- **Mouse buttons**: every button-down increments `MouseClicks`; button-up only updates hold state.
- **Mouse movement**: movement is recorded as a set of second-of-minute values, then persisted as `MouseActiveSeconds` (
  0–60).
- Completed minutes flush every 60 seconds; dispose/shutdown flushes the current partial minute as well.

### Dashboard Behavior (Connection Duration + Activity)

- **Dashboard refresh**: `DashboardViewModel` subscribes to `AppTimerService.ThirtySecondTick` for periodic refresh
  (Dashboard is transient, so subscriptions are cleaned up when the view is destroyed and re-created when the tab is switched back)
- Dashboard top cards show current connected count and top-3 keyboard/mouse device connection-duration summaries.
- Time-range filter supports `1 Day`, `1 Week`, `1 Month`, `1 Year`, and `All Time`.
- Activity chart supports configurable `bucketMinutes` and trailing moving-average `smoothingWindow` (both normalized
  to >= 1).
- Activity chart zeroes buckets where no app-running interval overlaps (`AppStarted`/`AppEnded` reconstruction).
- Activity chart series use:
    - keyboard = `Keystrokes`
    - mouse = `MouseClicks + MouseMovementSeconds`
- X-axis label format adapts by selected range:
    - `< 7 days`: `MM-dd HH:mm`
    - `>= 7 days and < 365 days`: `MM-dd`
    - `>= 365 days`: `yyyy-MM`
- Pie charts use hover tracking metadata (status, connection duration, share, connection time text) and update dashboard hover-preview
  state via tracker events.

### Calendar View Behavior

- **Data source**: `DailyStatsService.GetCalendarDaySummaries()` and `GetCalendarDayDetail()` — both backed by `DailyDeviceStats`.
- **Real-time overlay**: `CalendarViewModel` maintains a live in-memory input delta (`_todayLiveInputDeltaByDevice`) and connection overlay per device; `ApplyRealtimeTodayOverlay()` merges persisted + live state on every second tick and every `InputCountIncremented` event.
- **Persisted baseline refresh**: `OnThirtySecondTick` re-queries today's persisted summary from DB and refreshes the tile and detail panel baseline to catch minute-projector commits.
- **`CalendarDaySummary.IsToday`**: computed property (`Day == DateOnly.FromDateTime(DateTime.Now)`), never stale.
- **`CalendarDaySummary.IsSelected`**: UI-only flag toggled in-place via tile selection; never stored in DB.
- **Calendar DTOs** (`CalendarDaySummary`, `CalendarTileDevice`, `CalendarDeviceDetail`) use `DeviceTypes` enum, not raw strings.
- Calendar detail panel uses responsive `DockPanel` rows so labels/values stay readable at any panel width.

### Duplicate Detection

- WMI fires multiple insert events (~2-3) per physical USB connection
- `_cachedDevices` stores aggregated keyboard/mouse interface signals inside a short time window
- `_pendingCachedDeviceProcessing` ensures only one delayed aggregation callback runs per device burst, preventing a multi-callback race
- Only then is a single `Connected` event recorded
- `DeviceEvents` unique constraint prevents DB duplicates even if code fails

---

## Workflows & Commands

### Database Migrations (via EF Core CLI in Developer PowerShell)

```powershell
# Add new migration from current model state
dotnet ef migrations add MyMigrationName

# Remove last unapplied migration
dotnet ef migrations remove

# Apply pending migrations to DB
dotnet ef database update

# Revert to specific migration target
dotnet ef database update SomeOlderMigrationName
```

### Configuration

- **AppConstants.App** structure consolidates application-level settings:
  - `ProductName` = "KeyPulse Signal" (from assembly)
  - `DefaultName` = "KeyPulse Signal" or "KeyPulse Signal (Test)" depending on Debug/Release build
  - `StartupArgument` = `--startup`
  - `ActivationEventSuffix` = `.ACTIVATE`
  - `RunKeyPath` = Windows registry path for startup entries
  - `TrayIconRelativePath` = relative path to the taskbar icon (`Assets\keypulse-signal-icon.ico`)
  - `StartupWarningBalloonTimeoutMs` = 5000 (milliseconds for startup warning balloon)
- **Build mode default**: Debug launches windowed; Release launches to tray/background
- **`LaunchOnLogin` default**: `false` in Debug, `true` in Release (controlled via `#if DEBUG` in `AppUserSettings`)
- **Launch args**: `--startup` forces tray/background startup for that process
- **Application icon**: Set via `<ApplicationIcon>` in `.csproj` for both window and taskbar display; also used for tray icon
- Tray icon created if background mode enabled; main window created on-demand or at startup

### Build & Run

- Target: `.net8.0-windows`, WPF enabled
- Debug builds auto-generate WPF XAML behind the scenes
- **Build may fail** if KeyPulse.exe is locked — stop running instance first
- **Assets**: `Assets/keyboard_mouse_icon.ico` copied to build output

---

## File Organization & Responsibilities

| Folder        | Purpose                                                                                                          |
|---------------|------------------------------------------------------------------------------------------------------------------|
| `Helpers/`    | `ObservableObject`, `RelayCommand`, `UsbDeviceClassifier`, `TimeFormatter`, `PowerShellScripts`, `HeartbeatFile` |
| `Services/`   | `UsbMonitorService`, `RawInputService`, `DataService`, `DailyStatsService` — monitoring, activity capture, persistence, daily stats |
| `Models/`     | `Device`, `DeviceEvent`, `ActivitySnapshot`, `DailyDeviceStat` + enums/extensions                               |
| `Data/`       | `ApplicationDbContext`, database initialization                                                                  |
| `ViewModels/` | MVVM viewmodels for each UI view; `StatusMessageViewModelBase` provides shared status toast behavior             |
| `Views/`      | XAML + code-behind for UI; `StatusMessagePanel` is a reusable status toast control                              |
| `Migrations/` | EF Core snapshot migrations (read-only; auto-generated)                                                          |
| `docs/`       | release docs, production-readiness plan, and other project documentation                                         |

---

## Crash Recovery & Consistency

**Problem**: If app is force-killed (IDE stop, crash), devices may appear "stuck" as active in the DB.

**Solution** (`DataService.RecoverFromCrash`):

1. On startup, check if last app-lifecycle event was `AppStarted` (unmatched)
2. If so, retroactively add `AppEnded` and `ConnectionEnded` for orphaned devices
3. Event log stays consistent for future connection-duration calculations

**Snapshot Rebuild** (`DataService.RebuildDeviceSnapshots`):

- Recomputes persisted `ConnectionDuration` from the event log
- Clears runtime-only `SessionStartedAt` so devices do not appear connected after an unclean shutdown
- Called at startup after recovery

---

## Important Implementation Details

### Duplicate Event Prevention

- `DeviceInsertedEvent` accumulates keyboard/mouse signals until ≥2 are seen within a short timeframe
- Only emits one `Connected` event per physical device insertion
- DB unique constraint `(DeviceId, EventTime, EventType)` is secondary safeguard

### Current Session Connection Duration ("Session" vs. "Total")

- **SessionStartedAt**: Set when device becomes active (opening event), cleared when inactive
- **IsConnected**: Computed from `SessionStartedAt.HasValue`
- **IsActive**: Separate transient hold-state driven by `RawInputService.ActivityStateChanged`
- **ConnectionDuration**: Displays stored value + elapsed since SessionStartedAt (live tick while active)
- Avoids stale timing from unclean shutdown; current session always starts fresh

### ActivitySnapshots

- One row represents one device during one minute.
- `Keystrokes` counts key-down events.
- `MouseClicks` counts mouse button-down events.
- `MouseActiveSeconds` stores how many distinct seconds within the minute saw mouse movement.
- The current code persists snapshots, but the main UI primarily exposes live connection/activity state and total connection duration
  rather than a dedicated activity-history view.

### Device Name Resolution

- `DeviceNameLookup.GetDeviceName(deviceId)` first attempts native SetupAPI/cfgmgr32 lookup
- Falls back to `PowershellScripts.GetDeviceName(deviceId)` only if native lookup fails
- Falls back to `"Unknown Device"` if lookup fails
- Native lookup and PowerShell fallback paths log at `Debug`; lookup failures that throw log at `Warning`
- User can rename devices; changes saved immediately to DB

### Helper Logging Conventions

- Pure helpers such as `RelayCommand`, `AsyncRelayCommand`, `ObservableObject`, and `TimeFormatter` should stay log-free.
- Helpers that touch the file system, PowerShell, SetupAPI/cfgmgr32, or crash-recovery inputs should log meaningful failures.
- `HeartbeatFile.Read()` should log `Debug` when no heartbeat file exists and `Warning` if the file exists but contains an invalid timestamp.
- `UsbDeviceClassifier.ResolveDeviceType()` logs `Warning` when the observed signal pattern does not match a known keyboard/mouse shape.

### TimeFormatter Helpers

- `ToLocalTime(DateTime)` — converts any `DateTime` to local time, treating `Unspecified` as UTC (matches SQLite reads).
- `ToLocalDay(DateTime)` — converts a UTC/unspecified timestamp to the local `DateOnly` day. Use this everywhere instead of `DateOnly.FromDateTime(ToLocalTime(...))`.
- `LocalDayToUtc(DateOnly)` — converts a local day to its UTC start boundary for inclusive range queries.
- `NormalizeUtcMinute(DateTime)` — converts to UTC then truncates to minute boundary; used by `DailyStatsService` projector.
- `TruncateToMinute(DateTime)` — truncates to minute while preserving `DateTimeKind`; used by `RawInputService` for local-time minute buckets. **Not interchangeable with `NormalizeUtcMinute`.**

### Status Message Pattern

- `StatusMessageViewModelBase` (`ViewModels/`) provides `StatusMessage`, `StatusVisibility`, and auto-clear timer behavior.
- ViewModels that need transient toast messages should inherit `StatusMessageViewModelBase` instead of duplicating these fields.
- `StatusMessagePanel` (`Views/`) is the reusable XAML control with `StatusMessage` and `StatusVisibility` dependency properties.
- Positioning (alignment, margin, grid row) is controlled by the host view — the panel itself is layout-agnostic.

### Dispatcher Safety in Services

Services that receive background callbacks (WMI events, `RawInputService` message pump) and update UI-bound collections must guard Dispatcher calls.
Use `ShutdownDispose.IsDispatcherUsable(dispatcher)` before any `Invoke`/`BeginInvoke`. Two shutdown scenarios require this:
1. **Normal shutdown**: `_disposed = true` is set at Dispose start; subsequent callbacks skip UI updates.
2. **Windows session end**: disposal is skipped (`_disposed` stays false) but the Dispatcher may be dead; `HasShutdownStarted`/`HasShutdownFinished` checks prevent deadlocks.

Pure services (`DataService`, `DailyStatsService`, helpers) do **not** need dispatcher guards — they have no UI bindings.

### Troubleshooting View Auto-scroll Behavior

- Log viewer automatically scrolls to the bottom when:
  - The tab becomes visible (`IsVisibleChanged` event fires true)
  - New log content is loaded (`LogContent` property changes)
  - Logs are refreshed via `RefreshLogs()` command
- Scrolling uses `DispatcherPriority.Background` to allow layout to settle before scroll
- Search and entry parsing preserve scroll position only when actively searching; log load and visibility changes always trigger scroll-to-bottom

### Documentation Entry Points

- `README.md` = project overview, quick-start, and release doc links
- `docs/RELEASE_PROCESS.md` = versioning and packaging workflow
- `docs/PRODUCTION_READINESS_PLAN.md` = tracked production readiness plan

---

## Common Issues & Troubleshooting

| Issue                                                       | Cause                                                | Fix                                                   |
|-------------------------------------------------------------|------------------------------------------------------|-------------------------------------------------------|
| Second launch opens existing window instead of new instance | Mutex check + activation signal                      | Expected behavior                                     |
| Build fails, file locked                                    | KeyPulse.exe running                                 | Stop KeyPulse, then rebuild                           |
| Device shows as "Unknown Device"                            | Windows didn't provide friendly name                 | Rename manually in UI or check Windows Device Manager |
| Duplicate events in debug output                            | Expected behavior; deduped by cache or DB constraint | No action needed                                      |

---

## Key Files to Read First

- `App.xaml.cs` → DI setup, startup/shutdown lifecycle
- `Services/UsbMonitorService.cs` → WMI monitoring, event handling
- `Services/RawInputService.cs` → per-device raw input capture and minute-bucket flushing
- `Services/DataService.cs` → crash recovery, snapshot rebuild, event persistence
- `Services/DailyStatsService.cs` → daily stats write-through, activity projector, startup rebuild
- `ViewModels/CalendarViewModel.cs` → calendar real-time overlay, persisted baseline refresh
- `ViewModels/StatusMessageViewModelBase.cs` → shared status toast VM base
- `Views/StatusMessagePanel.xaml` → reusable status toast control
- `Models/Device.cs`, `Models/DeviceEvent.cs`, `Models/ActivitySnapshot.cs`, `Models/DailyDeviceStat.cs` → persisted models and runtime state

