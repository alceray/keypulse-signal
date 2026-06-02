# Changelog

All notable changes to this project are documented in this file.

## [1.2.0] - 2026-06-01

### Added

- Calendar view with per-day, per-device breakdowns of sessions, active minutes, and an hour-by-hour input activity graph that highlights your peak hour. Navigate days with the arrow keys.
- Dashboard device selection — click a device to highlight its activity across the charts.
- Real-time per-device input counters that tick up live as you type and persist on every flush.
- Pan and zoom on the activity chart — drag horizontally and scroll to explore any part of the selected time window.
- Hide individual devices from the dashboard and calendar while still tracking them in the background.
- Progress bars on time-based metrics for at-a-glance comparison.

### Changed

- Per-device activity now uses deterministic color palettes, and small pie-chart slices are grouped into an "Others" category.
- The device list shows a colored connection-status indicator (replacing the Connected column) and sorts connected devices first by default.
- The activity chart refreshes in place with a pinned time axis, preserving pan/zoom and scale across updates; time-axis labels were reformatted with per-range tick spacing.
- Reduced the installed size by ~86% (105 MB → 15 MB) by trimming unused packages and tightening release packaging.
- Replaced the manual bucket-size and smoothing-window settings with automatic, data-driven values.

### Fixed

- Fixed a dispatcher deadlock that could hang the app during Windows shutdown.
- Corrected the daily connection-stats calculation.
- Tooltips now appear on the current-day activity bars.

## [1.1.1] - 2026-04-30

### Added

- Troubleshooting view with log file browser, color-coded severity levels, search/filter, and live tail.
- Update-check settings with tray status integration for available updates.

### Changed

- Improved log messages for clearer troubleshooting and production diagnostics.
- App now opens the main window on first launch before switching to normal tray/background behavior.
- Centralized periodic timers into a shared timer service for more reliable refresh/update timing across views.
- Updated application icon so it displays consistently on both taskbar and tray.
- Renamed `TotalUsage` to `ConnectedTime` in the UI for clarity.

### Fixed

- Dashboard no longer stops periodic refresh after tab switches.
- Duplicate `Connected` events from bursty WMI insert callbacks are now deduplicated more reliably.

## [1.1.0] - 2026-04-29

### Added

- Single-instance enforcement per build mode (Debug and Release instances run independently).
- Launch on Login default setting.
- Safer database migration behavior with automatic pre-migration backup support.
- Improved startup/release reliability for installer-based deployments.

### Changed

- Renamed project and solution from "KeyPulse" to "KeyPulse Signal" for improved brand clarity.
- Separated Debug/test app data from Release app data to prevent cross-environment interference.

## [1.0.0] - 2026-04-28

### Added

- Initial stable release baseline.

