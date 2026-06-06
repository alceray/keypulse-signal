using KeyPulse.Data;
using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.Tests.Infrastructure;

namespace KeyPulse.Tests.Services;

/// <summary>
/// Retention cutoff math and the prune core. The orchestration around them (settings/backfill-marker
/// guards, daily-tick and settings-change triggers, VACUUM) composes these with already-tested pieces
/// and is verified manually.
/// </summary>
public class DataRetentionServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 6, 5);

    private readonly SqliteTestDatabase _db = new();
    private readonly AppTimerService _timer = new();
    private readonly DailyStatsService _dailyStats;

    public DataRetentionServiceTests()
    {
        _dailyStats = new DailyStatsService(_db.Factory, _timer);
    }

    public void Dispose()
    {
        _dailyStats.Dispose();
        _timer.Dispose();
        _db.Dispose();
    }

    // ── GetCutoffDay ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void GetCutoffDay_RetentionDisabled_ReturnsNull(int months) =>
        DataRetentionService.GetCutoffDay(months, Today).ShouldBeNull();

    [Theory]
    [InlineData(3, 2026, 3, 5)]
    [InlineData(6, 2025, 12, 5)]
    [InlineData(12, 2025, 6, 5)]
    public void GetCutoffDay_SubtractsMonths(int months, int year, int month, int day) =>
        DataRetentionService.GetCutoffDay(months, Today).ShouldBe(new DateOnly(year, month, day));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GetCutoffDay_BelowMinimum_ClampsToThreeMonths(int months) =>
        DataRetentionService.GetCutoffDay(months, Today).ShouldBe(new DateOnly(2026, 3, 5));

    [Fact]
    public void GetCutoffDay_MonthEnd_ClampsToValidDay() =>
        // May 31 − 3 months → Feb 28 (2026 is not a leap year).
        DataRetentionService.GetCutoffDay(3, new DateOnly(2026, 5, 31)).ShouldBe(new DateOnly(2026, 2, 28));

    // ── PruneActivityOlderThan ──────────────────────────────────────────────────

    [Fact]
    public void Prune_DeletesOldRows_KeepsCutoffDayAndNewer()
    {
        var cutoff = new DateOnly(2026, 3, 1);
        Seed(ctx =>
        {
            ctx.ActivitySnapshots.Add(Snapshot("D1", Local(2026, 1, 10, 9, 0), keys: 5));
            ctx.ActivityProjections.Add(Projection("D1", Local(2026, 1, 10, 9, 0)));
            // Boundary: first minute of the cutoff day must survive (cutoff is exclusive below).
            ctx.ActivitySnapshots.Add(Snapshot("D1", Local(2026, 3, 1, 0, 0), keys: 7));
            ctx.ActivityProjections.Add(Projection("D1", Local(2026, 3, 1, 0, 0)));
            ctx.ActivitySnapshots.Add(Snapshot("D1", Local(2026, 5, 20, 9, 0), keys: 9));
        });

        var deleted = DataRetentionService.PruneActivityOlderThan(_db.Factory, cutoff);

        deleted.ShouldBe(2); // one snapshot + one projection
        using var ctx = _db.CreateContext();
        ctx.ActivitySnapshots.Count().ShouldBe(2);
        ctx.ActivitySnapshots.Select(s => s.Keystrokes).OrderBy(k => k).ShouldBe([7, 9]);
        ctx.ActivityProjections.Count().ShouldBe(1);
    }

    [Fact]
    public void Prune_LeavesDailyStatsAndDeviceEventsUntouched()
    {
        var cutoff = new DateOnly(2026, 3, 1);
        Seed(ctx =>
        {
            ctx.ActivitySnapshots.Add(Snapshot("D1", Local(2026, 1, 10, 9, 0), keys: 5));
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "D1",
                    EventType = EventTypes.Connected,
                    EventTime = Local(2026, 1, 10, 8, 0),
                }
            );
            ctx.DailyDeviceStats.Add(
                new DailyDeviceStat
                {
                    Day = new DateOnly(2026, 1, 10),
                    DeviceId = "D1",
                    Keystrokes = 5,
                    UpdatedAt = DateTime.UtcNow,
                }
            );
        });

        DataRetentionService.PruneActivityOlderThan(_db.Factory, cutoff);

        using var ctx = _db.CreateContext();
        ctx.ActivitySnapshots.Count().ShouldBe(0);
        ctx.DeviceEvents.Count().ShouldBe(1);
        ctx.DailyDeviceStats.Count().ShouldBe(1);
    }

    [Fact]
    public void Prune_RemovesOrphanProjections()
    {
        // A checkpoint whose snapshot is already gone (e.g. an interrupted earlier prune).
        Seed(ctx => ctx.ActivityProjections.Add(Projection("D1", Local(2026, 1, 10, 9, 0))));

        var deleted = DataRetentionService.PruneActivityOlderThan(_db.Factory, new DateOnly(2026, 3, 1));

        deleted.ShouldBe(1);
        using var ctx = _db.CreateContext();
        ctx.ActivityProjections.Count().ShouldBe(0);
    }

    [Fact]
    public void Prune_SecondRun_DeletesNothing()
    {
        var cutoff = new DateOnly(2026, 3, 1);
        Seed(ctx =>
        {
            ctx.ActivitySnapshots.Add(Snapshot("D1", Local(2026, 1, 10, 9, 0), keys: 5));
            ctx.ActivityProjections.Add(Projection("D1", Local(2026, 1, 10, 9, 0)));
        });

        DataRetentionService.PruneActivityOlderThan(_db.Factory, cutoff).ShouldBe(2);
        DataRetentionService.PruneActivityOlderThan(_db.Factory, cutoff).ShouldBe(0);
    }

    [Fact]
    public void Prune_SpansManyChunks_DeletesAllOldRows()
    {
        // Snapshots spread over ~5 weeks force multiple 7-day delete windows.
        Seed(ctx =>
        {
            var start = Local(2026, 1, 1, 9, 0);
            for (var offset = 0; offset < 35; offset += 3)
                ctx.ActivitySnapshots.Add(Snapshot("D1", start.AddDays(offset), keys: offset + 1));
        });

        var deleted = DataRetentionService.PruneActivityOlderThan(_db.Factory, new DateOnly(2026, 3, 1));

        deleted.ShouldBe(12);
        using var ctx = _db.CreateContext();
        ctx.ActivitySnapshots.Count().ShouldBe(0);
    }

    [Fact]
    public void ProjectThenPrune_OldUnprojectedMinutes_LandInDailyStatsBeforeDeletion()
    {
        // The PruneNow invariant: draining the projector first means pruned detail is always
        // represented in the daily summaries, even for minutes that were never projected.
        var minute = Local(2026, 1, 10, 9, 0);
        Seed(ctx => ctx.ActivitySnapshots.Add(Snapshot("D1", minute, keys: 8)));

        _dailyStats.ProjectClosedActivityMinutes(); // initial dirty flag covers seeded rows
        DataRetentionService.PruneActivityOlderThan(_db.Factory, new DateOnly(2026, 3, 1));

        using var ctx = _db.CreateContext();
        ctx.ActivitySnapshots.Count().ShouldBe(0);
        var day = TimeFormatter.ToLocalDay(minute);
        var stat = ctx.DailyDeviceStats.Single(d => d.Day == day && d.DeviceId == "D1");
        stat.Keystrokes.ShouldBe(8);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private void Seed(Action<ApplicationDbContext> seed)
    {
        using var ctx = _db.CreateContext();
        seed(ctx);
        ctx.SaveChanges();
    }

    private static DateTime Local(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Local);

    private static ActivitySnapshot Snapshot(string deviceId, DateTime localMinute, int keys) =>
        new()
        {
            DeviceId = deviceId,
            Minute = localMinute,
            Keystrokes = keys,
        };

    private static ActivityProjection Projection(string deviceId, DateTime localMinute) =>
        new()
        {
            DeviceId = deviceId,
            Minute = localMinute,
            ProjectedAt = DateTime.UtcNow,
        };
}
