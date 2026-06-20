using KeyPulse.Data;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.Tests.Infrastructure;

namespace KeyPulse.Tests.Services;

/// <summary>
/// The snapshot rebuild recomputes lifetime input totals from daily aggregates plus
/// not-yet-projected snapshots, so the totals survive retention pruning of old snapshots.
/// Connection seconds intentionally still replay the full event log, which is never pruned.
/// </summary>
public class DeviceSnapshotRebuildTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 5, 20);

    private readonly SqliteTestDatabase _db = new();
    private readonly AppTimerService _timer = new();
    private readonly DailyStatsService _dailyStats;
    private readonly DataService _sut;

    public DeviceSnapshotRebuildTests()
    {
        _dailyStats = new DailyStatsService(_db.Factory, _timer);
        _sut = new DataService(_db.Factory, _dailyStats);
    }

    public void Dispose()
    {
        _dailyStats.Dispose();
        _timer.Dispose();
        _db.Dispose();
    }

    [Fact]
    public void Rebuild_AfterProjection_MatchesDirectSnapshotSum()
    {
        Seed(ctx =>
        {
            ctx.Devices.Add(Device("D1"));
            ctx.ActivitySnapshots.Add(Snapshot("D1", At(9, 5), keys: 10, clicks: 3, moveSeconds: 20));
            ctx.ActivitySnapshots.Add(Snapshot("D1", At(9, 6), keys: 5, clicks: 1, moveSeconds: 0));
        });
        _dailyStats.RecomputeDailyDeviceStatsForRange(Day, Day); // projects + records checkpoints

        _sut.RebuildDeviceSnapshots();

        GetDevice("D1").TotalInputCount.ShouldBe(39); // (10+3+20) + (5+1+0)
    }

    [Fact]
    public void Rebuild_NoProjectionsYet_CountsSnapshotsDirectly()
    {
        // Pre-backfill state: snapshots exist but nothing was ever projected.
        Seed(ctx =>
        {
            ctx.Devices.Add(Device("D1"));
            ctx.ActivitySnapshots.Add(Snapshot("D1", At(9, 5), keys: 7, clicks: 2, moveSeconds: 11));
        });

        _sut.RebuildDeviceSnapshots();

        GetDevice("D1").TotalInputCount.ShouldBe(20);
    }

    [Fact]
    public void Rebuild_MixedProjectedAndUnprojected_NoDoubleCount()
    {
        Seed(ctx =>
        {
            ctx.Devices.Add(Device("D1"));
            ctx.ActivitySnapshots.Add(Snapshot("D1", At(9, 5), keys: 10, clicks: 0, moveSeconds: 0));
        });
        _dailyStats.RecomputeDailyDeviceStatsForRange(Day, Day); // first minute projected

        Seed(ctx => ctx.ActivitySnapshots.Add(Snapshot("D1", At(9, 6), keys: 4, clicks: 0, moveSeconds: 0)));

        _sut.RebuildDeviceSnapshots();

        GetDevice("D1").TotalInputCount.ShouldBe(14); // projected 10 + unprojected 4, neither counted twice
    }

    [Fact]
    public void Rebuild_AfterPruningProjectedSnapshots_TotalUnchanged()
    {
        Seed(ctx =>
        {
            ctx.Devices.Add(Device("D1"));
            ctx.ActivitySnapshots.Add(Snapshot("D1", At(9, 5), keys: 10, clicks: 3, moveSeconds: 20));
            ctx.ActivitySnapshots.Add(Snapshot("D1", At(9, 6), keys: 5, clicks: 1, moveSeconds: 0));
        });
        _dailyStats.RecomputeDailyDeviceStatsForRange(Day, Day);

        // Retention prune: drop the raw rows and their checkpoints; daily aggregates remain.
        Seed(ctx =>
        {
            ctx.ActivitySnapshots.RemoveRange(ctx.ActivitySnapshots);
            ctx.ActivityProjections.RemoveRange(ctx.ActivityProjections);
        });

        _sut.RebuildDeviceSnapshots();

        GetDevice("D1").TotalInputCount.ShouldBe(39);
    }

    [Fact]
    public void Rebuild_CountsDistinctConnectedDays()
    {
        Seed(ctx =>
        {
            ctx.Devices.Add(Device("D1"));
            ctx.DailyDeviceStats.Add(Stat("D1", new DateOnly(2026, 5, 20), connectionSeconds: 100));
            ctx.DailyDeviceStats.Add(Stat("D1", new DateOnly(2026, 5, 21), connectionSeconds: 50));
            ctx.DailyDeviceStats.Add(Stat("D1", new DateOnly(2026, 5, 22), connectionSeconds: 0)); // never connected => excluded
        });

        _sut.RebuildDeviceSnapshots();

        GetDevice("D1").DaysConnected.ShouldBe(2);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private void Seed(Action<ApplicationDbContext> seed)
    {
        using var ctx = _db.CreateContext();
        seed(ctx);
        ctx.SaveChanges();
    }

    private Device GetDevice(string deviceId)
    {
        using var ctx = _db.CreateContext();
        return ctx.Devices.Single(d => d.DeviceId == deviceId);
    }

    private static DateTime At(int hour, int minute) => new(2026, 5, 20, hour, minute, 0, DateTimeKind.Local);

    private static Device Device(string deviceId) =>
        new()
        {
            DeviceId = deviceId,
            DeviceName = $"Device {deviceId}",
            DeviceType = DeviceTypes.Keyboard,
        };

    private static DailyDeviceStat Stat(string deviceId, DateOnly day, long connectionSeconds) =>
        new()
        {
            DeviceId = deviceId,
            Day = day,
            ConnectionSeconds = connectionSeconds,
            UpdatedAt = DateTime.UtcNow,
        };

    private static ActivitySnapshot Snapshot(
        string deviceId,
        DateTime localMinute,
        int keys,
        int clicks,
        byte moveSeconds
    ) =>
        new()
        {
            DeviceId = deviceId,
            Minute = localMinute,
            Keystrokes = keys,
            MouseClicks = clicks,
            MouseMovementSeconds = moveSeconds,
        };
}
