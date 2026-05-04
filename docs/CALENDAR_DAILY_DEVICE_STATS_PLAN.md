# Calendar Daily Device Stats Plan

## Purpose

This document now describes the **current calendar + daily-stats pipeline as implemented**, not just the original design direction.
It uses both:

- **high-level concepts** — source of truth, write-through lifecycle stats, minute projector, UI overlay
- **concrete implementation names** — `DataService.SaveDeviceEvent`, `DailyStatsService.ProjectClosedActivityMinutes`, `CalendarViewModel.ApplyRealtimeTodayOverlay`, etc.

It also calls out where the original plan has been **implemented**, **changed**, or is still **not wired up**.

---

## Current Outcome

Today, the calendar feature works as a hybrid of:

1. **Persisted daily aggregates** in `DailyDeviceStats`
2. **Exactly-once minute projection checkpoints** in `ActivityProjections`
3. **UI-only live overlays** in `CalendarViewModel` for today's open sessions and unprojected input deltas

At a high level, the pipeline is:

1. **Raw source tables** remain authoritative:
   - `DeviceEvents` for lifecycle / sessions
   - `ActivitySnapshots` for minute activity
2. **`DataService`** writes source rows first.
3. **`DailyStatsService`** projects / recomputes day-level aggregates into `DailyDeviceStats`.
4. **`CalendarViewModel`** reads persisted month/day summaries from `DailyStatsService` and overlays today's live state from:
   - `UsbMonitorService.DeviceList`
   - `RawInputService.InputCountIncremented`

---

## Persisted Tables in the Current Flow

## Source-of-truth tables

- `DeviceEvents`
  - append-only lifecycle log
  - opening events: `ConnectionStarted`, `Connected`
  - closing events: `ConnectionEnded`, `Disconnected`
  - app events: `AppStarted`, `AppEnded`
- `ActivitySnapshots`
  - one canonical row per `(DeviceId, Minute)`
  - stores `Keystrokes`, `MouseClicks`, `MouseMovementSeconds`
- `Devices`
  - mutable snapshot / metadata store used for names, types, current-ish state

## Aggregate / projector tables

- `DailyDeviceStats`
  - one row per `(Day, DeviceId)`
  - stores connection aggregates + activity aggregates
- `ActivityProjections`
  - exactly-once checkpoint table
  - one row per projected `(DeviceId, Minute)`

See:

- `Models/DailyDeviceStat.cs`
- `Models/ActivityProjection.cs`
- `Data/ApplicationDbContext.cs`

---

## Actual Entity Shape

## `DailyDeviceStat`

The original plan included some fields that are **not** in the final model.
The current entity in `Models/DailyDeviceStat.cs` contains:

- grain key:
  - `Day`
  - `DeviceId`
- connection metrics:
  - `SessionCount`
  - `ConnectionDuration`
  - `LongestSessionDuration`
- activity metrics:
  - `Keystrokes`
  - `MouseClicks`
  - `MouseMovementSeconds`
  - `DistinctActiveHours`
  - `ActiveMinutes`
  - `PeakInputHour`
- row maintenance:
  - `UpdatedAt`

### Not implemented from the original proposal

These planned fields are **not** currently stored in `DailyDeviceStats`:

- `FirstActivityAt`
- `LastActivityAt`

That means the current calendar detail panel is intentionally centered on:

- connection duration
- session count
- longest session
- input totals
- active minute/hour breadth
- peak input hour

---

## Database Constraints / Indexes

The current `ApplicationDbContext` configures:

- `DailyDeviceStats`
  - unique index on `(Day, DeviceId)`
  - index on `(Day)`
  - index on `(DeviceId, Day)`
- `ActivityProjections`
  - unique index on `(DeviceId, Minute)`

See `Data/ApplicationDbContext.cs`.

---

## Time Semantics in the Current Flow

## Persisted timestamp normalization

`ApplicationDbContext` converts persisted `DateTime` values through value converters:

- local/unspecified values are stored as UTC
- persisted UTC values are read back as local

## Helper methods used throughout the pipeline

`Helpers/TimeFormatter.cs` is the shared contract for calendar bucketing:

- `TimeFormatter.ToLocalTime(DateTime)`
- `TimeFormatter.ToLocalDay(DateTime)`
- `TimeFormatter.LocalDayToUtc(DateOnly)`
- `TimeFormatter.NormalizeUtcMinute(DateTime)`
- `TimeFormatter.TruncateToMinute(DateTime)`

### Important distinction

- `NormalizeUtcMinute(...)`
  - converts to UTC, then truncates
  - used by `DailyStatsService` projector logic
- `TruncateToMinute(...)`
  - preserves the incoming `DateTimeKind`
  - used by `RawInputService` local-time minute buckets

These are **not interchangeable**.

---

## End-to-End Pipeline

## 1) Lifecycle events enter through `UsbMonitorService`

High-level concept:

- USB connect/disconnect events are detected live and persisted as lifecycle rows.

Concrete flow:

- `UsbMonitorService.AddDeviceEvent(...)`
  - updates UI-bound collections when dispatcher is usable
  - calls `_dataService.SaveDeviceEvent(deviceEvent)`
  - updates the in-memory `Device` snapshot (`SessionStartedAt`, `CommitSession(...)`, etc.)
  - persists the `Device` snapshot via `_dataService.SaveDevice(...)`

The lifecycle event itself is the trigger for daily connection stat recomputation.

---

## 2) `DataService.SaveDeviceEvent(...)` writes source rows first

High-level concept:

- lifecycle rows are committed to `DeviceEvents` before calendar aggregates are updated.

Concrete flow:

- `DataService.SaveDeviceEvent(DeviceEvent deviceEvent)`
  - creates a DbContext
  - adds the `DeviceEvent`
  - calls `ctx.SaveChanges()`
  - then calls `_dailyStats.ApplyDeviceEvent(ctx, deviceEvent)`
- `DataService.SaveDeviceEvent(ApplicationDbContext ctx, DeviceEvent deviceEvent)`
  - same pattern, but inside an existing unit of work
  - used by `DataService.RecoverFromCrash()`

This ordering matters because the recompute query must be able to see the closing event in the database.

---

## 3) Daily connection stats are write-through on closing events

High-level concept:

- connection aggregates are not incrementally patched; they are **fully recomputed for that device/day** whenever a closing lifecycle event is written.

Concrete flow:

- `DailyStatsService.ApplyDeviceEvent(ApplicationDbContext ctx, DeviceEvent deviceEvent)`
  - ignores app events
  - ignores non-closing lifecycle events
  - computes `day = TimeFormatter.ToLocalDay(deviceEvent.EventTime)`
  - calls `RecomputeDailyConnectionStats(ctx, deviceEvent.DeviceId, day)`
  - calls `ctx.SaveChanges()`

### Current recompute algorithm

Implemented in:

- `DailyStatsService.RecomputeDailyConnectionStats(...)`

Behavior:

1. Query all `DeviceEvents` for `(deviceId, day)` using UTC day bounds from `GetUtcBounds(day, day)`.
2. Order by `DeviceEventId`.
3. Walk events in order.
4. On an opening event, remember `currentSessionStartLocal`.
5. On a closing event:
   - use the remembered open if present
   - otherwise assume **local midnight** (`dayStartLocal`) as fallback for cross-midnight carry-in
6. Overwrite:
   - `SessionCount`
   - `ConnectionDuration`
   - `LongestSessionDuration`

### Important difference from the original design

The original plan described interval splitting across multiple days for each close event.
The implemented version is simpler:

- recompute is **day-local**
- session count is effectively the number of closing events with positive overlap on that local day
- if the first event of the day is a close, start is assumed to be midnight

This is the current behavior to document and preserve unless the algorithm changes again.

---

## 4) Crash recovery participates in the same write-through path

High-level concept:

- synthetic close events inserted during crash recovery also feed daily stats through the same pipeline.

Concrete flow:

- `DataService.RecoverFromCrash()`
  - detects an unmatched `AppStarted`
  - determines `crashTime` from `HeartbeatFile.Read()` or session start fallback
  - inserts synthetic `ConnectionEnded` events through `SaveDeviceEvent(ctx, new DeviceEvent { ... })`
  - inserts synthetic `AppEnded` the same way

Because it uses the `ctx` overload of `SaveDeviceEvent(...)`, daily connection stats are recomputed during crash recovery too.

---

## 5) Raw input becomes canonical minute snapshots first

High-level concept:

- raw keyboard/mouse activity does **not** update `DailyDeviceStats` directly.
- canonical minute rows land in `ActivitySnapshots` first.

Concrete flow:

- `RawInputService`
  - tracks activity into in-memory minute buckets keyed by `(DeviceId, Minute)`
  - uses `DateTime.Now.TruncateToMinute()` for local-time buckets
  - flushes finished buckets through `DataService.SaveActivitySnapshots(...)`
- `DataService.SaveActivitySnapshots(IEnumerable<ActivitySnapshot> snapshots)`
  - upserts/merges canonical snapshot rows
  - merges into an existing `(DeviceId, Minute)` snapshot when needed
  - updates `Device.TotalInputCount`
  - if a minute changed, calls `_dailyStats.MarkActivitySnapshotWritten(snapshot.Minute)`
  - saves all changes at the end

This is the handoff point from source-of-truth minute activity to the delayed projector path.

---

## 6) Minute activity is projected later via `DailyStatsService`

High-level concept:

- activity aggregates are updated by a **minute-delayed exactly-once projector**.
- only **closed minutes** are eligible.

Concrete flow:

- `DailyStatsService.MarkActivitySnapshotWritten(DateTime minute)`
  - normalizes minute with `TimeFormatter.NormalizeUtcMinute(...)`
  - updates `_latestWriteMinuteTicks`
  - sets `_activityProjectionDirty = 1`
- `AppTimerService.MinuteTick` drives `DailyStatsService.OnMinuteTick(...)`
- `OnMinuteTick(...)`
  - avoids overlap using `_projectorDispatchInFlight`
  - dispatches `ProjectClosedActivityMinutes()` on a background task
- `ProjectClosedActivityMinutes()`
  - skips if disposed
  - skips if `_activityProjectionDirty == 0`
  - computes `currentBoundary = TimeFormatter.NormalizeUtcMinute(DateTime.UtcNow)`
  - queries `ActivitySnapshots` where:
    - `Minute < currentBoundary`
    - no matching `ActivityProjections` row exists
  - builds cached state with `BuildActivityProjectionState(...)`
  - applies each minute via `ApplyActivitySnapshot(...)`
  - writes `ActivityProjection` checkpoints
  - saves changes
  - clears the dirty flag when appropriate via `TryClearActivityProjectionDirty(...)`

### Exactly-once behavior

`ActivityProjections` is the guardrail.
A minute is considered projected when a row exists for `(DeviceId, Minute)`.
This prevents reapplying closed-minute activity across retries or restarts.

---

## 7) What `ApplyActivitySnapshot(...)` actually updates

High-level concept:

- each closed minute contributes input totals and activity-shape metrics to the local day row.

Concrete flow:

- `DailyStatsService.ApplyActivitySnapshot(...)`
  - resolves:
    - `minuteLocal = TimeFormatter.ToLocalTime(snapshot.Minute)`
    - `day = TimeFormatter.ToLocalDay(snapshot.Minute)`
    - `hour = minuteLocal.Hour`
  - increments:
    - `Keystrokes`
    - `MouseClicks`
    - `MouseMovementSeconds`
  - if the minute had any activity:
    - increments `ActiveMinutes`
    - tracks hour membership through `SeenHoursByKey`
    - updates `DistinctActiveHours`
    - accumulates per-hour totals in `HourTotalsByKey`
    - recomputes `PeakInputHour`
  - updates `UpdatedAt`
  - inserts the corresponding `ActivityProjection` checkpoint

### Important difference from the original plan

The original design mentioned `FirstActivityAt` / `LastActivityAt` projection.
Those are **not part of the implemented entity or projector path**.

---

## 8) Full rebuild path exists, but startup wiring is currently commented out

High-level concept:

- there is a complete bounded rebuild implementation, but startup does **not currently invoke it**.

Concrete flow:

- implemented methods:
  - `DailyStatsService.RebuildGapOnStartup()`
  - `DailyStatsService.RecomputeDailyDeviceStatsForRange(DateOnly from, DateOnly to)`
  - `RecomputeConnectionStatsForRange(...)`
  - `RecomputeActivityStatsForRange(...)`
- current `App.xaml.cs` status:
  - startup contains a commented-out background call:
    - `ServiceProvider.GetRequiredService<DailyStatsService>().RebuildGapOnStartup();`

### What `RebuildGapOnStartup()` would do if enabled

1. Read `LastCleanShutdownAt` from `AppMeta`.
2. Convert it to a local day with `TimeFormatter.ToLocalDay(...)`.
3. Fallback to the last 7 days if no marker exists.
4. Cap the rebuild start to the current month.
5. Delete the marker.
6. Call `RecomputeDailyDeviceStatsForRange(from, today)` unless a same-day clean shutdown means there is no gap.

### Current state summary

- **Implemented:** bounded rebuild logic
- **Not currently wired on startup:** yes
- **Manual/on-demand month rebuild from the original plan:** not currently implemented

---

## 9) Current clean-shutdown marker flow

High-level concept:

- the clean-shutdown marker is written by `DailyStatsService.Dispose()` during normal app disposal.

Concrete flow:

- `DailyStatsService` subscribes to `AppTimerService.MinuteTick` in its constructor
- `DailyStatsService.Dispose()`:
  - unsubscribes `MinuteTick`
  - calls `WriteLastCleanShutdownAt()`
- `App.OnExit(...)`
  - normally disposes `ServiceProvider`, which disposes singleton services
- `App.OnSessionEnding(...)`
  - sets `_isSessionEnding = true`
- `DisposeServicesForExitPath()`
  - skips DI disposal during Windows session end to avoid blocking OS shutdown

### Consequence

On Windows session end, `DailyStatsService.Dispose()` is skipped, so `LastCleanShutdownAt` may not be written.
That is why the rebuild path is designed to tolerate missing markers and fall back safely.

---

## Calendar Query / Read Pipeline

## Month summaries for tiles

High-level concept:

- month tiles are read from `DailyDeviceStats`, not from raw events.

Concrete flow:

- `CalendarViewModel.LoadCurrentMonth()`
  - calls `_dailyStatsService.GetCalendarDaySummaries(year, month)` on a background thread
- `DailyStatsService.GetCalendarDaySummaries(int year, int month)`
  - queries `DailyDeviceStats` rows for the month
  - resolves device metadata from `Devices`
  - groups by `Day`
  - fills in missing days with `HasData = false`
  - calls `ToCalendarDaySummary(...)`
- `ToCalendarDaySummary(...)`
  - builds `CalendarTileDevice` DTOs
  - sorts by `DeviceTypes` rank: keyboard, mouse, other

### Current tile DTO shape

`CalendarDaySummary` currently contains:

- `Day`
- `HasData`
- `IsSelected`
- computed `IsToday`
- `Devices`

It does **not** currently expose day-level totals such as:

- total connection duration
- total input count
- active device count

So the implemented calendar month grid is intentionally lighter than the original tile-metrics proposal.

---

## Day detail reads

High-level concept:

- the detail panel is backed entirely by `DailyDeviceStats` + device metadata.

Concrete flow:

- `CalendarViewModel.LoadSelectedDayDetails()`
  - calls `_dailyStatsService.GetCalendarDayDetail(day)` on a background thread
- `DailyStatsService.GetCalendarDayDetail(DateOnly day)`
  - loads that day's `DailyDeviceStats`
  - resolves `DeviceName` / `DeviceType` from `Devices`
  - returns `CalendarDeviceDetail`

### Current detail DTO shape

`CalendarDeviceDetail` contains:

- `SessionCount`
- `ConnectionDuration`
- `LongestSessionDuration`
- `Keystrokes`
- `MouseClicks`
- `MouseMovementSeconds`
- `ActiveMinutes`
- `DistinctActiveHours`
- `PeakInputHour`
- UI-only `LiveInputDelta`
- computed `TotalInput`

---

## Calendar UI Overlay Pipeline

## Baseline load

High-level concept:

- the calendar shows persisted stats as its baseline.

Concrete flow:

- `CalendarViewModel.OnVisible()`
  - queries `_dailyStatsService.GetEarliestDataDay()`
  - calls `LoadCurrentMonth()`
- `LoadCurrentMonth()`
  - fetches month summaries
  - passes them to `ApplySummaries(...)`
- `ApplySummaries(...)`
  - rebuilds `DaySummaries`
  - rebuilds `CalendarGridItems`
  - re-applies tile selection state
  - immediately calls `ApplyRealtimeTodayOverlay()`

---

## Today's persisted-baseline refresh

High-level concept:

- today's persisted baseline is refreshed every 30 seconds to pick up projector commits and closed-session writes.

Concrete flow:

- `CalendarViewModel` subscribes to `AppTimerService.ThirtySecondTick`
- `OnThirtySecondTick(...)`
  - only runs when the displayed month is the current month
  - re-queries `_dailyStatsService.GetCalendarDaySummaries(...)`
  - replaces only today's persisted summary in `DaySummaries`
  - calls `ApplyRealtimeTodayOverlay()`

This is the persisted-baseline refresh path, not the live input path.

---

## Today's live overlay

High-level concept:

- today's tile/detail can be more current than the DB by combining persisted aggregates with live connection and input state.

Concrete flow:

- `CalendarViewModel.OnSecondTick(...)`
  - calls `ApplyRealtimeTodayOverlay()` every second
- `CalendarViewModel.OnInputCountIncremented(deviceId, delta)`
  - updates `_todayLiveInputDeltaByDevice`
  - uses dispatcher-safe immediate refresh
  - calls `ApplyRealtimeTodayOverlay()`

### What `ApplyRealtimeTodayOverlay()` does

1. Only operates for the currently displayed month if it is the current month.
2. Reads the persisted today row from `DaySummaries`.
3. Builds `connectionOverlayByDevice` from `UsbMonitorService.DeviceList`:
   - uses `Device.SessionStartedAt`
   - clamps cross-midnight sessions to local midnight
4. Reads `inputOverlayByDevice` from `_todayLiveInputDeltaByDevice`.
5. Creates a new `CalendarDaySummary` for today with:
   - persisted `HasData` plus any live connection/input state
   - merged device list from `BuildRealtimeTileDevices(...)`
6. Replaces today's tile in:
   - `DaySummaries`
   - `CalendarGridItems`
7. If today's day is selected, calls `ApplyRealtimeTodayDetailOverlay(...)`

### What `ApplyRealtimeTodayDetailOverlay(...)` does

For each relevant device ID, it merges:

- persisted day detail from `_todayPersistedDetailByDevice`
- current connection session from `UsbMonitorService.DeviceList`
- unprojected live input delta from `_todayLiveInputDeltaByDevice`

The resulting displayed values are:

- `SessionCount`
  - persisted count + 1 if the device is currently connected
- `ConnectionDuration`
  - persisted duration + live elapsed open-session seconds
- `LongestSessionDuration`
  - `max(persisted longest, current open-session overlap)`
- `TotalInput`
  - persisted input + `LiveInputDelta`

This overlay is **UI-only**. It does not write back to `DailyDeviceStats`.

---

## Current Accuracy Model

## Persisted data

Persisted `DailyDeviceStats` is eventually consistent through two update paths:

- **connection stats** — updated on closing lifecycle events through `ApplyDeviceEvent(...)`
- **activity stats** — updated on the next minute projector pass through `ProjectClosedActivityMinutes()`

## UI freshness for today

Today's displayed values are fresher than the persisted table because `CalendarViewModel` overlays:

- live open-session duration every second
- live input deltas immediately on `InputCountIncremented`
- persisted baseline refresh every 30 seconds

This means:

- closed-session stats persist immediately after close
- minute activity stats persist on the projector cadence
- today's on-screen values are still more responsive than the DB alone

---

## What Changed vs the Original Plan

## Implemented or superseded

- `DailyDeviceStats` table exists
- `ActivityProjections` checkpoint table exists
- lifecycle stats are write-through on close via `ApplyDeviceEvent(...)`
- minute activity stats are projector-driven via `ProjectClosedActivityMinutes()`
- calendar month/day queries exist:
  - `GetCalendarDaySummaries(...)`
  - `GetCalendarDayDetail(...)`
- calendar view + viewmodel exist
- current-day UI overlay is implemented
- `DeviceTypes` enums are used in calendar DTOs instead of raw strings

## Implemented differently than planned

- Connection recompute is **day-local full replay**, not multi-day interval splitting during each close write.
- Current day overlay is **every second + immediate input event refresh**, not only every 30 seconds.
- Day detail reads come from `DailyDeviceStats` + `Devices`; there is no extra day-scoped `ActivitySnapshots` join for detail fields.
- Month tiles currently show **device presence/list**, not rich day totals like total input or total connection duration.

## Still not wired / deferred

- `RebuildGapOnStartup()` exists but startup invocation is commented out in `App.xaml.cs`
- original per-month rebuild/session-cache behavior is not implemented
- no production/manual repair command is exposed
- `FirstActivityAt` / `LastActivityAt` are not part of the entity
- no tile badges or advanced tile metrics from the original UI plan

---

## Recommended Mental Model for Future Work

When changing this feature, think of the system as four layers:

1. **Capture layer**
   - `UsbMonitorService`
   - `RawInputService`
2. **Source-of-truth persistence layer**
   - `DataService.SaveDeviceEvent(...)`
   - `DataService.SaveActivitySnapshots(...)`
   - tables: `DeviceEvents`, `ActivitySnapshots`
3. **Daily aggregate layer**
   - `DailyStatsService.ApplyDeviceEvent(...)`
   - `DailyStatsService.ProjectClosedActivityMinutes()`
   - table: `DailyDeviceStats`
   - checkpoint table: `ActivityProjections`
4. **Calendar presentation layer**
   - `DailyStatsService.GetCalendarDaySummaries(...)`
   - `DailyStatsService.GetCalendarDayDetail(...)`
   - `CalendarViewModel.ApplyRealtimeTodayOverlay()`
   - `CalendarViewModel.ApplyRealtimeTodayDetailOverlay()`

That separation is the core of the current implementation.

---

## Key Files to Inspect Along This Pipeline

- `Services/UsbMonitorService.cs`
- `Services/RawInputService.cs`
- `Services/DataService.cs`
- `Services/DailyStatsService.cs`
- `ViewModels/CalendarViewModel.cs`
- `ViewModels/Calendar/CalendarDTOs.cs`
- `Models/DailyDeviceStat.cs`
- `Models/ActivityProjection.cs`
- `Data/ApplicationDbContext.cs`
- `App.xaml.cs`
