# KeyPulse Signal

**Know exactly which board or mouse you picked up today — and how hard you pushed it.**

KeyPulse Signal is a lightweight Windows desktop app built for keyboard and mouse enthusiasts who rotate gear and want real data behind their daily drivers. It silently tracks every connection, counts every keystroke and click, and gives you a per-device breakdown by day — so you always know which board carried the most load this week, or how long that new endgame actually spent on-desk.

## What It Tracks

- **Connection history** — when each device was plugged in, for how long, and how many sessions.
- **Input activity** — per-device keystrokes, mouse clicks, and movement time captured silently in the background.
- **Daily summaries** — a calendar view showing which devices were active on any given day, with session counts, active minutes, and peak usage hours.
- **Live device state** — a device list showing what's connected right now, with real-time input counters ticking up as you type.

## Why It's Useful for Collectors

- Rotate through boards freely — KeyPulse logs every connection automatically, no manual entries.
- Compare daily drivers objectively: "Did I actually use the new build more than the old one this week?"
- Spot usage patterns: see which layouts or switches you gravitate toward by day of week or time of day.
- Keep a persistent history even across reboots, crashes, or hot-swaps.

## Features

- Detects connected keyboards and mice at startup, then monitors plug/unplug events via WMI.
- Tracks per-device connection duration with crash-recovery-safe lifecycle reconstruction.
- Captures minute-level activity snapshots (`Keystrokes`, `MouseClicks`, `MouseActiveSeconds`).
- Dashboard with connection summaries, distribution charts, and activity timelines across any date range.
- Calendar view showing per-day and per-device input/connection breakdowns.
- Troubleshooting log viewer with severity coloring, search/filter, and auto-scroll.
- Runs in system tray — zero UI clutter while you work.
- Single-instance; activating a second launch restores the existing window.

## Requirements

### End Users

- Windows 10 (version 1607+) or Windows 11 (recommended).
- .NET 8 Desktop Runtime (for framework-dependent builds).

### Developers

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 or JetBrains Rider (with WPF support)

## Tech Stack

- .NET `net8.0-windows`
- WPF
- Entity Framework Core 9
- SQLite
- Windows WMI (`System.Management`)
- Windows Raw Input (`WM_INPUT`)
- `Microsoft.Extensions.DependencyInjection`
- Serilog

## Quick Start (Development)

```powershell
dotnet restore
dotnet build -c Debug
dotnet run -c Debug
```

Release-mode run:

```powershell
dotnet run -c Release
```

## Configuration

- Default startup mode:
  - `Debug`: foreground window
  - `Release`: tray/background mode
- Default `LaunchOnLogin`:
  - `Debug`: off (avoids polluting Windows startup registry during development)
  - `Release`: on
- Launch argument override:
  - `--startup` forces tray/background startup for that launch.

## Data Storage

- `Release`: `%AppData%\KeyPulse Signal\keypulse-data.db`
- `Debug`: `%AppData%\KeyPulse Signal\Test\keypulse-data.db`

Main persisted tables:

- `Devices` (mutable device snapshot)
- `DeviceEvents` (immutable lifecycle log)
- `ActivitySnapshots` (immutable minute buckets)
- `DailyDeviceStats` (per-day per-device aggregates)

## Architecture (High Level)

- `AppTimerService`: shared 1-second, 30-second, and hourly UI-thread timers.
- `UsbMonitorService`: WMI monitoring, event deduplication, connection lifecycle management.
- `RawInputService`: per-device raw input capture and minute-bucket activity aggregation.
- `DataService`: migrations, persistence, crash recovery, snapshot rebuild.
- `DailyStatsService`: computes and maintains per-day per-device stats from lifecycle events and activity snapshots.

## Troubleshooting

- Second launch activates the running instance (expected single-instance behavior).
- If build output is locked, stop the running app before rebuilding.
- If a device shows `Unknown Device`, rename it in-app or check Windows device metadata.

## Documentation

- `AGENTS.md` - architecture and implementation conventions.
- `docs/PRODUCTION_READINESS_PLAN.md` - production hardening checklist.
- `docs/RELEASE_PROCESS.md` - versioning and release workflow.
- `docs/RELEASE_CHECKLIST.md` - release validation steps.
- `CHANGELOG.md` - release-to-release changes.
