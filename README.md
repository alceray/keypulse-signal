# KeyPulse Signal

KeyPulse Signal is a Windows desktop app for monitoring USB keyboard and mouse connection state and per-device activity over time.
It captures lifecycle events and minute-level input snapshots, then persists data locally in SQLite for fast dashboard and troubleshooting views.

## Features

- Detects connected keyboards and mice at startup, then monitors insert/remove events via WMI.
- Tracks per-device connection duration with crash-recovery-safe lifecycle reconstruction.
- Captures minute-level activity snapshots (`Keystrokes`, `MouseClicks`, `MouseActiveSeconds`).
- Provides a dashboard with connection summaries, distribution charts, and activity timelines.
- Includes a troubleshooting log viewer with severity coloring, search/filter, and auto-scroll-to-bottom.
- Supports tray/background startup mode and single-instance activation behavior.

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
- Launch argument override:
  - `--startup` forces tray/background startup for that launch.

## Data Storage

- `Release`: `%AppData%\KeyPulse Signal\keypulse-data.db`
- `Debug`: `%AppData%\KeyPulse Signal\Test\keypulse-data.db`

Main persisted tables:

- `Devices` (mutable device snapshot)
- `DeviceEvents` (immutable lifecycle log)
- `ActivitySnapshots` (immutable minute buckets)

## Architecture (High Level)

- `AppTimerService`: shared 1-second, 30-second, and hourly UI-thread timers.
- `UsbMonitorService`: WMI monitoring, event deduplication, connection lifecycle management.
- `RawInputService`: per-device raw input capture and minute-bucket activity aggregation.
- `DataService`: migrations, persistence, crash recovery, snapshot rebuild.

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
