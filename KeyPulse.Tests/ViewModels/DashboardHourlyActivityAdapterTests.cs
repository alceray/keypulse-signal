using KeyPulse.Models;
using KeyPulse.ViewModels.Dashboard;
using OxyPlot;
using OxyPlot.Series;

namespace KeyPulse.Tests.ViewModels;

/// <summary>
/// The hourly-aggregate chart source: tier selection, pseudo-snapshot expansion, and equivalence
/// with the raw-minute path at an hour-aligned bucket tier.
/// </summary>
public class DashboardHourlyActivityAdapterTests
{
    private static readonly DateOnly Day = new(2026, 5, 20);

    // ── Bucket tiers ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 5)] // 1 Day
    [InlineData(7, 30)] // 1 Week
    [InlineData(30, 120)] // 1 Month
    [InlineData(365, 1440)] // 1 Year
    public void ResolveBucketSize_PinsRangeTiers(int rangeDays, int expectedBucketMinutes) =>
        DashboardActivityChartBuilder
            .ResolveBucketSize(TimeSpan.FromDays(rangeDays))
            .BucketMinutes.ShouldBe(expectedBucketMinutes);

    [Theory]
    [InlineData(1, false)]
    [InlineData(7, false)]
    [InlineData(30, true)]
    [InlineData(365, true)]
    public void CanServeFromHourlyAggregates_TrueOnlyForHourAlignedTiers(int rangeDays, bool expected) =>
        DashboardHourlyActivityAdapter.CanServeFromHourlyAggregates(TimeSpan.FromDays(rangeDays)).ShouldBe(expected);

    // ── ToHourlySnapshots ───────────────────────────────────────────────────────

    [Fact]
    public void ToHourlySnapshots_KeyboardCountsLandInKeystrokes()
    {
        var snapshot = DashboardHourlyActivityAdapter
            .ToHourlySnapshots([Stat("KB", hour: 9, count: 50)], Types(("KB", DeviceTypes.Keyboard)))
            .ShouldHaveSingleItem();

        snapshot.DeviceId.ShouldBe("KB");
        snapshot.Minute.ShouldBe(Day.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Local));
        snapshot.Keystrokes.ShouldBe(50);
        snapshot.MouseClicks.ShouldBe(0);
    }

    [Theory]
    [InlineData(DeviceTypes.Mouse)]
    [InlineData(DeviceTypes.Other)]
    [InlineData(DeviceTypes.Unknown)]
    public void ToHourlySnapshots_NonKeyboardCountsLandInMouseClicks(DeviceTypes type)
    {
        var snapshot = DashboardHourlyActivityAdapter
            .ToHourlySnapshots([Stat("M1", hour: 14, count: 33)], Types(("M1", type)))
            .ShouldHaveSingleItem();

        snapshot.Keystrokes.ShouldBe(0);
        snapshot.MouseClicks.ShouldBe(33);
    }

    [Fact]
    public void ToHourlySnapshots_ZeroHoursAreSkipped()
    {
        var stat = Stat("KB", hour: 9, count: 50);
        stat.HourlyInputCount[17] = 25;

        var snapshots = DashboardHourlyActivityAdapter.ToHourlySnapshots([stat], Types(("KB", DeviceTypes.Keyboard)));

        snapshots.Count.ShouldBe(2);
        snapshots.Select(s => s.Minute.Hour).ShouldBe([9, 17]);
    }

    // ── Equivalence with the raw-minute path ────────────────────────────────────

    [Fact]
    public void ChartPoints_FromHourlyAggregates_MatchRawMinutePath()
    {
        // 30-day range resolves to 120-minute buckets, so both sources land in the same buckets.
        var from = Day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var to = from.AddDays(30);
        var devices = new[] { Device("KB", DeviceTypes.Keyboard) };
        var lifecycle = new[]
        {
            new DeviceEvent { EventType = EventTypes.AppStarted, EventTime = from },
        };
        var colors = new Dictionary<string, OxyColor>();

        var rawSnapshots = new[]
        {
            Minute("KB", from.AddHours(9).AddMinutes(5), keys: 30),
            Minute("KB", from.AddHours(9).AddMinutes(6), keys: 20),
        };
        var aggregateSnapshots = DashboardHourlyActivityAdapter.ToHourlySnapshots(
            [Stat("KB", hour: 9, count: 50)],
            Types(("KB", DeviceTypes.Keyboard))
        );

        var rawPoints = SeriesPoints(rawSnapshots, devices, lifecycle, from, to, colors);
        var aggregatePoints = SeriesPoints(aggregateSnapshots, devices, lifecycle, from, to, colors);

        aggregatePoints.ShouldBe(rawPoints);
    }

    private static List<(double X, double Y)> SeriesPoints(
        IReadOnlyCollection<ActivitySnapshot> snapshots,
        IReadOnlyCollection<Device> devices,
        IReadOnlyCollection<DeviceEvent> lifecycle,
        DateTime from,
        DateTime to,
        IReadOnlyDictionary<string, OxyColor> colors
    )
    {
        var data = DashboardActivityChartBuilder.ComputeInputActivityPlot(
            snapshots,
            devices,
            lifecycle,
            from,
            to,
            colors
        );
        var series = data.Series.OfType<LineSeries>().ShouldHaveSingleItem();
        return series.Points.Select(p => (p.X, p.Y)).ToList();
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private static DailyDeviceStat Stat(string deviceId, int hour, long count)
    {
        var hourly = new long[24];
        hourly[hour] = count;
        return new DailyDeviceStat
        {
            Day = Day,
            DeviceId = deviceId,
            HourlyInputCount = hourly,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    private static Device Device(string deviceId, DeviceTypes type) =>
        new()
        {
            DeviceId = deviceId,
            DeviceName = deviceId,
            DeviceType = type,
        };

    private static ActivitySnapshot Minute(string deviceId, DateTime minute, int keys) =>
        new()
        {
            DeviceId = deviceId,
            Minute = minute,
            Keystrokes = keys,
        };

    private static IReadOnlyDictionary<string, DeviceTypes> Types(params (string Id, DeviceTypes Type)[] entries) =>
        entries.ToDictionary(e => e.Id, e => e.Type);
}
