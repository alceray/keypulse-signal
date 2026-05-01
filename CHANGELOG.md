# Changelog

All notable changes to this project are documented in this file.

## [Unreleased]

### Added

- Auto-scroll-to-bottom in Troubleshooting log viewer when tab becomes visible or log content updates.
- Troubleshooting view with log file browser, color-coded severity levels, search/filter, and live tail.
- Color-coded and divider-styled log output for improved readability during troubleshooting.

### Changed

- Renamed `TotalUsage` to `ConnectionDuration` throughout codebase and UI for consistency and clarity.
- Icon management: Added `<ApplicationIcon>` to `.csproj`; icon displays on taskbar button and tray.
- Improved dashboard refresh reliability when switching tabs.

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

