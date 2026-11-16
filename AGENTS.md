# KeyPulse Architecture Guide

KeyPulse is a single-instance .NET 8 WPF app for tracking Windows USB keyboard/mouse connections and per-device input activity. It uses WMI, Windows Raw Input, EF Core 9, SQLite by default, and optional PostgreSQL. Capture continues in tray mode.

## Working conventions

- Use `caveman` for concise user-facing responses unless normal mode is requested. Keep code, comments, and documentation in plain prose.
- Use `ui-ux-pro-max` for UI work, `grilling` when reviewing proposed plans, and `thermo-nuclear-code-quality-review` after substantial code changes. Announce the skills used.
- Prefer simple fixes and existing patterns. Do not add abstractions solely to satisfy a review recommendation.
- Use PascalCase for project-owned folders (`Docs/`, `Scripts/`, `Installer/`, `Tasks/`). Preserve established tool/generated paths such as `.github/`, `bin/`, `obj/`, `artifacts/`, `publish/`, and `Installer/output/`.
- Catalog import code and metadata belong in `Scripts/Catalogs/`; accepted catalogs belong in `Assets/Catalogs/`.

### Plans and bug records

- Create TODO files only when explicitly requested, for work that needs written steps. Include **Created** and **Updated** dates (`YYYY-MM-DD`), preserving Created and refreshing Updated when changed.
- TODOs must estimate affected files and lines added/deleted as ranges, separating implementation, tests, and documentation. State assumptions, update estimates when scope changes, and distinguish estimates from measured diffs. Documentation-only work has zero code churn.
- When a correct test exposes a production bug, fix the code without weakening the intended assertion. After verification passes, append to the gitignored `Tasks/fixedBugs.md` with the date only (`YYYY-MM-DD`), test/file, expected versus actual behavior, root cause, production files changed, and verification command/results.
- Record the observed failure before the fix and passing result afterward. A new regression test qualifies only if it failed against unfixed code. Exclude stale/incorrect expectations, test-harness corrections, environment/build failures, and tests run only after a fix. Never invent retrospective entries.
- Mention qualifying test-caught bugs in the final report and link the entry, rather than reporting only passing counts. Keep `Tasks/` gitignored.

## Code map

| Entry point | Responsibility |
|---|---|
| `App.xaml.cs` | DI, single-instance activation, startup and shutdown |
| `Services/AppTimerService.cs` | Shared UI-thread second, 30-second, minute, and daily ticks |
| `Services/UsbMonitorService.cs` | WMI detection, device/event collections, lifecycle writes, recovery heartbeat |
| `Services/RawInputService.cs` | Background `WM_INPUT` capture, activity state, minute buckets |
| `Services/DataService.cs` | Device/event persistence, crash recovery, snapshot rebuild |
| `Services/DailyStatsService.cs` | Connection write-through, activity projection, historical rebuild |
| `Services/DataRetentionService.cs` | Minute-data pruning and SQLite compaction |
| `Services/Database/` | Provider configuration, recovery, transfers, credentials, verification |
| `Services/DataService.Collection.cs` | Collection transactions, revision checks, committed cache |
| `ViewModels/Collection/`, `Views/Collection/` | Catalog/owned rows, drafts, variants, grouped grids |
| `ViewModels/Dashboard/`, `ViewModels/Calendar/` | Chart/summary builders and presentation DTOs |
| `Data/ApplicationDbContext.cs` | SQLite model; PostgreSQL uses `PostgreSqlApplicationDbContext` |
| `Configuration/`, `Views/Styles/` | App constants, palettes, shared WPF styles |

Core services are DI-owned singletons; Dashboard is transient. Keep periodic UI timers in `AppTimerService`, not transient views. The USB heartbeat has separate ownership.

## Persistence and lifecycle

| Store | Meaning |
|---|---|
| `Devices` | Mutable device metadata and fast-read totals |
| `DeviceEvents` | Append-only source of connection history; unique `(DeviceId, EventTime, EventType)` |
| `ActivitySnapshots` | Minute input buckets; unique `(DeviceId, Minute)` |
| `DailyDeviceStats` | Derived local-day totals; unique `(Day, DeviceId)` |
| `ActivityProjections` | Per-device/minute checkpoints preventing duplicate activity projection |
| `CollectionItems` | User-owned parts and descriptive snapshots, independent of activity retention |
| `AppMeta` | Lazily created raw key/value table outside EF |

### Startup and shutdown

1. Register exception cleanup, enforce the single-instance mutex, and signal an existing instance to restore if needed.
2. Complete database selection, pending transfers, and connection recovery before DI/monitoring. Use explicit shutdown during preflight so closing a dialog cannot terminate an outstanding await. Cancellation exits explicitly.
3. Resolve startup mode and DI. Initialize `DataService`: provider migrations, SQLite-only WAL, crash recovery, device snapshot rebuild, history loading, then the USB heartbeat.
4. Run `RunStartupRebuild()` followed by `RunStartupPrune()` on one background task. Show the main window or tray according to startup mode.
5. Await USB startup: write `AppStarted`, detect connected HID devices and write `ConnectionStarted`, then start WMI watchers. Start Raw Input afterward.

- Normal exit drains accepted collection writes through `StopAndDrainAsync` before disposing services and releasing the mutex. Windows session end retains its nonblocking exit path.
- DI owns singleton disposal. `Dispose()` must be idempotent because exception cleanup can invoke it again; duplicate calls return with a Debug log.
- Background callbacks updating UI-bound state must check `ShutdownDispose.IsDispatcherUsable` before dispatcher calls. `_disposed` alone is insufficient during Windows session end. Pure persistence services need no dispatcher guard.
- `ObservableObject` marshals property-change notifications to the UI thread; dispose transient view-model subscriptions when their views are destroyed.

### Device and input semantics

- Device IDs use `USB\VID_xxxx&PID_xxxx`; identical devices can share this identity. Classify `kbdhid`/`mouhid` interfaces through `UsbDeviceClassifier`. Aggregate interface signals (at least two) and schedule only one delayed callback per insertion burst; the database uniqueness constraint is a second safeguard.
- `UsbMonitorService.AddDeviceEvent()` owns state transitions. `ConnectionStarted`/`Connected` set `SessionStartedAt`; `ConnectionEnded`/`Disconnected` commit elapsed time and clear it. `AppStarted`/`AppEnded` do not change per-device state.
- `IsConnected` means an open session. `IsActive` is transient Raw Input hold-state. `TotalConnectionSeconds` adds elapsed session time to the stored total while connected.
- Every key-down increments keystrokes; every mouse button-down increments clicks. Releases only update hold-state. Mouse movement counts distinct active seconds within each minute (0-60); `ActiveSeconds` counts distinct seconds with any input.
- Capture uses `RIDEV_INPUTSINK`, including tray mode. Flush closed minutes every minute and the current partial minute on shutdown. `ActivityStateChanged` drives highlighting; `InputDeltaIncremented` supplies immediate calendar deltas.
- Manual type corrections persist before changing the shared device. Twenty consecutive packets contradicting a saved Keyboard/Mouse type produce one session-only suggestion; matching packets reset the count. Never automatically change the type or suggest for Unknown/Other.
- Resolve names through native SetupAPI/cfgmgr32, then PowerShell, then "Unknown Device". Log lookup paths at Debug and thrown failures at Warning; user renames persist immediately.

### Recovery, daily stats, and retention

- Crash recovery closes orphan app/device sessions before rebuilding device snapshots. Rebuild connection totals from unpruned events, input totals from daily stats plus unprojected snapshots, and days connected from daily rows with connection time. Clear stale runtime session starts.
- Closing lifecycle events recompute that day's connection seconds and session count through a full, non-cumulative replay. Closed minute snapshots project once into activity totals and `HourlyInputCount` (24 local-hour combined-input buckets).
- All daily-stat mutations share one in-process gate: range recompute, activity projection, and connection write-through. `RecomputeDailyDeviceStatsForRange` must remain idempotent.
- Without `DailyStatsFullBackfillAt`, startup rebuilds all history month by month, then writes the marker. Later startups reconcile only missing current-month device/day rows. Do not add recurring blind rebuilds. Drain all unprojected closed minutes at startup, regardless of age.
- Retention defaults to 24 months; 0 means forever. Prune only `ActivitySnapshots` and `ActivityProjections`, never events, daily stats, or collections.
- Never prune before the full-backfill marker exists. Drain `ProjectClosedActivityMinutes()` first; delete snapshots **before** their checkpoints so surviving snapshots cannot be projected twice. Use week-sized transactions and VACUUM only after large SQLite prunes.
- Pruning runs after startup rebuild, on the daily tick, and when retention is tightened. Long-range activity charts use retained hourly/daily aggregates; short-range charts use recent minute snapshots.

### Database switching

See `DatabaseSwitchService`, `DatabaseConnectionSettingsService`, `SqliteHistoryFile`, `ConfiguredDbContextFactory`, and `AppRestartService`.

- Debug and Release isolate settings, credentials, and databases. SQLite paths are `%AppData%\KeyPulse Signal\Test\keypulse-data.db` and `%AppData%\KeyPulse Signal\keypulse-data.db`, respectively. Use separate PostgreSQL databases and build-qualified Windows Credential Manager entries.
- **Save and restart** preserves executable arguments and exits normally. The replacement waits for the old process before opening logs, claiming the mutex, or transferring history, then opens the main window even in tray mode.
- Database work runs off the UI thread; recovery dialogs remain on it. The transfer window reports preparation, copying, verification, and activation and supports Stop and exit. Capture starts only after successful preflight.
- PostgreSQL has its own migrations in `Migrations/PostgreSql`. Validate source schemas read-only; migrate the active source before retrying a pending transfer. PostgreSQL replacement requires explicit confirmation.
- Direct PostgreSQL transfers acquire separate nonblocking source and destination advisory locks before target migrations or history reads. Check destination completion markers before source access so a committed copy can activate without its source. Authentication recovery must not change confirmed endpoints.
- Keep the active `PostgreSql` connection/credential until activation; `PendingPostgreSql` describes the destination. Publish settings and credential references atomically. New credential entries are unique; cleanup checks durable references and never deletes the legacy default entry.
- Copy and verify counts/content fingerprints in one target transaction, preserving retention metadata. `DatabaseImportSwitchId` makes activation retryable without recopying, including empty histories. Keep the destination lock through activation and hand it to DI when PostgreSQL stays active.
- SQLite exports use staging, a verified `.pre-import` backup in `DbBackups`, and checkpointed replacement. After successful SQLite activation/initialization, retain the three newest automatic backups per database across imports and migrations. Timestamp migration markers prevent repeated UTC conversion.
- Completion logs include endpoints, current-attempt duration, and resumed activation status, but no row counts.

## Collections

### Storage and services

- `CollectionItems` use stable unique `ItemKey` and application-managed `Revision`. `PartKind` distinguishes Switch/Piece and Keycap/Kit; details are immutable `SwitchPartDetails`/`KeycapPartDetails` JSON records at schema version 1.
- Custom rows have SQL NULL catalog references and empty `Variant`. Catalog rows are unique by `(PartKind, CatalogEntryId, Variant)`; an empty variant means unspecified. Snapshot refresh never changes the selected variant.
- `CatalogService` and `CollectionService` load lazily, never during startup preflight. Owned snapshots load independently; catalogs load asynchronously and refresh matching IDs in one transaction per kind. Missing IDs retain snapshots; custom rows remain custom.
- Catalog loading and snapshot-refresh failures are distinct and retryable. Preserve unreadable raw details: allow removal, but block edits, additions, and refresh for that row. A collection-read failure blocks mutations; a catalog failure still allows saved rows and custom creation.
- Serialize writes through one gate, validate captured revisions, and return typed results. Update the committed immutable cache under that gate; a separate short cache lock keeps readers independent of database work. Dispatch notifications afterward; consumers read the latest cache. Stale edits/removals conflict without retries; no-op writes preserve revisions.
- Prepare `CatalogSearch.CreateFilter` once per query change. Reuse catalog search text and rebuild owned search text only when searchable fields, including notes, change.
- Transfers include every collection column except its generated integer key. Fingerprint in `ItemKey` order, including exact raw details, schema version, quantity unit, variant, revision, and UTC date ticks. A collection-only database counts as history.

### Grid and editing behavior

- `CollectionView` retains Switches/Keycaps `PartListViewModel`s. Each uses a shared-column grouped grid with My collection before Catalog, independently collapsible. Owned variants are separate visible rows, not collapsed set summaries; custom and missing-catalog items stay owned.
- Pin column headers and the collection header. Scrolling past owned rows collapses the collection while preserving catalog position; reopening it scrolls back to the collection. Preserve pixel scrolling and virtualization while grouping.
- Create editor/action templates only when applicable; merely collapsing controls still creates them. Skip unchanged row notifications and redundant view resets. Validation/saving feedback uses the shared toast, not expanding rows.
- Search/filters expand groups with visible matches, including after refresh and tab return. Clearing criteria or groups without matches does not alter expansion. Switching between open filters takes one click. Sort within fixed ownership groups using numeric/text/date values.
- Name and Notes share spare width; use explicit pixel widths and column minimums to preserve narrow-window horizontal scrolling. Only Name is frozen. Keep Variants, Qty and its controls, Notes, then Added together; variant fields use the same centered cell layout and outer spacing as other inputs.
- Quantity changes are drafts. Enter/Save commits; Escape/Cancel resets quantity alone. Quantity action buttons appear only for quantity drafts, never notes or descriptive edits. Add/increment/decrement uses 10 switches or 1 keycap kit; decrement stops at zero.
- Zero is a removal draft, never a persisted quantity. Delete sets that draft, Cancel restores it, and Save removes without confirmation. Catalog items return to Catalog; custom rows disappear. App limits are 10,000 switches or 100 keycap kits; the database constraint remains 1-1,000,000.
- Drafts retain their captured snapshot/revision through unrelated refreshes and storage failures. Conflicts use toasts and explicit Reload current values; do not silently retry.
- Notes save on Enter or focus loss, including clicks on non-focusable surfaces. Notes-only writes preserve quantity/descriptive drafts and advance the captured revision only after success. Custom-item Save/Cancel suppress the blur save; new custom notes wait for creation.
- Custom creation supports one unsaved row per category, with Save/Cancel beside Name; owned custom descriptions are inline inputs and catalog descriptions are read-only. The Add custom item button is currently hidden. Do not introduce separate collection editor/picker windows.
- Catalog variants stay a read-only list until Add. The owned dropdown stages selections: Apply atomically adds/removes variants with revision checks; Cancel, Escape, or outside dismissal discards them. Failed Apply retains selections for retry/reload.
- New variants copy the source row's **saved** quantity, never its draft; first additions default to 10 switches or 1 kit. Retained variants keep quantity/notes. Replacing an unspecified row with named variants is atomic.
- Tab switches retain search, filters, sorting, selection, and horizontal scroll, but reset to the top with My collection expanded and Catalog collapsed. Discard unsaved drafts/custom rows; accepted writes finish normally. Hide/restore retains state. Dispose host subscriptions when the main window closes.

## Dashboard, calendar, and visibility

- `IsHiddenFromDisplay` is presentation-only: never stop capture, events, or projection. Persist changes before updating the shared device. Hidden rows remain accessible in Devices; exclude them from dashboard queries/cards/charts and calendar queries/overlays/details. Visibility changes refresh both views; days without visible data have `HasData = false`.
- Dashboard subscribes to the 30-second tick and disposes subscriptions with the transient view. `DashboardActivityChartBuilder` selects bucket size from the range, capped at 400 points; zoom never recomputes finer data. Use unsmoothed, baseline-anchored polylines broken across inactive runs, and zero buckets outside reconstructed app-running intervals.
- Keyboard activity is keystrokes; mouse activity is clicks plus movement-active seconds. Hour-aligned chart tiers read `DailyDeviceStats` via `DashboardHourlyActivityAdapter`; finer tiers read raw minute snapshots.
- Pie charts show connection-time share with deterministic rank/color ordering. Group devices beyond the top 12 or below 1% into Others, retaining their names for hover details. Use the separate `DashboardDeviceColorPalette`.
- Calendar consumes plain daily rows through `GetVisibleDailyDeviceStats`; `CalendarSummaryBuilder` maps them with device metadata. Keep presentation DTOs out of services; use `DeviceTypes` and seconds for connection durations.
- Calendar overlays live input deltas and connection time on second ticks and `InputDeltaIncremented`. Refresh persisted baselines on month load, day selection, and rollover only; do not add a periodic database-refresh timer.
- Compute `IsToday` dynamically; selection is UI-only. Cache hourly-bar list references across second ticks so WPF preserves elements/tooltips. Serialize arrow navigation across data-bearing days/months with `_arrowNavigationGate`.

## Shared UI, time, and logging

- Use `ToastMessageViewModelBase` and `ToastMessagePanel` (`ToastMessage`, `ToastVisibility`) rather than duplicating notification state. The host controls placement; repeated messages restart the reading interval.
- `AppColorPalette` owns shared frozen brush resources. Reference them dynamically, including trigger setters. Shared templates are in `Views/Styles/ThemeControls.xaml`; app styles are in `Views/Styles/AppStyles.xaml`.
- `ThemeService` applies the saved palette before setup/recovery windows and updates resources/notifications on the UI thread. Log `Run`s use `SetResourceReference` to preserve document/search/scroll state.
- Rebuild dashboard plots from cached inputs on theme changes, without querying the database. Apply `DashboardPlotTheme` after installing fresh series using original device colors, preserving axes/zoom. Windows 11 title bars follow `WindowTheme`; native dialogs/tray menus keep system appearance.
- Use `TimeFormatter.ToLocalDay` for local dates and `LocalDayToUtc` for local-day UTC boundaries. `ToLocalTime` treats Unspecified as UTC. `NormalizeUtcMinute` converts to UTC before truncation; `TruncateToMinute` preserves DateTimeKind. They are not interchangeable.
- Truncate timestamps using tick arithmetic, not reconstructed calendar components, to preserve the two occurrences of a repeated daylight-saving clock time.
- Pure helpers stay log-free except `TimeFormatter.ToRelativeTime` errors for future timestamps. Log meaningful failures at filesystem/native/PowerShell/recovery boundaries. Use domain language, not method/class names.
- Missing heartbeat files log Debug; malformed heartbeat timestamps and unrecognized device signal patterns log Warning.
- Troubleshooting logs scroll to the bottom on visibility, load, or refresh after background-priority layout. Active search preserves scroll during search/parsing.

## Build, test, and release

- Debug opens a window and defaults LaunchOnLogin to false; Release starts in the tray and defaults it to true. `--tray` forces tray mode. Configuration lives in `AppConstants` and `AppUserSettings`.
- Run `dotnet build "KeyPulse Signal.sln"` and `dotnet test "KeyPulse Signal.sln"`; use `-c Release` for Release checks. A running executable can lock the output; use a separate `OutputPath` when appropriate instead of disrupting the user's session.
- Set `KEYPULSE_TEST_POSTGRES_BIN` to a PostgreSQL bin folder for disposable-cluster integration tests. Report skipped tests when it is unset.
- `PartsViewModelTests` cover drafts, variants, conflicts, search, sorting, and pending writes. `PartsWindowTests` run in a child process to verify actual controls, both themes, and narrow/scaled layouts. `KEYPULSE_THEME_PREVIEW_DIR` captures screenshots and Release timings.
- Generate migrations for both providers in Debug; keep generated migrations/snapshots tool-owned. SQLite: `dotnet ef migrations add Name -c ApplicationDbContext -o Migrations`. PostgreSQL: `dotnet ef migrations add Name -c PostgreSqlApplicationDbContext -o Migrations/PostgreSql`.
- Follow [Docs/RELEASE_PROCESS.md](Docs/RELEASE_PROCESS.md) and [Docs/RELEASE_CHECKLIST.md](Docs/RELEASE_CHECKLIST.md) for versioning, packaging, tagging, and upgrade checks. The README is the overview/setup entry point; the changelog records user-facing release changes.
