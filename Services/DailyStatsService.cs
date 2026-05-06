using KeyPulse.Data;
using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.ViewModels.Calendar;
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

    private const string LAST_CLEAN_SHUTDOWN_META_KEY = "LastCleanShutdownAt";

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
    /// Reads LastCleanShutdownAt from AppMeta and rebuilds DailyDeviceStats for the gap period.
    /// Falls back to last 7 local days when the marker is absent (first install or unclean shutdown).
    /// Optimized: rebuild is capped to the current month to keep startup work bounded.
    /// </summary>
    public void RebuildGapOnStartup()
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Log.Information("Daily stats rebuild started");

            var today = DateOnly.FromDateTime(DateTime.Now);
            var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
            DateOnly from;
            var skipRebuild = false;

            using (var ctx = _factory.CreateDbContext())
            {
                var lastShutdown = AppMetaStore.ReadUtc(ctx, LAST_CLEAN_SHUTDOWN_META_KEY);

                if (lastShutdown.HasValue)
                {
                    var lastShutdownDay = TimeFormatter.ToLocalDay(lastShutdown.Value);
                    from = lastShutdownDay;

                    // A clean shutdown earlier today has no offline gap to rebuild.
                    // Re-running today would re-apply cross-midnight close splits to prior days.
                    if (lastShutdownDay == today)
                        skipRebuild = true;
                }
                else
                {
                    from = today.AddDays(-6); // 7 days inclusive fallback
                }

                // Keep startup rebuild bounded to current month.
                if (from < currentMonthStart)
                    from = currentMonthStart;

                // Clear the marker so next startup only covers the new gap.
                AppMetaStore.Delete(ctx, LAST_CLEAN_SHUTDOWN_META_KEY);
            }

            if (!skipRebuild && from <= today)
                RecomputeDailyDeviceStatsForRange(from, today);
            else if (skipRebuild)
                Log.Debug("Daily stats rebuild skipped because last clean shutdown was already today ({Day})", today);

            stopwatch.Stop();
            Log.Information("Daily stats rebuild completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Daily stats rebuild failed");
        }
    }

    /// <summary>
    /// Writes LastCleanShutdownAt to AppMeta. Call once during clean shutdown before DB is closed.
    /// </summary>
    public void WriteLastCleanShutdownAt()
    {
        try
        {
            using var ctx = _factory.CreateDbContext();
            AppMetaStore.WriteUtc(ctx, LAST_CLEAN_SHUTDOWN_META_KEY, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to write LastCleanShutdownAt to AppMeta");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Core rebuild
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Clears and fully recomputes DailyDeviceStats for a local-day range from source tables.
    /// Safe to call multiple times (idempotent delete + recompute).
    /// </summary>
    public void RecomputeDailyDeviceStatsForRange(DateOnly from, DateOnly to)
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

        Log.Debug("Recomputed daily stats for {From} to {To}", from.ToString(), to.ToString());
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

        var day = TimeFormatter.ToLocalDay(deviceEvent.EventTime);
        RecomputeDailyConnectionStats(ctx, deviceEvent.DeviceId, day);
        ctx.SaveChanges();
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
            Log.Error(ex, "Activity minute projector failed");
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
            return ctx.DailyDeviceStats.OrderBy(d => d.Day).Select(d => (DateOnly?)d.Day).FirstOrDefault();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to query earliest data day");
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
    /// Returns day-level summaries (across all devices) for a calendar month.
    /// Each entry covers one day; days with no data are still included so the grid can render empty tiles.
    /// </summary>
    public IReadOnlyList<CalendarDaySummary> GetCalendarDaySummaries(int year, int month)
    {
        var from = new DateOnly(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var to = new DateOnly(year, month, daysInMonth);

        using var ctx = _factory.CreateDbContext();

        var statRows = ctx.DailyDeviceStats.Where(d => d.Day >= from && d.Day <= to).ToList();
        var deviceIds = statRows.Select(r => r.DeviceId).Distinct().ToList();
        var devicesById = ctx.Devices.Where(d => deviceIds.Contains(d.DeviceId)).ToDictionary(d => d.DeviceId);

        var grouped = statRows.GroupBy(d => d.Day).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<CalendarDaySummary>(daysInMonth);
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            grouped.TryGetValue(day, out var dayRows);
            result.Add(ToCalendarDaySummary(day, dayRows, devicesById));
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Returns per-device detail for a single local day, sorted by connection seconds descending.
    /// Device name and type are resolved from the Devices table.
    /// </summary>
    public IReadOnlyList<CalendarDeviceDetail> GetCalendarDayDetail(DateOnly day)
    {
        using var ctx = _factory.CreateDbContext();

        var rows = ctx.DailyDeviceStats.Where(d => d.Day == day).ToList();
        if (rows.Count == 0)
            return Array.Empty<CalendarDeviceDetail>();

        var deviceIds = rows.Select(r => r.DeviceId).ToList();
        var devices = ctx.Devices.Where(d => deviceIds.Contains(d.DeviceId)).ToDictionary(d => d.DeviceId);

        return rows.Select(row =>
            {
                devices.TryGetValue(row.DeviceId, out var device);
                return new CalendarDeviceDetail
                {
                    DeviceId = row.DeviceId,
                    DeviceName = device?.DeviceName ?? row.DeviceId,
                    DeviceType = device?.DeviceType ?? DeviceTypes.Unknown,
                    IsConnected = false,
                    SessionCount = row.SessionCount,
                    ConnectionSeconds = row.ConnectionSeconds,
                    LongestSessionSeconds = row.LongestSessionSeconds,
                    Keystrokes = row.Keystrokes,
                    MouseClicks = row.MouseClicks,
                    MouseMovementSeconds = row.MouseMovementSeconds,
                    ActiveMinutes = row.ActiveMinutes,
                    HourlyInputCount = row.HourlyInputCount.ToArray(),
                };
            })
            .OrderByDescending(r => r.ConnectionSeconds)
            .ToList()
            .AsReadOnly();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Recomputes connection stats for all (device, day) pairs that have any lifecycle events in the range.</summary>
    private void RecomputeConnectionStatsForRange(ApplicationDbContext ctx, DateOnly from, DateOnly to)
    {
        var (fromBound, toBound) = GetUtcBounds(from, to);

        var deviceDayPairs = ctx
            .DeviceEvents.Where(e =>
                e.EventTime >= fromBound
                && e.EventTime < toBound
                && e.EventType != EventTypes.AppStarted
                && e.EventType != EventTypes.AppEnded
                && e.DeviceId != ""
            )
            .Select(e => new { e.DeviceId, e.EventTime })
            .AsEnumerable()
            .Select(e => (e.DeviceId, Day: TimeFormatter.ToLocalDay(e.EventTime)))
            .Distinct()
            .ToList();

        foreach (var (deviceId, day) in deviceDayPairs)
            RecomputeDailyConnectionStats(ctx, deviceId, day);

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
    /// Overwrites connection stats (SessionCount, ConnectionSeconds, LongestSessionSeconds) for
    /// a device on a single local day by replaying all its lifecycle events for that day.
    /// If the first event in the day is a closing event (cross-midnight session), the session
    /// start is assumed to be local midnight.
    /// </summary>
    private void RecomputeDailyConnectionStats(ApplicationDbContext ctx, string deviceId, DateOnly day)
    {
        var dayStartLocal = day.ToDateTime(TimeOnly.MinValue);
        var (dayStartUtc, dayEndUtc) = GetUtcBounds(day, day);

        var dayEvents = ctx
            .DeviceEvents.Where(e => e.DeviceId == deviceId && e.EventTime >= dayStartUtc && e.EventTime < dayEndUtc)
            .OrderBy(e => e.DeviceEventId)
            .ToList();

        var sessionCount = 0;
        var totalSeconds = 0L;
        var longestSeconds = 0L;
        DateTime? currentSessionStartLocal = null;

        foreach (var evt in dayEvents)
        {
            if (evt.EventType.IsOpeningEvent())
            {
                currentSessionStartLocal = TimeFormatter.ToLocalTime(evt.EventTime);
            }
            else if (evt.EventType.IsClosingEvent())
            {
                var closeLocal = TimeFormatter.ToLocalTime(evt.EventTime);
                // No open seen today → cross-midnight session; use midnight as start.
                var sessionStart = currentSessionStartLocal ?? dayStartLocal;
                var seconds = (long)(closeLocal - sessionStart).TotalSeconds;
                if (seconds > 0)
                {
                    sessionCount++;
                    totalSeconds += seconds;
                    if (seconds > longestSeconds)
                        longestSeconds = seconds;
                }
                currentSessionStartLocal = null;
            }
        }

        var stat = GetOrCreateDailyStat(ctx, day, deviceId);
        stat.SessionCount = sessionCount;
        stat.ConnectionSeconds = totalSeconds;
        stat.LongestSessionSeconds = longestSeconds;
        stat.UpdatedAt = DateTime.UtcNow;
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

    private static CalendarDaySummary ToCalendarDaySummary(
        DateOnly day,
        IReadOnlyCollection<DailyDeviceStat>? dayRows,
        Dictionary<string, Device>? devicesById = null
    )
    {
        if (dayRows == null)
            return new CalendarDaySummary { Day = day, HasData = false };

        var tileDevices = dayRows
            .Select(r =>
            {
                Device? device = null;
                devicesById?.TryGetValue(r.DeviceId, out device);
                return new CalendarTileDevice
                {
                    DeviceId = r.DeviceId,
                    DeviceName = device?.DeviceName ?? r.DeviceId,
                    DeviceType = device?.DeviceType ?? DeviceTypes.Unknown,
                };
            })
            .OrderBy(d =>
                d.DeviceType == DeviceTypes.Keyboard ? 0
                : d.DeviceType == DeviceTypes.Mouse ? 1
                : 2
            )
            .ThenBy(d => d.DeviceName)
            .ToList();

        return new CalendarDaySummary
        {
            Day = day,
            HasData = true,
            Devices = tileDevices,
        };
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
            Log.Debug("Daily stats dispose skipped — already disposed");
            return;
        }

        _disposed = true;
        _appTimerService.MinuteTick -= OnMinuteTick;
        WriteLastCleanShutdownAt();
        Log.Debug("Daily stats disposed; LastCleanShutdownAt written");
    }
}
