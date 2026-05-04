# Calendar Daily Device Stats Plan

## Goals

- Add a calendar view that shows daily USB device usage at a glance.
- Persist a day-level aggregate table for fast month/week queries.
- Keep aggregates updated incrementally from `DeviceEvents` and closed-minute `ActivitySnapshots`.
- Rebuild the gap since last clean shutdown on startup; rebuild on-demand per month on first navigation.

## Non-Goals

- No full historical rebuild across all years of data.
- No change to existing source-of-truth tables (`DeviceEvents`, `ActivitySnapshots`).

## Source of Truth

- Connection lifecycle: `DeviceEvents`
- Input activity: `ActivitySnapshots`
- Device metadata for detail display: `Devices`

## Timezone Policy

- Canonical source timestamps are stored as UTC (all persisted `DateTime` values).
- Calendar bucketing uses a local calendar timezone (`TimeZoneInfo.Local` by default).
- `DailyDeviceStats.Day` is the local day key (`DateOnly`) derived from persisted timestamps.
- Day buckets are half-open local intervals: `[localMidnight, nextLocalMidnight)`.
- DST transitions are handled by local-midnight boundaries (days may be 23/24/25 hours).

## New Table

Add one table: `DailyDeviceStats`.

### Entity (proposed)

```csharp
public sealed class DailyDeviceStat
{
    public int DailyDeviceStatId { get; set; }

    // Grain key
    public DateOnly Day { get; set; }
    public string DeviceId { get; set; } = "";

    // Connection metrics (from DeviceEvents)
    public int SessionCount { get; set; }
    public long ConnectionDuration { get; set; }
    public long LongestSessionDuration { get; set; }

    // Activity metrics (from ActivitySnapshots)
    public long Keystrokes { get; set; }
    public long MouseClicks { get; set; }
    public long MouseMovementSeconds { get; set; }
    public DateTime? FirstActivityAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public int DistinctActiveHours { get; set; }
    public int ActiveMinutes { get; set; }
    public int PeakInputHour { get; set; }

    public DateTime UpdatedAt { get; set; }
}
```

### Constraints / Indexes

- Unique index: `(Day, DeviceId)`
- Index: `(Day)` for month range fetch
- Index: `(DeviceId, Day)` for per-device trend views

## Incremental Collection Strategy (+ Hybrid Rebuild)

Use a mixed strategy:

- `DeviceEvents` update `DailyDeviceStats` immediately on write.
- `ActivitySnapshots` update `DailyDeviceStats` with a minute-delayed projector that processes only closed minutes.

## 1) From `DeviceEvents` (connection duration)

Hook in `DataService.SaveDeviceEvent` after insert succeeds.

### Event handling rules

- Opening events: `ConnectionStarted`, `Connected` — no per-day counter needed
- Closing events: `ConnectionEnded`, `Disconnected`
  - Determine session start for this close using same-day fallback rules
  - Split duration across crossed days and accumulate seconds into `ConnectionDuration` for each day touched
  - Increment `SessionCount` for each day where this session has non-zero overlap
  - Update `LongestSessionDuration` for each day touched using that day's overlap seconds

**Note**: Sessions spanning midnight still accumulate `ConnectionDuration` on both days. `SessionCount` reflects the number of sessions with non-zero overlap on that day.

### Interval split rule

For interval `[start, end)`:

- Interpret `start`/`end` as persisted UTC instants.
- Convert boundaries to local calendar timezone and split by local midnight boundaries.
- If same local day: add total seconds to that `Day`.
- If multi-day: split by local day boundaries and add seconds to each day bucket.

### Session start fallback rule (no open-match stack)

No historical open/close matching is required.

For each closing event on day `D`:

- Resolve `D` from `closeTime` in local calendar timezone.
- Try to find an opening event for the same `DeviceId` on day `D` with `EventTime <= closeTime`.
- If found, use the latest same-day opening event as `start`.
- If not found, use `start = D 00:00:00` local converted to stored (UTC) format.

Then apply interval split on `[start, closeTime)`.

## 2) From `ActivitySnapshots` (input activity)

Do not update `DailyDeviceStats` directly inside `SaveActivitySnapshots`.

`SaveActivitySnapshots` remains responsible for canonical minute rows in `ActivitySnapshots` (merge/upsert to one row per `(DeviceId, Minute)`).

### Minute-delayed activity projector

Run a periodic projector (every 60 seconds) that processes only closed minutes (`Minute < currentMinute`).

For each closed `(DeviceId, Minute)` snapshot not yet projected:

- `Keystrokes += snapshot.Keystrokes`
- `MouseClicks += snapshot.MouseClicks`
- `MouseMovementSeconds += snapshot.MouseMovementSeconds`
- `activeMinuteDelta = 1` if the snapshot has non-zero activity
- `distinctHourDelta = 1` only when that local hour becomes newly active for that day/device
- `FirstActivityAt` = min(existing, snapshot minute)
- `LastActivityAt` = max(existing, snapshot minute)
- Track per-hour input totals (`Keystrokes + MouseClicks + MouseMovementSeconds`) and update `PeakInputHour` to the local hour with the highest total for that day/device

Apply values to `DailyDeviceStats` for `Day(ToLocal(snapshot.Minute))` and mark the minute as projected.

### Idempotency / exactly-once guard

Use a projector checkpoint (e.g., `DailyActivityMinuteProjection`) keyed by `(DeviceId, Minute)` so each closed minute is projected once, even across restarts.

This removes merge-time delta complexity while preserving current `TotalInputCount` semantics.

## 3) Device metadata resolution

`DailyDeviceStats` stores only numeric metrics. Device name/type are resolved at read time from `Devices` for the day detail panel.

## Write-Path Integration Points

In `DataService`:

- `SaveDeviceEvent(DeviceEvent deviceEvent)`
  - After successful insert: `ApplyDailyStatsFromDeviceEvent(ctx, deviceEvent)`
- `SaveActivitySnapshots(IEnumerable<ActivitySnapshot> snapshots)`
  - Persist canonical minute snapshots only (no direct `DailyDeviceStats` updates)
- `ProjectClosedActivityMinutes()` (timer-driven)
  - Read unprojected closed minutes
  - Apply to `DailyDeviceStats`
  - Mark projected minutes

Device-event stats are write-through; activity stats are minute-delayed and projector-driven.

## Current Day Accuracy

Real-time accuracy is not a goal for the calendar. Activity remains batched (flushed every ~60 seconds) and open
session elapsed time is applied as a UI overlay every 30 seconds. Today's tile may therefore lag:

| Lag source | Cause | Max lag |
|---|---|---|
| Activity (`InputCount`, activity span, active-hour breadth) | Closed-minute projector after snapshot flush | ~120 seconds |
| Open session `ConnectionDuration` | VM overlays live elapsed time every 30 seconds via `ThirtySecondTick` | ~30 seconds |

This lag is acceptable for calendar usage and avoids merge-time double-apply complexity.

The `Partial day` UX marker (distinct tile style for today) communicates that the day is still accumulating.
Today's tile refreshes every 30 seconds via `ThirtySecondTick`, keeping it within ~90 seconds of current activity
(60-second flush lag + up to 30-second tick lag).

### Open session overlay (today only)

For devices that are currently connected, `ConnectionDuration` in `DailyDeviceStats` lags until the session closes.
On each `ThirtySecondTick` refresh of today's tile, the VM overlays live elapsed time:

```
displayConnectionDuration = stat.ConnectionDuration
    + (Device.SessionStartedAt.HasValue
        ? (DateTime.UtcNow - Device.SessionStartedAt.Value.ToUniversalTime()).TotalSeconds
        : 0)
```

This overlay is UI-only — no DB write occurs. The persisted `ConnectionDuration` is updated only when the
closing event is written (same as today). The overlay is read from the in-memory `UsbMonitorService.DeviceList`,
which is already on the UI thread and requires no locking.

## Startup / Shutdown Behavior

### On clean shutdown

Write `LastCleanShutdownAt` to `AppMeta` before exiting.

### On startup rebuild

1. Read `LastCleanShutdownAt` from `AppMeta`.
2. If present: rebuild `DailyDeviceStats` from `LastCleanShutdownAt.LocalDay` through `today` (covers the gap since last run).
3. If absent (first install or marker cleared): rebuild last 7 local days as a safe fallback.
4. After rebuild, clear `LastCleanShutdownAt` so next startup only covers the new gap.

This avoids rebuilding already-correct past days on every startup. After a crash, the unwritten `LastCleanShutdownAt` triggers the 7-day fallback which covers all plausible gap days.

Existing startup flows (`RecoverFromCrash`, `RebuildDeviceSnapshots`) remain unchanged and run before this rebuild.
Crash recovery writes synthetic closing events that flow through `SaveDeviceEvent`, so daily stats stay consistent automatically.

### On-demand per-month rebuild

When the user navigates to a month:

1. Check if that month has been rebuilt this session (track in-memory `HashSet<YearMonth>`).
2. If not, run `RecomputeDailyDeviceStatsForRange` for that month and mark it rebuilt.
3. Past months only rebuild once per session; the current month rebuilds on every navigation to it.

This ensures stale or missing data is corrected the moment the user sees a month, without paying startup cost for months they never visit.

## Bounded repair

One method for both startup, on-demand, and manual maintenance:

- `RecomputeDailyDeviceStatsForRange(DateOnly from, DateOnly to)`

- `from`/`to` are local day keys; implementation converts to UTC bounds internally.
- Clears and recomputes `DailyDeviceStats` rows for the range from source tables.
- Safe to call multiple times; existing rows for the range are deleted and replaced.

## Query API for Calendar

Add DataService methods:

- `GetDailyDeviceStats(DateOnly from, DateOnly to)`
  - returns per-device day rows (`from`/`to` interpreted in local calendar timezone)
- `GetCalendarDaySummaries(DateOnly from, DateOnly to)`
  - grouped day totals across all devices for tile rendering (`from`/`to` local-day range)
- `GetCalendarDayDetail(DateOnly day)`
  - per-device list sorted by connected time or activity, with detail activity breakdown joined from `ActivitySnapshots` (same local day)

## Calendar VM Refresh Strategy

`CalendarViewModel` is transient (like `DashboardViewModel`) and subscribes to `AppTimerService.ThirtySecondTick`
for periodic refresh. Subscriptions are set up on construction and cleaned up when the view is destroyed, consistent
with the Dashboard pattern.

Refresh triggers:

| Trigger | Action |
|---|---|
| Tab becomes visible (`IsVisibleChanged`) | Load current month; trigger on-demand rebuild if not yet rebuilt this session |
| Month navigation | Load new month; trigger on-demand rebuild if not yet rebuilt this session |
| `ThirtySecondTick` | Reload today's tile only |
| Manual refresh (optional) | Reload full current month |

Past days are immutable once written. Only today's tile needs periodic refresh via `ThirtySecondTick`.
Per-month on-demand rebuild runs once per session per past month; current month rebuilds on each visit.

## Calendar UI Plan

## Placement

- Add a dedicated `Calendar` tab/viewmodel (recommended)
- Keep Dashboard focused on range charts/cards
- Calendar tab opens to the current month in a standard month grid.

## Tile metrics (month grid)

Per day (combined across all devices):

- Connected devices (distinct)
- Total connection duration
- Input count (`Keystrokes + MouseClicks + MouseMovementSeconds` combined)
- Active devices (distinct with activity)
- Optional badge: high-activity day / no-activity-but-connected day

## Day detail panel

On day click:

- Top devices by connection duration
- Top devices by total input count (`Keystrokes + MouseClicks + MouseMovementSeconds`)
- Per-device breakdown: connection duration, longest session duration, session count, keystrokes, mouse clicks, mouse movement seconds, active minutes, distinct active hours, peak input hour
- First activity at and last activity at
- Uses `DailyDeviceStats` + day-scoped `ActivitySnapshots` query for detail values.

## Filters

- Device type: All / Keyboard / Mouse / Other
- Sort mode: Connected time / Activity / Name

## UX states

- `No data` day (before feature rollout or truly no events)
- `Partial day` marker for current day
- Loading placeholders for month switch

## Rollout Plan

1. Migration: add `DailyDeviceStats` table + indexes.
2. Wire write-path updates in `DataService` methods.
3. Add query methods and DTOs for calendar VM.
4. Build `CalendarViewModel` + `CalendarView`.
5. Add tab navigation and empty-state copy.
6. Add optional bounded repair command (dev-only).

## Testing Plan

## Unit tests

- Session split across midnight/day boundaries
- Same-day closing with no same-day opening falls back to midnight start
- Activity projection correctness for closed minutes (`Keystrokes`, `MouseClicks`, `MouseMovementSeconds`, activity span, active-hour updates, `PeakInputHour`)
- Closed-minute projector applies each minute exactly once (idempotent checkpoint)
- Idempotency under duplicate event insert failures

## Integration tests

- Save opening + closing events and verify `ConnectionDuration`
- Verify `LongestSessionDuration` uses per-day overlap for cross-midnight sessions
- Save closing event with no same-day opening and verify midnight fallback duration
- Save snapshot updates to same minute and verify projector applies that minute once
- Verify `FirstActivityAt`, `LastActivityAt`, `DistinctActiveHours`, and `PeakInputHour` updates
- Restart during projection and verify checkpoint resumes without re-applying projected minutes
- Crash-recovery inserted `ConnectionEnded` updates daily stats as expected

## UI tests (manual)

- Month navigation performance
- Future month navigation is disabled
- Calendar tab opens to current month grid
- Tile aggregated values match detail panel sums
- Rename device updates new days without breaking existing rows

## Performance Notes

- Reads should primarily hit `DailyDeviceStats` with day-range filtering.
- No expensive joins on `DeviceEvents`/`ActivitySnapshots` for month calendar render.
- Keep row size moderate; avoid storing per-minute arrays/blobs.

## Open Decisions

- Whether to expose optional day-range repair in production settings.

