using KeyPulse.Models;
using KeyPulse.ViewModels.Dashboard;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace KeyPulse.Tests.ViewModels;

/// <summary>
/// Pure-logic coverage for the dashboard chart builders. They emit OxyPlot models, so assertions
/// reach into the produced model's series/slices/axes. GetSliceAt's angle-walk paths require a
/// rendered PlotArea (set internally during render), so only the early-return null branches are
/// asserted here.
/// </summary>
public class DashboardPieChartBuilderTests
{
    private static Device Dev(string id, string name = "") =>
        new()
        {
            DeviceId = id,
            DeviceName = name,
            DeviceType = DeviceTypes.Keyboard,
        };

    [Fact]
    public void Build_NoDevices_EmitsNoDataPie()
    {
        var model = DashboardPieChartBuilder.BuildConnectionTimePiePlot(
            "Keyboards",
            [],
            new Dictionary<string, double>(),
            new Dictionary<string, OxyColor>()
        );

        var pie = model.Series.OfType<PieSeries>().ShouldHaveSingleItem();
        pie.Slices.Count.ShouldBe(1);
        pie.Slices[0].Label.ShouldBe("No data");
    }

    [Fact]
    public void Build_AllZeroMinutes_EmitsNoDataPie()
    {
        Device[] devices = [Dev("A", "Alpha"), Dev("B", "Bravo")];

        var model = DashboardPieChartBuilder.BuildConnectionTimePiePlot(
            "Keyboards",
            devices,
            new Dictionary<string, double>(),
            new Dictionary<string, OxyColor>()
        );

        // All devices filtered out (Value > 0 fails) -> "No data" pie.
        model.Series.OfType<PieSeries>().Single().Slices[0].Label.ShouldBe("No data");
    }

    [Fact]
    public void Build_ZeroValueDeviceExcluded_AndSlicesSortedByValueDesc()
    {
        Device[] devices = [Dev("A", "Alpha"), Dev("B", "Bravo"), Dev("Z", "Zero")];
        var minutes = new Dictionary<string, double>
        {
            ["A"] = 10,
            ["B"] = 30,
            ["Z"] = 0, // excluded
        };

        var model = DashboardPieChartBuilder.BuildConnectionTimePiePlot(
            "K",
            devices,
            minutes,
            new Dictionary<string, OxyColor>()
        );

        var slices = model
            .Series.OfType<PieSeries>()
            .Single()
            .Slices.Cast<DashboardPieSlice>()
            .Select(s => s.Label)
            .ToList();

        slices.ShouldBe(["Bravo", "Alpha"]); // sorted by value desc, no Z
    }

    [Fact]
    public void Build_TinyShareGroupedIntoOthers()
    {
        Device[] devices = [Dev("BIG", "Big"), Dev("TINY", "Tiny")];
        var minutes = new Dictionary<string, double> { ["BIG"] = 1000, ["TINY"] = 1 }; // tiny share ~0.1%

        var model = DashboardPieChartBuilder.BuildConnectionTimePiePlot(
            "K",
            devices,
            minutes,
            new Dictionary<string, OxyColor>()
        );

        var slices = model.Series.OfType<PieSeries>().Single().Slices.Cast<DashboardPieSlice>().ToList();
        slices.Count.ShouldBe(2); // BIG + Others
        slices.ShouldContain(s => s.IsOthers && s.Label == "Others (1)");
    }

    [Fact]
    public void Build_WithSelection_NonMatchingSlicesAreFaded()
    {
        Device[] devices = [Dev("A", "Alpha"), Dev("B", "Bravo")];
        var minutes = new Dictionary<string, double> { ["A"] = 10, ["B"] = 20 };
        var colors = new Dictionary<string, OxyColor>
        {
            ["A"] = OxyColor.FromRgb(1, 2, 3),
            ["B"] = OxyColor.FromRgb(4, 5, 6),
        };

        var model = DashboardPieChartBuilder.BuildConnectionTimePiePlot(
            "K",
            devices,
            minutes,
            colors,
            selectedDeviceId: "A"
        );

        var byId = model
            .Series.OfType<PieSeries>()
            .Single()
            .Slices.Cast<DashboardPieSlice>()
            .ToDictionary(s => s.DeviceId);

        byId["A"].Fill.A.ShouldBe((byte)255); // selected stays opaque
        byId["B"].Fill.A.ShouldBe((byte)60); // non-selected faded (Faded sets alpha=60)
    }

    [Fact]
    public void DashboardPieSlice_DerivedStrings_ForConnected()
    {
        var slice = new DashboardPieSlice("x", 5)
        {
            IsConnected = true,
            LastSeenOrConnectedLabel = "Last connected",
            LastSeenOrConnectedValue = "2h ago",
        };

        slice.StatusTag.ShouldBe("Connected");
        slice.StatusLine.ShouldBe("Status: Connected\n");
        slice.LastSeenOrConnectedLine.ShouldBe("Last connected: 2h ago\n");
    }

    [Fact]
    public void DashboardPieSlice_DerivedStrings_ForDisconnected() =>
        new DashboardPieSlice("x", 5) { IsConnected = false }.StatusTag.ShouldBe("Disconnected");

    [Fact]
    public void DashboardPieSlice_DerivedStrings_ForOthers_SuppressAllLines()
    {
        var slice = new DashboardPieSlice("Others (2)", 5)
        {
            IsOthers = true,
            LastSeenOrConnectedLabel = "Last seen",
            LastSeenOrConnectedValue = "1h ago",
        };

        slice.StatusTag.ShouldBe("");
        slice.StatusLine.ShouldBe(""); // suppressed for the Others slice
        slice.LastSeenOrConnectedLine.ShouldBe("");
    }

    [Fact]
    public void GetSliceAt_NoPieSeriesInModel_ReturnsNull() =>
        DashboardPieChartBuilder.GetSliceAt(new PlotModel(), new ScreenPoint(0, 0)).ShouldBeNull();

    [Fact]
    public void GetSliceAt_PlotAreaNotYetRendered_ReturnsNull()
    {
        // A freshly-built model has PlotArea = default(OxyRect) (width/height 0) until rendered,
        // so maxRadius <= 0 and the angle walk is short-circuited.
        var model = DashboardPieChartBuilder.BuildConnectionTimePiePlot(
            "K",
            [Dev("A", "Alpha")],
            new Dictionary<string, double> { ["A"] = 1 },
            new Dictionary<string, OxyColor>()
        );

        DashboardPieChartBuilder.GetSliceAt(model, new ScreenPoint(0, 0)).ShouldBeNull();
    }
}

public class DashboardActivityChartBuilderTests
{
    private static readonly DateTime From = new(2026, 5, 20, 9, 0, 0, DateTimeKind.Local);
    private static readonly DateTime To = From.AddHours(1);

    private static Device Dev(string id, string name, DeviceTypes type = DeviceTypes.Keyboard) =>
        new()
        {
            DeviceId = id,
            DeviceName = name,
            DeviceType = type,
        };

    private static ActivitySnapshot Snap(string id, DateTime minute, int keys = 0, int clicks = 0, byte move = 0) =>
        new()
        {
            DeviceId = id,
            Minute = minute,
            Keystrokes = keys,
            MouseClicks = clicks,
            MouseMovementSeconds = move,
        };

    private static DeviceEvent AppStarted(DateTime when) =>
        new() { EventType = EventTypes.AppStarted, EventTime = when };

    [Fact]
    public void Build_EmptyInputs_YieldsAxesOnlyModel()
    {
        var model = DashboardActivityChartBuilder.BuildInputActivityPlot(
            [],
            [],
            [],
            from: null,
            to: To,
            colorsByDevice: new Dictionary<string, OxyColor>()
        );

        // DateTimeAxis derives from LinearAxis, so filter by position rather than type.
        model.Axes.Count(a => a.Position == AxisPosition.Bottom).ShouldBe(1);
        model.Axes.Count(a => a.Position == AxisPosition.Left).ShouldBe(1);
        model.Series.OfType<LineSeries>().ShouldBeEmpty();
    }

    [Fact]
    public void Build_AppRunningWithKeyboardActivity_ProducesSeriesWithPositivePoint()
    {
        var devices = new[] { Dev("K1", "kb") };
        var snapshots = new[] { Snap("K1", From.AddMinutes(10), keys: 50) };
        var lifecycle = new[] { AppStarted(From) }; // open interval clipped to `To`

        var model = DashboardActivityChartBuilder.BuildInputActivityPlot(
            snapshots,
            devices,
            lifecycle,
            From,
            To,
            new Dictionary<string, OxyColor>()
        );

        var series = model.Series.OfType<LineSeries>().ShouldHaveSingleItem();
        series.Title.ShouldBe("kb");
        series.Points.ShouldContain(p => p.Y > 0);
    }

    [Fact]
    public void Build_NoLifecycleEvents_AllBucketsZeroedAndSeriesSkipped()
    {
        var devices = new[] { Dev("K1", "kb") };
        var snapshots = new[] { Snap("K1", From.AddMinutes(10), keys: 50) };

        var model = DashboardActivityChartBuilder.BuildInputActivityPlot(
            snapshots,
            devices,
            lifecycleEvents: [], // no AppStarted => no running intervals => buckets forced to 0
            From,
            To,
            new Dictionary<string, OxyColor>()
        );

        model.Series.OfType<LineSeries>().ShouldBeEmpty();
    }

    [Fact]
    public void Build_DeviceWithNoSnapshots_SeriesSkipped()
    {
        var devices = new[] { Dev("K1", "kb") };

        var model = DashboardActivityChartBuilder.BuildInputActivityPlot(
            snapshots: [],
            devices,
            lifecycleEvents: [AppStarted(From)],
            From,
            To,
            new Dictionary<string, OxyColor>()
        );

        model.Series.OfType<LineSeries>().ShouldBeEmpty();
    }

    [Fact]
    public void Build_ShortRange_UsesTenMinuteBuckets()
    {
        var model = DashboardActivityChartBuilder.BuildInputActivityPlot(
            [],
            [],
            [],
            From,
            From.AddHours(1),
            new Dictionary<string, OxyColor>()
        );

        model.Axes.Single(a => a.Position == AxisPosition.Left).Title.ShouldBe("Input count per 10 min");
    }

    [Fact]
    public void Build_MultiDayRange_UsesHourlyBuckets()
    {
        var from = new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Local);
        var to = from.AddDays(3); // <=7d => hourly

        var model = DashboardActivityChartBuilder.BuildInputActivityPlot(
            [],
            [],
            [],
            from,
            to,
            new Dictionary<string, OxyColor>()
        );

        model.Axes.Single(a => a.Position == AxisPosition.Left).Title.ShouldBe("Input count per hour");
    }

    [Fact]
    public void Build_MouseDevice_UsesClicksPlusMovementSelector()
    {
        var devices = new[] { Dev("M1", "mouse", DeviceTypes.Mouse) };
        var snapshots = new[] { Snap("M1", From.AddMinutes(10), clicks: 10, move: 5) }; // selector => 15
        var lifecycle = new[] { AppStarted(From) };

        var model = DashboardActivityChartBuilder.BuildInputActivityPlot(
            snapshots,
            devices,
            lifecycle,
            From,
            To,
            new Dictionary<string, OxyColor>()
        );

        var series = model.Series.OfType<LineSeries>().ShouldHaveSingleItem();
        series.Title.ShouldBe("mouse");
        series.Points.ShouldContain(p => p.Y > 0); // confirms the mouse selector contributed
    }
}
