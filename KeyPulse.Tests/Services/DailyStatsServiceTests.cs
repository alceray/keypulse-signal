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
        stat.LongestSessionSeconds.ShouldBe(5400);
    }

    [Fact]
    public void Recompute_MultipleSessions_SumsSecondsAndTracksLongest()
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
        stat.LongestSessionSeconds.ShouldBe(7200);
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

    // ── Activity stats (ActivitySnapshots → DailyDeviceStats) ───────────────────

    [Fact]
    public void Recompute_ActivitySnapshots_AggregateIntoDailyStats()
    {
        Seed(ctx =>
        {
            ctx.ActivitySnapshots.Add(Snapshot("DEV1", At(9, 5), keys: 10, clicks: 3, moveSeconds: 20));
            ctx.ActivitySnapshots.Add(Snapshot("DEV1", At(9, 6), keys: 5, clicks: 0, moveSeconds: 0));
        });

        _sut.RecomputeDailyDeviceStatsForRange(Day, Day);

        var stat = _sut.GetDailyDeviceStats(Day, Day).ShouldHaveSingleItem();
        stat.Keystrokes.ShouldBe(15);
        stat.MouseClicks.ShouldBe(3);
        stat.MouseMovementSeconds.ShouldBe(20);
        stat.ActiveMinutes.ShouldBe(2);
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
            ctx.ActivitySnapshots.Add(Snapshot("DEV1", At(9, 5), keys: 7, clicks: 2, moveSeconds: 11));
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
        second.ActiveMinutes.ShouldBe(first.ActiveMinutes);
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
    public void GetCalendarDayDetail_ResolvesDeviceMetadata_AndExcludesHiddenDevices()
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

        var row = _sut.GetCalendarDayDetail(Day).ShouldHaveSingleItem(); // DEV2 is hidden => excluded
        row.DeviceId.ShouldBe("DEV1");
        row.DeviceName.ShouldBe("My Keyboard");
        row.DeviceType.ShouldBe(DeviceTypes.Keyboard);
        row.ConnectionSeconds.ShouldBe(600);
    }

    [Fact]
    public void GetCalendarDayDetail_EmptyDay_ReturnsEmpty() => _sut.GetCalendarDayDetail(Day).ShouldBeEmpty();

    [Fact]
    public void GetCalendarDayDetail_NoDeviceRow_FallsBackToDeviceId()
    {
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

        var detail = _sut.GetCalendarDayDetail(Day).ShouldHaveSingleItem();
        detail.DeviceName.ShouldBe("ORPHAN"); // no Devices row => id fallback
        detail.DeviceType.ShouldBe(DeviceTypes.Unknown);
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

    [Fact]
    public void GetCalendarDaySummaries_LeapFebruary_Returns29Entries() =>
        _sut.GetCalendarDaySummaries(2024, 2).Count.ShouldBe(29);

    [Fact]
    public void GetCalendarDaySummaries_EmptyMonth_AllDaysPresentWithNoData()
    {
        var summaries = _sut.GetCalendarDaySummaries(2026, 5);

        summaries.Count.ShouldBe(31);
        summaries.ShouldAllBe(s => !s.HasData);
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

    private static Device Device(string deviceId, string name, DeviceTypes type, bool hidden) =>
        new()
        {
            DeviceId = deviceId,
            DeviceName = name,
            DeviceType = type,
            IsHiddenFromDisplay = hidden,
        };
}
