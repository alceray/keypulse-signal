# Changelog

All notable changes to this project are documented in this file.

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

