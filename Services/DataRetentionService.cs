using KeyPulse.Data;
using KeyPulse.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace KeyPulse.Services;

/// <summary>
/// Prunes per-minute activity detail older than the user's retention window. Daily summaries and
/// the lifecycle event log are always kept, so calendar history and connection totals survive
/// pruning. Runs after the startup daily-stats rebuild, on the daily tick, and when the setting
/// is tightened.
/// </summary>
public sealed class DataRetentionService : IDisposable
{
    // Retention windows shorter than this are clamped up, keeping the prune cutoff strictly clear
    // of the current-month drift-recovery pass.
    internal const int MinimumRetentionMonths = 3;

    // Days of snapshots deleted per transaction; keeps each write lock short under WAL.
    private const int PruneChunkDays = 7;

    // A prune this large (roughly a month of single-device minutes) leaves enough free pages to be
    // worth compacting; steady-state daily prunes stay far below it.
    private const int VacuumRowThreshold = 25_000;

    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly AppSettingsService _appSettingsService;
    private readonly DailyStatsService _dailyStatsService;
    private readonly AppTimerService _appTimerService;
    private int _pruneInFlight;
    private int _lastSeenRetentionMonths;
    private bool _disposed;

    public DataRetentionService(
        IDbContextFactory<ApplicationDbContext> factory,
        AppSettingsService appSettingsService,
        DailyStatsService dailyStatsService,
        AppTimerService appTimerService
    )
    {
        _factory = factory;
        _appSettingsService = appSettingsService;
        _dailyStatsService = dailyStatsService;
        _appTimerService = appTimerService;

        _lastSeenRetentionMonths = appSettingsService.GetSettings().ActivityRetentionMonths;
        _appTimerService.DailyTick += OnDailyTick;
        _appSettingsService.SettingsChanged += OnSettingsChanged;
    }

    /// <summary>
    /// Startup entry point; call only after the daily-stats startup rebuild so a first-run
    /// backfill never recomputes days whose source minutes were already pruned.
    /// </summary>
    public void RunStartupPrune()
    {
        try
        {
            PruneNow();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Activity retention prune failed");
        }
    }

    private void OnDailyTick(object? sender, EventArgs e)
    {
        if (_disposed)
            return;

        _ = Task.Run(RunStartupPrune);
    }

    private void OnSettingsChanged(AppUserSettings settings)
    {
        var months = settings.ActivityRetentionMonths;
        if (months == _lastSeenRetentionMonths)
            return;

        _lastSeenRetentionMonths = months;
        if (_disposed || months <= 0)
            return;

        // Tightening (or first enabling) retention takes effect immediately, off the UI thread.
        _ = Task.Run(RunStartupPrune);
    }

    /// <summary>Applies the current retention setting; returns the number of rows deleted.</summary>
    internal int PruneNow()
    {
        if (_disposed)
            return 0;

        var cutoffDay = GetCutoffDay(
            _appSettingsService.GetSettings().ActivityRetentionMonths,
            DateOnly.FromDateTime(DateTime.Now)
        );
        if (cutoffDay == null)
            return 0;

        // Overlapping triggers (startup + daily tick + settings change) collapse into one run.
        if (Interlocked.CompareExchange(ref _pruneInFlight, 1, 0) != 0)
            return 0;

        try
        {
            using (var ctx = _factory.CreateDbContext())
            {
                if (AppMetaStore.ReadUtc(ctx, DailyStatsService.FULL_BACKFILL_META_KEY) == null)
                {
                    Log.Information("Activity prune skipped because daily summaries are not built yet");
                    return 0;
                }
            }

            // Drain every closed minute into the daily summaries first, so pruned detail is
            // always represented there before its source rows disappear.
            _dailyStatsService.ProjectClosedActivityMinutes();

            var deleted = PruneActivityOlderThan(_factory, cutoffDay.Value);

            if (deleted > VacuumRowThreshold)
                CompactDatabase(deleted);

            return deleted;
        }
        finally
        {
            Interlocked.Exchange(ref _pruneInFlight, 0);
        }
    }

    /// <summary>
    /// Deletes activity snapshots and their projection checkpoints strictly before the cutoff day,
    /// in week-sized chunks so each write transaction stays short. Snapshots go first: orphan
    /// checkpoints are harmless, while orphan snapshots would be re-projected and double-counted.
    /// Comparisons stay in Local-kind time to match how the EF converter materializes minutes.
    /// </summary>
    internal static int PruneActivityOlderThan(IDbContextFactory<ApplicationDbContext> factory, DateOnly cutoffDay)
    {
        var cutoffLocal = cutoffDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var totalDeleted = 0;

        using var ctx = factory.CreateDbContext();

        var oldestSnapshot = ctx
            .ActivitySnapshots.Where(s => s.Minute < cutoffLocal)
            .OrderBy(s => s.Minute)
            .Select(s => (DateTime?)s.Minute)
            .FirstOrDefault();
        var oldestProjection = ctx
            .ActivityProjections.Where(p => p.Minute < cutoffLocal)
            .OrderBy(p => p.Minute)
            .Select(p => (DateTime?)p.Minute)
            .FirstOrDefault();

        var cursor = Min(oldestSnapshot, oldestProjection);
        if (cursor == null)
            return 0;

        while (cursor < cutoffLocal)
        {
            var chunkEnd = cursor.Value.AddDays(PruneChunkDays);
            if (chunkEnd > cutoffLocal)
                chunkEnd = cutoffLocal;

            var from = cursor.Value;
            totalDeleted += ctx.ActivitySnapshots.Where(s => s.Minute >= from && s.Minute < chunkEnd).ExecuteDelete();
            totalDeleted += ctx.ActivityProjections.Where(p => p.Minute >= from && p.Minute < chunkEnd).ExecuteDelete();

            cursor = chunkEnd;
        }

        if (totalDeleted > 0)
            Log.Information(
                "Pruned {RowCount} detailed activity rows older than {CutoffDay}",
                totalDeleted,
                cutoffDay.ToString("yyyy-MM-dd")
            );

        return totalDeleted;
    }

    /// <summary>
    /// Maps the retention setting to the first local day to keep, or null when retention is disabled.
    /// Values below the minimum are clamped up rather than trusted.
    /// </summary>
    internal static DateOnly? GetCutoffDay(int retentionMonths, DateOnly today)
    {
        if (retentionMonths <= 0)
            return null;

        return today.AddMonths(-Math.Max(retentionMonths, MinimumRetentionMonths));
    }

    private void CompactDatabase(int deletedRows)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            using var ctx = _factory.CreateDbContext();
            ctx.Database.ExecuteSqlRaw("VACUUM;");
            stopwatch.Stop();
            Log.Information(
                "Database compacted in {ElapsedMs}ms after pruning {RowCount} rows",
                stopwatch.ElapsedMilliseconds,
                deletedRows
            );
        }
        catch (Exception ex)
        {
            // Non-fatal: the freed pages are still reused by future writes.
            Log.Warning(ex, "Database compaction after pruning failed");
        }
    }

    private static DateTime? Min(DateTime? a, DateTime? b)
    {
        if (a == null)
            return b;
        if (b == null)
            return a;
        return a < b ? a : b;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            Log.Debug("Data retention dispose skipped because it was already disposed");
            return;
        }

        _disposed = true;
        _appTimerService.DailyTick -= OnDailyTick;
        _appSettingsService.SettingsChanged -= OnSettingsChanged;
    }
}
