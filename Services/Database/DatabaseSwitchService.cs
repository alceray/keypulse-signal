using KeyPulse.Configuration;
using KeyPulse.Data;
using KeyPulse.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace KeyPulse.Services;

/// <summary>Completes provider changes while monitoring is stopped during application startup.</summary>
public sealed class DatabaseSwitchService(AppSettingsService settingsService, IDatabaseCredentialStore credentialStore)
{
    private const int BatchSize = 1000;
    private const string ImportSwitchMetaKey = "DatabaseImportSwitchId";

    public async Task<bool> ProcessPendingSwitchAsync(CancellationToken cancellationToken = default)
    {
        var settings = settingsService.GetSettings();
        if (!settings.PendingDatabaseProvider.HasValue)
            return false;

        if (settings.PendingDatabaseProvider == DatabaseProvider.Sqlite)
        {
            settings.DatabaseProvider = DatabaseProvider.Sqlite;
            ClearPending(settings);
            settingsService.SaveSettings(settings);
            Log.Information("Database provider switched to SQLite");
            return true;
        }

        var password = credentialStore.ReadPostgreSqlPassword();
        if (password == null)
            throw new InvalidOperationException("The saved PostgreSQL password is unavailable");

        settings.PendingDatabaseSwitchId ??= Guid.NewGuid().ToString("N");
        settingsService.SaveSettings(settings);

        await DatabaseConfigurationService.TestPostgreSqlAsync(settings.PostgreSql, password, cancellationToken);
        await using var target = ConfiguredDbContextFactory.CreatePostgreSqlContext(settings.PostgreSql, password);
        await target.Database.MigrateAsync(cancellationToken);
        AppMetaStore.EnsureTable(target);

        if (await HasApplicationDataAsync(target, cancellationToken))
        {
            var targetMeta = AppMetaStore.ReadAll(target);
            if (
                !targetMeta.TryGetValue(ImportSwitchMetaKey, out var completedSwitchId)
                || !string.Equals(completedSwitchId, settings.PendingDatabaseSwitchId, StringComparison.Ordinal)
            )
                throw new InvalidOperationException("The PostgreSQL database already contains KeyPulse data");

            // The import committed but activating settings did not. Complete that activation without copying again.
            settings.DatabaseProvider = DatabaseProvider.PostgreSql;
            ClearPending(settings);
            settingsService.SaveSettings(settings);
            return true;
        }

        if (settings.PendingDatabaseImport)
            await ImportSqliteHistoryAsync(target, settings.PendingDatabaseSwitchId, cancellationToken);

        settings.DatabaseProvider = DatabaseProvider.PostgreSql;
        ClearPending(settings);
        settingsService.SaveSettings(settings);
        Log.Information("Database provider switched to PostgreSQL");
        return true;
    }

    private static void ClearPending(AppUserSettings settings)
    {
        settings.PendingDatabaseProvider = null;
        settings.PendingDatabaseImport = false;
        settings.PendingDatabaseSwitchId = null;
    }

    private static async Task ImportSqliteHistoryAsync(
        PostgreSqlApplicationDbContext target,
        string switchId,
        CancellationToken cancellationToken
    )
    {
        var sourcePath = AppDataPaths.GetPath(AppConstants.Paths.DatabaseFileName);
        using var source = ConfiguredDbContextFactory.CreateSqliteContext(sourcePath);
        await source.Database.MigrateAsync(cancellationToken);
        DatabaseMigrations.RunAll(source);
        AppMetaStore.EnsureTable(source);

        var expected = await ReadCountsAsync(source, cancellationToken);
        var sourceMeta = AppMetaStore.ReadAll(source);

        await using var transaction = await target.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await CopyDevicesAsync(source, target, cancellationToken);
            await CopyDeviceEventsAsync(source, target, cancellationToken);
            await CopyActivitySnapshotsAsync(source, target, cancellationToken);
            await CopyDailyStatsAsync(source, target, cancellationToken);
            await CopyActivityProjectionsAsync(source, target, cancellationToken);

            foreach (var (key, value) in sourceMeta)
                AppMetaStore.Write(target, key, value);
            AppMetaStore.Write(target, ImportSwitchMetaKey, switchId);

            var actual = await ReadCountsAsync(target, cancellationToken);
            if (actual != expected)
                throw new InvalidOperationException(
                    $"Database import verification failed. Expected {expected}; imported {actual}."
                );

            var expectedInput = await ReadInputTotalAsync(source, cancellationToken);
            var actualInput = await ReadInputTotalAsync(target, cancellationToken);
            if (actualInput != expectedInput)
                throw new InvalidOperationException("Database import aggregate verification failed");

            await transaction.CommitAsync(cancellationToken);
            Log.Information("SQLite history imported into PostgreSQL: {Counts}", actual);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task CopyDevicesAsync(
        ApplicationDbContext source,
        ApplicationDbContext target,
        CancellationToken cancellationToken
    )
    {
        var rows = await source.Devices.AsNoTracking().OrderBy(x => x.DeviceId).ToListAsync(cancellationToken);
        foreach (var batch in rows.Chunk(BatchSize))
        {
            target.Devices.AddRange(
                batch.Select(x => new Device
                {
                    DeviceId = x.DeviceId,
                    DeviceName = x.DeviceName,
                    DeviceType = x.DeviceType,
                    TotalConnectionSeconds = x.TotalConnectionSeconds,
                    SessionStartedAt = null,
                    LastConnectedAt = x.LastConnectedAt,
                    LastSeenAt = x.LastSeenAt,
                    IsHiddenFromDisplay = x.IsHiddenFromDisplay,
                    TotalInputCount = x.TotalInputCount,
                    DaysConnected = x.DaysConnected,
                })
            );
            await target.SaveChangesAsync(cancellationToken);
            target.ChangeTracker.Clear();
        }
    }

    private static async Task CopyDeviceEventsAsync(
        ApplicationDbContext source,
        ApplicationDbContext target,
        CancellationToken cancellationToken
    ) =>
        await CopyInBatchesAsync(
            source.DeviceEvents.AsNoTracking().OrderBy(x => x.DeviceEventId),
            batch =>
                target.DeviceEvents.AddRange(
                    batch.Select(x => new DeviceEvent
                    {
                        DeviceId = x.DeviceId,
                        EventTime = x.EventTime,
                        EventType = x.EventType,
                    })
                ),
            target,
            cancellationToken
        );

    private static async Task CopyActivitySnapshotsAsync(
        ApplicationDbContext source,
        ApplicationDbContext target,
        CancellationToken cancellationToken
    ) =>
        await CopyInBatchesAsync(
            source.ActivitySnapshots.AsNoTracking().OrderBy(x => x.ActivitySnapshotId),
            batch =>
                target.ActivitySnapshots.AddRange(
                    batch.Select(x => new ActivitySnapshot
                    {
                        DeviceId = x.DeviceId,
                        Minute = x.Minute,
                        Keystrokes = x.Keystrokes,
                        MouseClicks = x.MouseClicks,
                        MouseMovementSeconds = x.MouseMovementSeconds,
                        ActiveSeconds = x.ActiveSeconds,
                    })
                ),
            target,
            cancellationToken
        );

    private static async Task CopyDailyStatsAsync(
        ApplicationDbContext source,
        ApplicationDbContext target,
        CancellationToken cancellationToken
    ) =>
        await CopyInBatchesAsync(
            source.DailyDeviceStats.AsNoTracking().OrderBy(x => x.DailyDeviceStatId),
            batch =>
                target.DailyDeviceStats.AddRange(
                    batch.Select(x => new DailyDeviceStat
                    {
                        Day = x.Day,
                        DeviceId = x.DeviceId,
                        SessionCount = x.SessionCount,
                        ConnectionSeconds = x.ConnectionSeconds,
                        Keystrokes = x.Keystrokes,
                        MouseClicks = x.MouseClicks,
                        MouseMovementSeconds = x.MouseMovementSeconds,
                        ActiveSeconds = x.ActiveSeconds,
                        HourlyInputCount = x.HourlyInputCount.ToArray(),
                        UpdatedAt = x.UpdatedAt,
                    })
                ),
            target,
            cancellationToken
        );

    private static async Task CopyActivityProjectionsAsync(
        ApplicationDbContext source,
        ApplicationDbContext target,
        CancellationToken cancellationToken
    ) =>
        await CopyInBatchesAsync(
            source.ActivityProjections.AsNoTracking().OrderBy(x => x.ActivityProjectionId),
            batch =>
                target.ActivityProjections.AddRange(
                    batch.Select(x => new ActivityProjection
                    {
                        DeviceId = x.DeviceId,
                        Minute = x.Minute,
                        ProjectedAt = x.ProjectedAt,
                    })
                ),
            target,
            cancellationToken
        );

    private static async Task CopyInBatchesAsync<T>(
        IOrderedQueryable<T> query,
        Action<IReadOnlyList<T>> addBatch,
        ApplicationDbContext target,
        CancellationToken cancellationToken
    )
        where T : class
    {
        var offset = 0;
        while (true)
        {
            var batch = await query.Skip(offset).Take(BatchSize).ToListAsync(cancellationToken);
            if (batch.Count == 0)
                return;
            addBatch(batch);
            await target.SaveChangesAsync(cancellationToken);
            target.ChangeTracker.Clear();
            offset += batch.Count;
        }
    }

    private static async Task<bool> HasApplicationDataAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken
    ) =>
        await context.Devices.AnyAsync(cancellationToken)
        || await context.DeviceEvents.AnyAsync(cancellationToken)
        || await context.ActivitySnapshots.AnyAsync(cancellationToken)
        || await context.DailyDeviceStats.AnyAsync(cancellationToken)
        || await context.ActivityProjections.AnyAsync(cancellationToken);

    private static async Task<DatabaseCounts> ReadCountsAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken
    ) =>
        new(
            await context.Devices.CountAsync(cancellationToken),
            await context.DeviceEvents.CountAsync(cancellationToken),
            await context.ActivitySnapshots.CountAsync(cancellationToken),
            await context.DailyDeviceStats.CountAsync(cancellationToken),
            await context.ActivityProjections.CountAsync(cancellationToken)
        );

    private static async Task<long> ReadInputTotalAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken
    )
    {
        var snapshotTotal = await context.ActivitySnapshots.SumAsync(
            x => (long)x.Keystrokes + x.MouseClicks + x.MouseMovementSeconds,
            cancellationToken
        );
        var dailyTotal = await context.DailyDeviceStats.SumAsync(
            x => x.Keystrokes + x.MouseClicks + x.MouseMovementSeconds,
            cancellationToken
        );
        return snapshotTotal + dailyTotal;
    }

    private readonly record struct DatabaseCounts(
        int Devices,
        int Events,
        int Snapshots,
        int DailyStats,
        int Projections
    );
}
