using KeyPulse.Data;
using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.Tests.Infrastructure;

namespace KeyPulse.Tests.Services;

/// <summary>
/// Coverage for the daily-stats projector against a real SQLite database — connection-session replay
/// from DeviceEvents and activity aggregation from ActivitySnapshots, plus the minute projector, drift
/// recovery, calendar/earliest queries, and the one-time startup backfill.
///
/// Seeded timestamps are Local-kind within a single local day (2026-05-20, away from any DST
/// transition), so the Local↔UTC round-trip through the converters is exact in any timezone.
/// </summary>
public class DailyStatsServiceTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 5, 20);

    private readonly SqliteTestDatabase _db = new();
    private readonly AppTimerService _timer = new();
    private readonly DailyStatsService _sut;

    public DailyStatsServiceTests()
    {
        _sut = new DailyStatsService(_db.Factory, _timer);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _timer.Dispose();
        _db.Dispose();
    }

    // ── Connection stats (DeviceEvents → DailyDeviceStats) ──────────────────────

    [Fact]
    public void Recompute_SingleSession_ComputesConnectionSeconds()
    {
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(Event("DEV1", EventTypes.Connected, At(9, 0)));
            ctx.DeviceEvents.Add(Event("DEV1", EventTypes.Disconnected, At(10, 30)));
        });

        _sut.RecomputeDailyDeviceStatsForRange(Day, Day);

        var stat = _sut.GetDailyDeviceStats(Day, Day).ShouldHaveSingleItem();
        stat.DeviceId.ShouldBe("DEV1");
        stat.SessionCount.ShouldBe(1);
        stat.ConnectionSeconds.ShouldBe(5400); // 1h30m
    }

    [Fact]
    public void Recompute_MultipleSessions_SumsSeconds()
    {
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(Event("DEV1", EventTypes.Connected, At(9, 0)));
            ctx.DeviceEvents.Add(Event("DEV1", EventTypes.Disconnected, At(9, 30))); // 1800s
            ctx.DeviceEvents.Add(Event("DEV1", EventTypes.Connected, At(11, 0)));
            ctx.DeviceEvents.Add(Event("DEV1", EventTypes.Disconnected, At(13, 0))); // 7200s
        });

        _sut.RecomputeDailyDeviceStatsForRange(Day, Day);

        var stat = _sut.GetDailyDeviceStats(Day, Day).ShouldHaveSingleItem();
        stat.SessionCount.ShouldBe(2);
        stat.ConnectionSeconds.ShouldBe(9000);
    }

    [Fact]
    public void Recompute_ClosingEventWithoutOpening_AssumesMidnightStart()
    {
        // A cross-midnight session: the day only sees the closing event, so the start is local midnight.
        Seed(ctx => ctx.DeviceEvents.Add(Event("DEV1", EventTypes.Disconnected, At(1, 0))));

        _sut.RecomputeDailyDeviceStatsForRange(Day, Day);

        var stat = _sut.GetDailyDeviceStats(Day, Day).ShouldHaveSingleItem();
        stat.SessionCount.ShouldBe(1);
        stat.ConnectionSeconds.ShouldBe(3600); // midnight → 01:00
    }

    [Fact]
    public void Recompute_AppLifecycleEvents_AreIgnored()
    {
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(Event("", EventTypes.AppStarted, At(8, 0)));
            ctx.DeviceEvents.Add(Event("", EventTypes.AppEnded, At(18, 0)));
        });

        _sut.RecomputeDailyDeviceStatsForRange(Day, Day);

        _sut.GetDailyDeviceStats(Day, Day).ShouldBeEmpty();
    }

    [Fact]
    public void Recompute_MultiDayRange_CreatesRowPerDay()
    {
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Connected, Local(5, 20, 9)));
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Disconnected, Local(5, 20, 10)));
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Connected, Local(5, 22, 9)));
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Disconnected, Local(5, 22, 9, 30)));
        });

        _sut.RecomputeDailyDeviceStatsForRange(new DateOnly(2026, 5, 20), new DateOnly(2026, 5, 22));

        var stats = _sut.GetDailyDeviceStats(new DateOnly(2026, 5, 20), new DateOnly(2026, 5, 22));
        stats.Count.ShouldBe(2);
        stats.Single(s => s.Day == new DateOnly(2026, 5, 20)).ConnectionSeconds.ShouldBe(3600);
        stats.Single(s => s.Day == new DateOnly(2026, 5, 22)).ConnectionSeconds.ShouldBe(1800);
    }

    [Fact]
    public void Recompute_SessionCrossingMidnight_CreditsBothDays()
    {
        // Opens 22:00 on day 1, closes 02:00 on day 2: each day gets only its overlapping portion.
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Connected, Local(5, 20, 22)));
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Disconnected, Local(5, 21, 2)));
        });

        _sut.RecomputeDailyDeviceStatsForRange(new DateOnly(2026, 5, 20), new DateOnly(2026, 5, 21));

        var stats = _sut.GetDailyDeviceStats(new DateOnly(2026, 5, 20), new DateOnly(2026, 5, 21));
        stats.Single(s => s.Day == new DateOnly(2026, 5, 20)).ConnectionSeconds.ShouldBe(7200); // 22:00 → midnight
        stats.Single(s => s.Day == new DateOnly(2026, 5, 21)).ConnectionSeconds.ShouldBe(7200); // midnight → 02:00
        stats.ShouldAllBe(s => s.SessionCount == 1);
    }

    [Fact]
    public void Recompute_SessionSpanningFullDay_CreditsInteriorDayWithNoEvents()
    {
        // Opens day 1 10:00, closes day 3 14:00: day 2 is connected all day yet has no events of its own.
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Connected, Local(5, 20, 10)));
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Disconnected, Local(5, 22, 14)));
        });

        _sut.RecomputeDailyDeviceStatsForRange(new DateOnly(2026, 5, 20), new DateOnly(2026, 5, 22));

        var stats = _sut.GetDailyDeviceStats(new DateOnly(2026, 5, 20), new DateOnly(2026, 5, 22));
        stats.Single(s => s.Day == new DateOnly(2026, 5, 20)).ConnectionSeconds.ShouldBe(50400); // 10:00 → midnight
        var interior = stats.Single(s => s.Day == new DateOnly(2026, 5, 21));
        interior.ConnectionSeconds.ShouldBe(86400); // full day
        interior.SessionCount.ShouldBe(1);
        stats.Single(s => s.Day == new DateOnly(2026, 5, 22)).ConnectionSeconds.ShouldBe(50400); // midnight → 14:00
    }

    [Fact]
    public void Recompute_SingleDay_OpenWithoutSameDayClose_CreditsTailNotZero()
    {
        // The production bug: a day whose only event is the opening (the close lands on the next day).
        // Recomputing that day in isolation must still credit open → midnight, not 0.
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Connected, Local(5, 20, 10)));
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Disconnected, Local(5, 21, 2)));
        });

        _sut.RecomputeDailyDeviceStatsForRange(new DateOnly(2026, 5, 20), new DateOnly(2026, 5, 20));

        var stat = _sut.GetDailyDeviceStats(new DateOnly(2026, 5, 20), new DateOnly(2026, 5, 20))
            .ShouldHaveSingleItem();
        stat.SessionCount.ShouldBe(1);
        stat.ConnectionSeconds.ShouldBe(50400); // 10:00 → midnight, previously 0
    }

    // ── Activity stats (ActivitySnapshots → DailyDeviceStats) ───────────────────

    [Fact]
    public void Recompute_ActivitySnapshots_AggregateIntoDailyStats()
    {
        Seed(ctx =>
        {
            ctx.ActivitySnapshots.Add(
                Snapshot("DEV1", At(9, 5), keys: 10, clicks: 3, moveSeconds: 20, activeSeconds: 15)
            );
            ctx.ActivitySnapshots.Add(Snapshot("DEV1", At(9, 6), keys: 5, clicks: 0, moveSeconds: 0, activeSeconds: 4));
        });

        _sut.RecomputeDailyDeviceStatsForRange(Day, Day);

        var stat = _sut.GetDailyDeviceStats(Day, Day).ShouldHaveSingleItem();
        stat.Keystrokes.ShouldBe(15);
        stat.MouseClicks.ShouldBe(3);
        stat.MouseMovementSeconds.ShouldBe(20);
        stat.ActiveSeconds.ShouldBe(19); // 15 + 4, summed across minutes
        stat.HourlyInputCount[9].ShouldBe(38); // (10+3+20) + (5+0+0), bucketed by local hour
    }

    // ── Idempotency ─────────────────────────────────────────────────────────────

    [Fact]
    public void Recompute_RunTwice_IsIdempotent()
    {
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(Event("DEV1", EventTypes.Connected, At(9, 0)));
            ctx.DeviceEvents.Add(Event("DEV1", EventTypes.Disconnected, At(10, 0)));
            ctx.ActivitySnapshots.Add(
                Snapshot("DEV1", At(9, 5), keys: 7, clicks: 2, moveSeconds: 11, activeSeconds: 9)
            );
        });

        _sut.RecomputeDailyDeviceStatsForRange(Day, Day);
        var first = _sut.GetDailyDeviceStats(Day, Day).ShouldHaveSingleItem();

        _sut.RecomputeDailyDeviceStatsForRange(Day, Day);
        var stats = _sut.GetDailyDeviceStats(Day, Day);

        stats.Count.ShouldBe(1); // no duplicate row on the unique (Day, DeviceId) index
        var second = stats[0];
        second.ConnectionSeconds.ShouldBe(first.ConnectionSeconds);
        second.SessionCount.ShouldBe(first.SessionCount);
        second.Keystrokes.ShouldBe(first.Keystrokes);
        second.ActiveSeconds.ShouldBe(first.ActiveSeconds);
    }

    // ── Write-through (DataService calls ApplyDeviceEvent after an insert) ───────

    [Fact]
    public void ApplyDeviceEvent_OnClosingEvent_WritesThroughConnectionStats()
    {
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(Event("DEV1", EventTypes.Connected, At(9, 0)));
            ctx.DeviceEvents.Add(Event("DEV1", EventTypes.Disconnected, At(10, 0)));
        });

        using (var ctx = _db.CreateContext())
        {
            var closing = ctx.DeviceEvents.Single(e => e.EventType == EventTypes.Disconnected);
            _sut.ApplyDeviceEvent(ctx, closing);
        }

        var stat = _sut.GetDailyDeviceStats(Day, Day).ShouldHaveSingleItem();
        stat.SessionCount.ShouldBe(1);
        stat.ConnectionSeconds.ShouldBe(3600);
    }

    [Fact]
    public void ApplyDeviceEvent_ClosingEventCrossingMidnight_CreditsOpeningDayToo()
    {
        // Closing on day 2 must recompute the opening day as well, not only the close day.
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Connected, Local(5, 20, 22)));
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Disconnected, Local(5, 21, 2)));
        });

        using (var ctx = _db.CreateContext())
        {
            var closing = ctx.DeviceEvents.Single(e => e.EventType == EventTypes.Disconnected);
            _sut.ApplyDeviceEvent(ctx, closing);
        }

        var stats = _sut.GetDailyDeviceStats(new DateOnly(2026, 5, 20), new DateOnly(2026, 5, 21));
        stats.Single(s => s.Day == new DateOnly(2026, 5, 20)).ConnectionSeconds.ShouldBe(7200);
        stats.Single(s => s.Day == new DateOnly(2026, 5, 21)).ConnectionSeconds.ShouldBe(7200);
    }

    [Fact]
    public void ApplyDeviceEvent_OpeningEvent_NoRowCreated()
    {
        using var ctx = _db.CreateContext();
        var opening = Event("DEV1", EventTypes.Connected, At(9, 0));
        ctx.DeviceEvents.Add(opening);
        ctx.SaveChanges();

        _sut.ApplyDeviceEvent(ctx, opening);

        _sut.GetDailyDeviceStats(Day, Day).ShouldBeEmpty();
    }

    // ── Minute projector ────────────────────────────────────────────────────────

    [Fact]
    public void ProjectClosedActivityMinutes_ProjectsClosedMinute_ExactlyOnce()
    {
        var closedMinute = DateTime.Now.AddMinutes(-5).TruncateToMinute(); // local, safely in the past
        Seed(ctx =>
            ctx.ActivitySnapshots.Add(
                new ActivitySnapshot
                {
                    DeviceId = "D1",
                    Minute = closedMinute,
                    Keystrokes = 8,
                }
            )
        );
        _sut.MarkActivitySnapshotWritten(closedMinute);

        _sut.ProjectClosedActivityMinutes();
        var day = TimeFormatter.ToLocalDay(closedMinute);
        _sut.GetDailyDeviceStats(day, day).ShouldHaveSingleItem().Keystrokes.ShouldBe(8);

        // Re-mark dirty and run again — the projection checkpoint must prevent a double count.
        _sut.MarkActivitySnapshotWritten(closedMinute);
        _sut.ProjectClosedActivityMinutes();
        _sut.GetDailyDeviceStats(day, day).ShouldHaveSingleItem().Keystrokes.ShouldBe(8);
    }

    [Fact]
    public void ProjectClosedActivityMinutes_MinuteNotYetClosed_NotProjected()
    {
        // A minute in the future is never strictly before the current closed-minute boundary.
        var futureMinute = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(1).TruncateToMinute(), DateTimeKind.Utc);
        Seed(ctx =>
            ctx.ActivitySnapshots.Add(
                new ActivitySnapshot
                {
                    DeviceId = "D1",
                    Minute = futureMinute,
                    Keystrokes = 8,
                }
            )
        );
        _sut.MarkActivitySnapshotWritten(futureMinute);

        _sut.ProjectClosedActivityMinutes();

        var today = DateOnly.FromDateTime(DateTime.Now);
        _sut.GetDailyDeviceStats(today.AddDays(-2), today.AddDays(2)).ShouldBeEmpty();
    }

    // ── Drift recovery ──────────────────────────────────────────────────────────

    [Fact]
    public void ReconcileDriftedDays_MissingDayWithClosingEvent_IsHealed()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var open = today.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Local);
        var close = today.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Local);
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Connected, open));
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Disconnected, close));
        });

        _sut.ReconcileDriftedDays(today); // no DailyDeviceStat row yet => drift => heal

        _sut.GetDailyDeviceStats(today, today).ShouldContain(s => s.DeviceId == "D1");
    }

    [Fact]
    public void ReconcileDriftedDays_EmptyDb_NoThrow() =>
        Should.NotThrow(() => _sut.ReconcileDriftedDays(DateOnly.FromDateTime(DateTime.Now)));

    // ── Query helpers ───────────────────────────────────────────────────────────

    [Fact]
    public void GetVisibleDailyDeviceStats_ExcludesHiddenDevices()
    {
        Seed(ctx =>
        {
            ctx.Devices.Add(Device("DEV1", "My Keyboard", DeviceTypes.Keyboard, hidden: false));
            ctx.Devices.Add(Device("DEV2", "Hidden Mouse", DeviceTypes.Mouse, hidden: true));

            ctx.DeviceEvents.Add(Event("DEV1", EventTypes.Connected, At(9, 0)));
            ctx.DeviceEvents.Add(Event("DEV1", EventTypes.Disconnected, At(9, 10))); // 600s
            ctx.DeviceEvents.Add(Event("DEV2", EventTypes.Connected, At(9, 0)));
            ctx.DeviceEvents.Add(Event("DEV2", EventTypes.Disconnected, At(9, 30)));
        });

        _sut.RecomputeDailyDeviceStatsForRange(Day, Day);

        var row = _sut.GetVisibleDailyDeviceStats(Day, Day).ShouldHaveSingleItem(); // DEV2 is hidden => excluded
        row.DeviceId.ShouldBe("DEV1");
        row.ConnectionSeconds.ShouldBe(600);
    }

    [Fact]
    public void GetVisibleDailyDeviceStats_EmptyDay_ReturnsEmpty() =>
        _sut.GetVisibleDailyDeviceStats(Day, Day).ShouldBeEmpty();

    [Fact]
    public void GetVisibleDailyDeviceStats_NoDeviceRow_StatStillReturned()
    {
        // A stat row without a matching Devices row is still data; name fallback is presentation's job.
        Seed(ctx =>
            ctx.DailyDeviceStats.Add(
                new DailyDeviceStat
                {
                    Day = Day,
                    DeviceId = "ORPHAN",
                    ConnectionSeconds = 5,
                }
            )
        );

        _sut.GetVisibleDailyDeviceStats(Day, Day).ShouldHaveSingleItem().DeviceId.ShouldBe("ORPHAN");
    }

    [Fact]
    public void GetEarliestDataDay_Empty_ReturnsNull() => _sut.GetEarliestDataDay().ShouldBeNull();

    [Fact]
    public void GetEarliestDataDay_ExcludesHiddenDevices()
    {
        Seed(ctx =>
        {
            ctx.Devices.Add(Device("HID", "h", DeviceTypes.Keyboard, hidden: true));
            ctx.DailyDeviceStats.Add(new DailyDeviceStat { Day = new DateOnly(2026, 5, 1), DeviceId = "HID" });
            ctx.Devices.Add(Device("VIS", "v", DeviceTypes.Keyboard, hidden: false));
            ctx.DailyDeviceStats.Add(new DailyDeviceStat { Day = new DateOnly(2026, 5, 10), DeviceId = "VIS" });
        });

        _sut.GetEarliestDataDay().ShouldBe(new DateOnly(2026, 5, 10)); // hidden device's earlier day ignored
    }

    // ── One-time startup backfill (needs AppMeta) ───────────────────────────────

    [Fact]
    public void RunStartupRebuild_FirstRun_BackfillsHistoryFromEarliestSource()
    {
        _db.EnsureAppMetaTable();
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Connected, Local(5, 18, 9)));
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Disconnected, Local(5, 18, 10)));
        });

        _sut.RunStartupRebuild();

        _sut.GetDailyDeviceStats(new DateOnly(2026, 5, 18), new DateOnly(2026, 5, 18))
            .ShouldContain(s => s.DeviceId == "D1" && s.ConnectionSeconds == 3600);
    }

    [Fact]
    public void RunStartupRebuild_ConnectionSpanRecompute_CreditsCrossMidnightWithoutErasingPrunedActivity()
    {
        _db.EnsureAppMetaTable();
        var openDay = new DateOnly(2026, 5, 20);

        Seed(ctx =>
        {
            // Pre-aggregated activity for a day whose minute snapshots retention has already pruned.
            ctx.DailyDeviceStats.Add(
                new DailyDeviceStat
                {
                    Day = openDay,
                    DeviceId = "D1",
                    Keystrokes = 500,
                    ActiveSeconds = 1800,
                }
            );
            // A session crossing midnight: the old logic left the opening day at 0 connected seconds.
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Connected, Local(5, 20, 10)));
            ctx.DeviceEvents.Add(Event("D1", EventTypes.Disconnected, Local(5, 21, 2)));
        });

        // The existing-install path: initial backfill already done, connection-span recompute not yet run.
        using (var ctx = _db.CreateContext())
            AppMetaStore.WriteUtc(ctx, DailyStatsService.FULL_BACKFILL_META_KEY, DateTime.UtcNow);

        _sut.RunStartupRebuild();

        var stat = _sut.GetDailyDeviceStats(openDay, openDay).ShouldHaveSingleItem();
        stat.ConnectionSeconds.ShouldBe(50400); // 10:00 → midnight now credited (was 0)
        stat.SessionCount.ShouldBe(1);
        stat.Keystrokes.ShouldBe(500); // pruned-day activity preserved, not recomputed to 0
        stat.ActiveSeconds.ShouldBe(1800);
    }

    // ── Seeding helpers ─────────────────────────────────────────────────────────

    private void Seed(Action<ApplicationDbContext> seed)
    {
        using var ctx = _db.CreateContext();
        seed(ctx);
        ctx.SaveChanges();
    }

    /// <summary>A Local-kind timestamp on the fixture's default test day (2026-05-20).</summary>
    private static DateTime At(int hour, int minute, int second = 0) =>
        new(2026, 5, 20, hour, minute, second, DateTimeKind.Local);

    /// <summary>A Local-kind timestamp on an arbitrary day in 2026.</summary>
    private static DateTime Local(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Local);

    private static DeviceEvent Event(string deviceId, EventTypes type, DateTime localTime) =>
        new()
        {
            DeviceId = deviceId,
            EventType = type,
            EventTime = localTime,
        };

    private static ActivitySnapshot Snapshot(
        string deviceId,
        DateTime localMinute,
        int keys,
        int clicks,
        byte moveSeconds,
        byte activeSeconds = 0
    ) =>
        new()
        {
            DeviceId = deviceId,
            Minute = localMinute,
            Keystrokes = keys,
            MouseClicks = clicks,
            MouseMovementSeconds = moveSeconds,
            ActiveSeconds = activeSeconds,
        };

    private static Device Device(string deviceId, string name, DeviceTypes type, bool hidden) =>
        new()
        {
            DeviceId = deviceId,
            DeviceName = name,
            DeviceType = type,
            IsHiddenFromDisplay = hidden,
        };
}
