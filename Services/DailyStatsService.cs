using KeyPulse.Data;
using KeyPulse.Helpers;
using KeyPulse.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace KeyPulse.Services;

/// <summary>
/// Maintains DailyDeviceStats from two sources:
///   - DeviceEvents:       write-through on every lifecycle event.
///   - ActivitySnapshots:  minute-delayed projector applied once per closed (DeviceId, Minute).
///
/// Also owns the startup rebuild (gap since last clean shutdown) and on-demand per-month rebuild.
/// </summary>
public class DailyStatsService : IDisposable
{
    private sealed class ActivityProjectionState
    {
        public required Dictionary<(DateOnly Day, string DeviceId), DailyDeviceStat> StatsByKey { get; init; }
    }

    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly AppTimerService _appTimerService;
    private bool _disposed;
    private int _activityProjectionDirty = 1;
    private long _latestWriteMinuteTicks;
    private int _projectorDispatchInFlight;

    // Serializes all DailyDeviceStats mutations (range recompute, live projector, connection write-through)
    // so they cannot race on the unique ActivityProjections(DeviceId, Minute) / DailyDeviceStats(Day, DeviceId)
    // indexes. Reentrant: the one-time backfill holds it across its month loop while each per-month
    // RecomputeDailyDeviceStatsForRange re-locks on the same thread.
    private readonly object _projectionGate = new();

    // Shared with DataRetentionService, which must never prune before the one-time backfill has run.
    internal const string FULL_BACKFILL_META_KEY = "DailyStatsFullBackfillAt";

    internal const string CONNECTION_SPAN_RECOMPUTE_META_KEY = "DailyStatsConnectionSpanRecomputedAt";

    public DailyStatsService(IDbContextFactory<ApplicationDbContext> factory, AppTimerService appTimerService)
    {
        _factory = factory;
        _appTimerService = appTimerService;
        _appTimerService.MinuteTick += OnMinuteTick;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Startup rebuild
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Startup entry point. The first run after this feature ships (marker absent) performs a one-time full
    /// historical backfill of DailyDeviceStats from the earliest source day through today, then records the
    /// marker. Every subsequent startup runs a cheap structural drift-recovery pass instead, since the
    /// write-through (connection) and live projector (activity) paths already keep ongoing data current.
    /// Intended to be called on a background thread so the UI can appear immediately.
    /// </summary>
    public void RunStartupRebuild()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            bool backfillDone;
            using (var ctx = _factory.CreateDbContext())
                backfillDone = AppMetaStore.ReadUtc(ctx, FULL_BACKFILL_META_KEY).HasValue;

            if (!backfillDone)
            {
                // The full historical sweep can take a while, so it is timed.
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Log.Information("Daily stats backfill started");

                var earliest = GetEarliestSourceDay();
                if (earliest.HasValue)
                    RebuildAllHistory(earliest.Value, today);

                using (var ctx = _factory.CreateDbContext())
                {
                    AppMetaStore.WriteUtc(ctx, FULL_BACKFILL_META_KEY, DateTime.UtcNow);
                    // A fresh build already uses the fixed connection-span logic, so the recompute is moot.
                    AppMetaStore.WriteUtc(ctx, CONNECTION_SPAN_RECOMPUTE_META_KEY, DateTime.UtcNow);
                }

                Log.Information("Daily stats backfill completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                return; // A fresh full build leaves nothing to reconcile.
            }

            EnsureConnectionSpanRecompute(today);
            ReconcileDriftedDays(today);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Daily stats startup rebuild failed");
        }
    }

    /// <summary>
    /// One-time, idempotent recompute for installs that backfilled before per-day connection attribution was
    /// fixed for sessions spanning midnight — without it they keep showing 0 sessions / 0 connected time on
    /// days whose only session crossed a day boundary. Recomputes connection only (from the never-pruned
    /// DeviceEvents), so aggregated activity for days whose minute snapshots retention has pruned survives.
    /// </summary>
    private void EnsureConnectionSpanRecompute(DateOnly today)
    {
        using (var ctx = _factory.CreateDbContext())
            if (AppMetaStore.ReadUtc(ctx, CONNECTION_SPAN_RECOMPUTE_META_KEY).HasValue)
                return;

        var earliest = GetEarliestSourceDay();
        if (earliest.HasValue)
        {
            ForEachMonth(
                earliest.Value,
                today,
                (from, to) =>
                {
                    using var ctx = _factory.CreateDbContext();
                    RecomputeConnectionStatsForRange(ctx, from, to);
                }
            );
            Log.Information(
                "Daily stats recomputed connection history from {From} to {To}",
                earliest.Value.ToString(),
                today.ToString()
            );
        }

        using (var ctx = _factory.CreateDbContext())
            AppMetaStore.WriteUtc(ctx, CONNECTION_SPAN_RECOMPUTE_META_KEY, DateTime.UtcNow);
    }

    /// <summary>
    /// One-time full rebuild (connection plus activity) from the earliest source day through today. It
    /// recomputes activity from snapshots, so it is only safe before retention prunes them — reserved for a
    /// fresh install's initial backfill.
    /// </summary>
    private void RebuildAllHistory(DateOnly from, DateOnly today)
    {
        if (from > today)
            return;

        ForEachMonth(from, today, RecomputeDailyDeviceStatsForRange);
        Log.Information("Daily stats rebuilt full history from {From} to {To}", from.ToString(), today.ToString());
    }

    /// <summary>
    /// Runs a recompute over each calendar-month chunk in the range, holding the projection gate across the
    /// whole sweep so the live projector cannot interleave. Chunking keeps memory bounded for long histories.
    /// </summary>
    private void ForEachMonth(DateOnly from, DateOnly today, Action<DateOnly, DateOnly> recompute)
    {
        lock (_projectionGate)
        {
            var cursor = from;
            while (cursor <= today)
            {
                var monthEnd = new DateOnly(cursor.Year, cursor.Month, 1).AddMonths(1).AddDays(-1);
                recompute(cursor, monthEnd < today ? monthEnd : today);
                cursor = monthEnd.AddDays(1);
            }
        }
    }

    /// <summary>
    /// Cheap, non-destructive integrity pass over the current month. Detects (DeviceId, day) pairs that are
    /// MISSING from DailyDeviceStats even though a closing DeviceEvent or ActivitySnapshot exists for them —
    /// the one drift the live paths cannot self-heal (the projector skips checkpointed minutes and
    /// write-through only fires on new events). Heals each affected day and logs the discrepancy. Subtle
    /// wrong-value corruption is out of scope here (it would require a full recompute every startup); use the
    /// one-time backfill or RecomputeDailyDeviceStatsForRange for that.
    /// </summary>
    public void ReconcileDriftedDays(DateOnly today)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var (fromBound, toBound) = GetUtcBounds(monthStart, today);

        List<(string DeviceId, DateOnly Day)> driftedKeys;
        using (var ctx = _factory.CreateDbContext())
        {
            // Evidence: (device, local day) pairs with a closing lifecycle event in the window.
            var eventKeys = ctx
                .DeviceEvents.Where(e =>
                    e.DeviceId != ""
                    && e.EventTime >= fromBound
                    && e.EventTime < toBound
                    && (e.EventType == EventTypes.ConnectionEnded || e.EventType == EventTypes.Disconnected)
                )
                .Select(e => new { e.DeviceId, e.EventTime })
                .AsEnumerable()
                .Select(e => (e.DeviceId, Day: TimeFormatter.ToLocalDay(e.EventTime)));

            // Evidence: (device, local day) pairs with an activity snapshot in the window.
            var snapshotKeys = ctx
                .ActivitySnapshots.Where(s => s.Minute >= fromBound && s.Minute < toBound)
                .Select(s => new { s.DeviceId, s.Minute })
                .AsEnumerable()
                .Select(s => (s.DeviceId, Day: TimeFormatter.ToLocalDay(s.Minute)));

            var evidenceKeys = eventKeys.Concat(snapshotKeys).Distinct().ToList();

            var existingKeys = ctx
                .DailyDeviceStats.Where(d => d.Day >= monthStart && d.Day <= today)
                .Select(d => new { d.DeviceId, d.Day })
                .AsEnumerable()
                .Select(d => (d.DeviceId, d.Day))
                .ToHashSet();

            driftedKeys = evidenceKeys.Where(k => !existingKeys.Contains(k)).ToList();
        }

        if (driftedKeys.Count == 0)
        {
            Log.Debug("Daily stats integrity OK from {From} to {To}", monthStart.ToString(), today.ToString());
            return;
        }

        var driftedDays = driftedKeys.Select(k => k.Day).Distinct().OrderBy(d => d).ToList();
        foreach (var day in driftedDays)
            RecomputeDailyDeviceStatsForRange(day, day);

        Log.Warning(
            "Daily stats drift healed {DayCount} day(s): {Keys}",
            driftedDays.Count,
            string.Join(", ", driftedKeys.Select(k => $"{k.Day:yyyy-MM-dd}/{k.DeviceId}"))
        );
    }

    /// <summary>
    /// Returns the earliest local day that has source data (a device lifecycle event or an activity snapshot),
    /// or null when no source data exists yet.
    /// </summary>
    private DateOnly? GetEarliestSourceDay()
    {
        using var ctx = _factory.CreateDbContext();

        var earliestEvent = ctx
            .DeviceEvents.Where(e =>
                e.DeviceId != "" && e.EventType != EventTypes.AppStarted && e.EventType != EventTypes.AppEnded
            )
            .OrderBy(e => e.EventTime)
            .Select(e => (DateTime?)e.EventTime)
            .FirstOrDefault();

        var earliestSnapshot = ctx
            .ActivitySnapshots.OrderBy(s => s.Minute)
            .Select(s => (DateTime?)s.Minute)
            .FirstOrDefault();

        DateTime? earliest = earliestEvent;
        if (earliestSnapshot.HasValue && (!earliest.HasValue || earliestSnapshot.Value < earliest.Value))
            earliest = earliestSnapshot;

        return earliest.HasValue ? TimeFormatter.ToLocalDay(earliest.Value) : null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Core rebuild
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Clears and fully recomputes DailyDeviceStats for a local-day range from source tables.
    /// Safe to call multiple times (idempotent delete plus recompute).
    /// </summary>
    public void RecomputeDailyDeviceStatsForRange(DateOnly from, DateOnly to)
    {
        lock (_projectionGate)
        {
            using var ctx = _factory.CreateDbContext();

            // 1. Delete existing daily rows for this range.
            var existingRows = ctx.DailyDeviceStats.Where(d => d.Day >= from && d.Day <= to).ToList();
            ctx.DailyDeviceStats.RemoveRange(existingRows);

            // 2. Delete projection checkpoints so activity re-projects for the range.
            var (fromBound, toBound) = GetUtcBounds(from, to);
            var existingProjections = ctx
                .ActivityProjections.Where(p => p.Minute >= fromBound && p.Minute < toBound)
                .ToList();
            ctx.ActivityProjections.RemoveRange(existingProjections);

            ctx.SaveChanges();

            // 3. Recompute from DeviceEvents.
            RecomputeConnectionStatsForRange(ctx, from, to);

            // 4. Recompute from ActivitySnapshots.
            RecomputeActivityStatsForRange(ctx, from, to);

            Log.Debug("Daily stats recomputed {From} to {To}", from.ToString(), to.ToString());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Write-through: DeviceEvent → DailyDeviceStats
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by DataService.SaveDeviceEvent after a successful insert.
    /// Applies connection lifecycle updates to DailyDeviceStats.
    /// </summary>
    public void ApplyDeviceEvent(ApplicationDbContext ctx, DeviceEvent deviceEvent)
    {
        if (!deviceEvent.EventType.IsClosingEvent())
            return;

        // Gate against the projector / range recompute so concurrent writers can't collide on the
        // unique DailyDeviceStats(Day, DeviceId) index when first creating a day's row.
        lock (_projectionGate)
        {
            var closeDay = TimeFormatter.ToLocalDay(deviceEvent.EventTime);

            // The session may have opened on an earlier day; recompute every day it spanned, not just the
            // close day, so the opening and any fully-spanned interior days get their connection time.
            var openEvent = ctx
                .DeviceEvents.Where(e =>
                    e.DeviceId == deviceEvent.DeviceId
                    && e.DeviceEventId < deviceEvent.DeviceEventId
                    && (e.EventType == EventTypes.Connected || e.EventType == EventTypes.ConnectionStarted)
                )
                .OrderByDescending(e => e.DeviceEventId)
                .FirstOrDefault();
            var openDay = openEvent != null ? TimeFormatter.ToLocalDay(openEvent.EventTime) : closeDay;

            WriteDeviceConnectionDays(ctx, deviceEvent.DeviceId, openDay, closeDay);
            ctx.SaveChanges();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Minute-delayed projector
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Processes unprojected closed minute snapshots and applies them to DailyDeviceStats.
    /// Runs every 60 seconds via timer. Each (DeviceId, Minute) is applied exactly once.
    /// </summary>
    public void ProjectClosedActivityMinutes()
    {
        if (_disposed)
            return;

        // No writes since last drain; skip all DB work.
        if (Volatile.Read(ref _activityProjectionDirty) == 0)
            return;

        // Gate against range recompute / backfill so projection inserts can't collide on the unique
        // ActivityProjections(DeviceId, Minute) index.
        lock (_projectionGate)
        {
            try
            {
                using var ctx = _factory.CreateDbContext();

                var currentBoundary = TimeFormatter.NormalizeUtcMinute(DateTime.UtcNow);

                // Closed-minute filter remains authoritative.
                var unprojected = ctx
                    .ActivitySnapshots.Where(s =>
                        s.Minute < currentBoundary
                        && !ctx.ActivityProjections.Any(p => p.DeviceId == s.DeviceId && p.Minute == s.Minute)
                    )
                    .ToList();

                if (unprojected.Count == 0)
                {
                    TryClearActivityProjectionDirty(currentBoundary);
                    return;
                }

                var state = BuildActivityProjectionState(ctx, unprojected);
                foreach (var snapshot in unprojected)
                    ApplyActivitySnapshot(ctx, snapshot, DateTime.UtcNow, state);

                ctx.SaveChanges();
                TryClearActivityProjectionDirty(currentBoundary);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Daily stats activity projection failed");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Query helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns the earliest day that has a DailyDeviceStat row, or null if no data exists yet.</summary>
    public DateOnly? GetEarliestDataDay()
    {
        try
        {
            using var ctx = _factory.CreateDbContext();
            return ctx
                .DailyDeviceStats.Where(d =>
                    !ctx.Devices.Any(device => device.DeviceId == d.DeviceId && device.IsHiddenFromDisplay)
                )
                .OrderBy(d => d.Day)
                .Select(d => (DateOnly?)d.Day)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Daily stats earliest-day query failed");
            return null;
        }
    }

    /// <summary>Returns all DailyDeviceStat rows for a local-day range (inclusive).</summary>
    public IReadOnlyList<DailyDeviceStat> GetDailyDeviceStats(DateOnly from, DateOnly to)
    {
        using var ctx = _factory.CreateDbContext();
        return ctx
            .DailyDeviceStats.Where(d => d.Day >= from && d.Day <= to)
            .OrderBy(d => d.Day)
            .ThenBy(d => d.DeviceId)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Returns daily stat rows for a local-day range (inclusive), excluding devices the user has
    /// hidden from display. Presentation shaping belongs to the caller.
    /// </summary>
    public IReadOnlyList<DailyDeviceStat> GetVisibleDailyDeviceStats(DateOnly from, DateOnly to)
    {
        using var ctx = _factory.CreateDbContext();
        return ctx
            .DailyDeviceStats.Where(d => d.Day >= from && d.Day <= to)
            .Where(d => !ctx.Devices.Any(dev => dev.DeviceId == d.DeviceId && dev.IsHiddenFromDisplay))
            .OrderBy(d => d.Day)
            .ThenBy(d => d.DeviceId)
            .ToList()
            .AsReadOnly();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Recomputes connection stats for every device with a session overlapping the range.</summary>
    private void RecomputeConnectionStatsForRange(ApplicationDbContext ctx, DateOnly from, DateOnly to)
    {
        var (_, toBound) = GetUtcBounds(from, to);

        var deviceIds = ctx
            .DeviceEvents.Where(e =>
                e.DeviceId != ""
                && e.EventTime < toBound
                && e.EventType != EventTypes.AppStarted
                && e.EventType != EventTypes.AppEnded
            )
            .Select(e => e.DeviceId)
            .Distinct()
            .ToList();

        foreach (var deviceId in deviceIds)
            WriteDeviceConnectionDays(ctx, deviceId, from, to);

        ctx.SaveChanges();
    }

    private void RecomputeActivityStatsForRange(ApplicationDbContext ctx, DateOnly from, DateOnly to)
    {
        var (fromBound, toBound) = GetUtcBounds(from, to);
        var now = DateTime.UtcNow;

        var snapshots = ctx.ActivitySnapshots.Where(s => s.Minute >= fromBound && s.Minute < toBound).ToList();

        var state = BuildActivityProjectionState(ctx, snapshots);
        foreach (var snapshot in snapshots)
            ApplyActivitySnapshot(ctx, snapshot, now, state);

        ctx.SaveChanges();
    }

    /// <summary>
    /// Overwrites a device's per-day session count, connected seconds, and longest session across a
    /// local-day range. Each session is clipped to each day it overlaps, so a session that opens, spans, or
    /// closes across midnight credits every day it touches — not only the day its events land on. A
    /// still-open session is credited through the end of the range or now, whichever is earlier.
    /// </summary>
    private static void WriteDeviceConnectionDays(ApplicationDbContext ctx, string deviceId, DateOnly from, DateOnly to)
    {
        var (_, toBound) = GetUtcBounds(from, to);
        var rangeEndLocal = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);

        var events = ctx
            .DeviceEvents.Where(e =>
                e.DeviceId == deviceId
                && e.EventTime < toBound
                && e.EventType != EventTypes.AppStarted
                && e.EventType != EventTypes.AppEnded
            )
            .OrderBy(e => e.DeviceEventId)
            .ToList();

        // Clip each session to every day it overlaps within the range, accumulating per-day totals.
        var perDay = new Dictionary<DateOnly, (int Count, long Total)>();
        foreach (var (start, end) in ReconstructSessions(events, rangeEndLocal, DateTime.Now))
        {
            var firstDay = DateOnly.FromDateTime(start);
            if (firstDay < from)
                firstDay = from;
            var lastDay = DateOnly.FromDateTime(end);
            if (lastDay > to)
                lastDay = to;

            for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
            {
                var dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
                var clipStart = start > dayStart ? start : dayStart;
                var clipEnd = end < dayStart.AddDays(1) ? end : dayStart.AddDays(1);
                var seconds = (long)(clipEnd - clipStart).TotalSeconds;
                if (seconds <= 0)
                    continue;

                perDay.TryGetValue(day, out var agg);
                perDay[day] = (agg.Count + 1, agg.Total + seconds);
            }
        }

        foreach (var (day, agg) in perDay)
        {
            var stat = GetOrCreateDailyStat(ctx, day, deviceId);
            stat.SessionCount = agg.Count;
            stat.ConnectionSeconds = agg.Total;
            stat.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Pairs ordered opening/closing events into local-time session intervals. An opening with no matching
    /// close (the live session) ends at <paramref name="rangeEndLocal"/> or <paramref name="nowLocal"/>,
    /// whichever is earlier. A closing with no preceding open (an orphaned close, which the append-only log
    /// should never produce) is assumed to start at its own local midnight.
    /// </summary>
    private static List<(DateTime Start, DateTime End)> ReconstructSessions(
        IReadOnlyList<DeviceEvent> orderedEvents,
        DateTime rangeEndLocal,
        DateTime nowLocal
    )
    {
        var sessions = new List<(DateTime Start, DateTime End)>();
        DateTime? openLocal = null;

        foreach (var evt in orderedEvents)
        {
            var time = TimeFormatter.ToLocalTime(evt.EventTime);
            if (evt.EventType.IsOpeningEvent())
            {
                openLocal ??= time; // keep the earliest open of a redundant run
            }
            else if (evt.EventType.IsClosingEvent())
            {
                var start = openLocal ?? time.Date;
                if (time > start)
                    sessions.Add((start, time));
                openLocal = null;
            }
        }

        if (openLocal != null)
        {
            var end = rangeEndLocal < nowLocal ? rangeEndLocal : nowLocal;
            if (end > openLocal.Value)
                sessions.Add((openLocal.Value, end));
        }

        return sessions;
    }

    /// <summary>Projects one minute snapshot into daily stats and records its projection checkpoint.</summary>
    private void ApplyActivitySnapshot(
        ApplicationDbContext ctx,
        ActivitySnapshot snapshot,
        DateTime projectedAt,
        ActivityProjectionState state
    )
    {
        var minuteLocal = TimeFormatter.ToLocalTime(snapshot.Minute);
        var day = TimeFormatter.ToLocalDay(snapshot.Minute);
        var hour = minuteLocal.Hour;
        var key = (day, snapshot.DeviceId);

        if (!state.StatsByKey.TryGetValue(key, out var stat))
            return;

        stat.Keystrokes += snapshot.Keystrokes;
        stat.MouseClicks += snapshot.MouseClicks;
        stat.MouseMovementSeconds += snapshot.MouseMovementSeconds;

        var totalThisMinute = snapshot.Keystrokes + snapshot.MouseClicks + snapshot.MouseMovementSeconds;
        if (totalThisMinute > 0)
        {
            stat.ActiveMinutes++;
            stat.HourlyInputCount[hour] += totalThisMinute;
        }

        stat.UpdatedAt = projectedAt;

        // Mark minute as projected.
        ctx.ActivityProjections.Add(
            new ActivityProjection
            {
                DeviceId = snapshot.DeviceId,
                Minute = snapshot.Minute,
                ProjectedAt = projectedAt,
            }
        );
    }

    /// <summary>Builds cached per-day/per-device projection state needed to apply minute snapshots efficiently.</summary>
    private ActivityProjectionState BuildActivityProjectionState(
        ApplicationDbContext ctx,
        IReadOnlyCollection<ActivitySnapshot> snapshots
    )
    {
        if (snapshots.Count == 0)
        {
            return new ActivityProjectionState { StatsByKey = [] };
        }

        var impacted = snapshots.Select(s => (TimeFormatter.ToLocalDay(s.Minute), s.DeviceId)).Distinct().ToList();

        var deviceIds = impacted.Select(k => k.DeviceId).Distinct().ToList();
        var days = impacted.Select(k => k.Item1).Distinct().ToList();

        var statsByKey = ctx
            .DailyDeviceStats.Where(d => days.Contains(d.Day) && deviceIds.Contains(d.DeviceId))
            .ToList()
            .ToDictionary(d => (d.Day, d.DeviceId));

        foreach (var key in impacted)
        {
            if (statsByKey.ContainsKey((key.Item1, key.DeviceId)))
                continue;

            var stat = CreateDailyDeviceStat(key.Item1, key.DeviceId);
            ctx.DailyDeviceStats.Add(stat);
            statsByKey[(key.Item1, key.DeviceId)] = stat;
        }

        return new ActivityProjectionState { StatsByKey = statsByKey };
    }

    private static DailyDeviceStat GetOrCreateDailyStat(ApplicationDbContext ctx, DateOnly day, string deviceId)
    {
        var stat =
            ctx.DailyDeviceStats.Local.FirstOrDefault(d => d.Day == day && d.DeviceId == deviceId)
            ?? ctx.DailyDeviceStats.FirstOrDefault(d => d.Day == day && d.DeviceId == deviceId);

        if (stat != null)
            return stat;

        stat = CreateDailyDeviceStat(day, deviceId);
        ctx.DailyDeviceStats.Add(stat);
        return stat;
    }

    private static DailyDeviceStat CreateDailyDeviceStat(DateOnly day, string deviceId)
    {
        return new DailyDeviceStat
        {
            Day = day,
            DeviceId = deviceId,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    private static (DateTime From, DateTime To) GetUtcBounds(DateOnly from, DateOnly toInclusive)
    {
        return (TimeFormatter.LocalDayToUtc(from), TimeFormatter.LocalDayToUtc(toInclusive.AddDays(1)));
    }

    /// <summary>
    /// Marks activity projection as dirty whenever snapshots are written or merged.
    /// Minute is normalized to a UTC minute boundary for projector close-boundary checks.
    /// </summary>
    public void MarkActivitySnapshotWritten(DateTime minute)
    {
        var ticks = TimeFormatter.NormalizeUtcMinute(minute).Ticks;

        while (true)
        {
            var current = Interlocked.Read(ref _latestWriteMinuteTicks);
            if (ticks <= current)
                break;

            if (Interlocked.CompareExchange(ref _latestWriteMinuteTicks, ticks, current) == current)
                break;
        }

        Volatile.Write(ref _activityProjectionDirty, 1);
    }

    /// <summary>Clears the dirty flag when all written minutes are strictly older than the current closed-minute boundary.</summary>
    private void TryClearActivityProjectionDirty(DateTime currentBoundary)
    {
        var latestTicks = Interlocked.Read(ref _latestWriteMinuteTicks);
        if (latestTicks <= 0)
        {
            Volatile.Write(ref _activityProjectionDirty, 0);
            return;
        }

        var latestWriteMinute = new DateTime(latestTicks, DateTimeKind.Utc);
        if (latestWriteMinute < currentBoundary)
            Volatile.Write(ref _activityProjectionDirty, 0);
    }

    private void OnMinuteTick(object? sender, EventArgs e)
    {
        if (_disposed)
            return;

        // Keep heavy DB work off the UI thread and avoid overlapping projector runs.
        if (Interlocked.CompareExchange(ref _projectorDispatchInFlight, 1, 0) != 0)
            return;

        _ = Task.Run(() =>
        {
            try
            {
                ProjectClosedActivityMinutes();
            }
            finally
            {
                Interlocked.Exchange(ref _projectorDispatchInFlight, 0);
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            Log.Debug("Daily stats already disposed");
            return;
        }

        _disposed = true;
        _appTimerService.MinuteTick -= OnMinuteTick;
        Log.Debug("Daily stats disposed");
    }
}
