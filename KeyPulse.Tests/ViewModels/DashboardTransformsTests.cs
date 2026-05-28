using KeyPulse.Models;
using KeyPulse.ViewModels.Dashboard;
using OxyPlot;

namespace KeyPulse.Tests.ViewModels;

public class DashboardRangeResolverTests
{
    private static readonly DateTime Now = new(2026, 5, 20, 12, 0, 0, DateTimeKind.Local);

    [Fact]
    public void OneDay() => DashboardRangeResolver.ResolveRangeStart("1 Day", Now).ShouldBe(Now.AddDays(-1));

    [Fact]
    public void OneWeek() => DashboardRangeResolver.ResolveRangeStart("1 Week", Now).ShouldBe(Now.AddDays(-7));

    [Fact]
    public void OneMonth() => DashboardRangeResolver.ResolveRangeStart("1 Month", Now).ShouldBe(Now.AddMonths(-1));

    [Fact]
    public void ThreeMonths() => DashboardRangeResolver.ResolveRangeStart("3 Months", Now).ShouldBe(Now.AddMonths(-3));

    [Fact]
    public void OneYear() => DashboardRangeResolver.ResolveRangeStart("1 Year", Now).ShouldBe(Now.AddYears(-1));

    [Fact]
    public void AllTime_ReturnsNull() => DashboardRangeResolver.ResolveRangeStart("All Time", Now).ShouldBeNull();

    [Fact]
    public void UnknownLabel_DefaultsToWeek() =>
        DashboardRangeResolver.ResolveRangeStart("garbage", Now).ShouldBe(Now.AddDays(-7));
}

public class DashboardConnectionTimeCalculatorTests
{
    private static readonly DateTime T0 = new(2026, 5, 20, 9, 0, 0, DateTimeKind.Local);
    private static readonly DateTime RangeEnd = new(2026, 5, 20, 23, 0, 0, DateTimeKind.Local);

    private static DeviceEvent Evt(string id, EventTypes type, DateTime time) =>
        new()
        {
            DeviceId = id,
            EventType = type,
            EventTime = time,
        };

    [Fact]
    public void Empty_ReturnsEmpty() =>
        DashboardConnectionTimeCalculator.ComputeConnectionMinutesByDevice([], null, RangeEnd).ShouldBeEmpty();

    [Fact]
    public void PairedSession_SumsMinutes()
    {
        List<DeviceEvent> events =
        [
            Evt("D1", EventTypes.Connected, T0),
            Evt("D1", EventTypes.Disconnected, T0.AddMinutes(30)),
        ];

        DashboardConnectionTimeCalculator.ComputeConnectionMinutesByDevice(events, null, RangeEnd)["D1"].ShouldBe(30.0);
    }

    [Fact]
    public void ClosingWithoutOpening_Ignored()
    {
        List<DeviceEvent> events = [Evt("D1", EventTypes.Disconnected, T0)];

        DashboardConnectionTimeCalculator.ComputeConnectionMinutesByDevice(events, null, RangeEnd).ShouldBeEmpty();
    }

    [Fact]
    public void OpenSession_ClippedToRangeEnd()
    {
        List<DeviceEvent> events = [Evt("D1", EventTypes.Connected, T0)];

        DashboardConnectionTimeCalculator
            .ComputeConnectionMinutesByDevice(events, null, T0.AddMinutes(15))["D1"]
            .ShouldBe(15.0);
    }

    [Fact]
    public void IntervalClippedToRangeStart()
    {
        var rangeStart = T0.AddMinutes(10);
        List<DeviceEvent> events =
        [
            Evt("D1", EventTypes.Connected, T0),
            Evt("D1", EventTypes.Disconnected, T0.AddMinutes(40)),
        ];

        DashboardConnectionTimeCalculator
            .ComputeConnectionMinutesByDevice(events, rangeStart, RangeEnd)["D1"]
            .ShouldBe(30.0);
    }

    [Fact]
    public void IntervalEntirelyBeforeRange_Dropped()
    {
        var rangeStart = T0.AddHours(5);
        List<DeviceEvent> events =
        [
            Evt("D1", EventTypes.Connected, T0),
            Evt("D1", EventTypes.Disconnected, T0.AddMinutes(30)),
        ];

        DashboardConnectionTimeCalculator
            .ComputeConnectionMinutesByDevice(events, rangeStart, RangeEnd)
            .ShouldBeEmpty();
    }

    [Fact]
    public void ZeroLengthSession_Dropped()
    {
        List<DeviceEvent> events = [Evt("D1", EventTypes.Connected, T0), Evt("D1", EventTypes.Disconnected, T0)];

        DashboardConnectionTimeCalculator.ComputeConnectionMinutesByDevice(events, null, RangeEnd).ShouldBeEmpty();
    }

    [Fact]
    public void MultipleSessions_SameDevice_Summed()
    {
        List<DeviceEvent> events =
        [
            Evt("D1", EventTypes.Connected, T0),
            Evt("D1", EventTypes.Disconnected, T0.AddMinutes(10)),
            Evt("D1", EventTypes.Connected, T0.AddMinutes(20)),
            Evt("D1", EventTypes.Disconnected, T0.AddMinutes(50)),
        ];

        DashboardConnectionTimeCalculator.ComputeConnectionMinutesByDevice(events, null, RangeEnd)["D1"].ShouldBe(40.0);
    }
}

public class DashboardDeviceColorPaletteTests
{
    private static Device Dev(string id, DeviceTypes type, string name = "") =>
        new()
        {
            DeviceId = id,
            DeviceType = type,
            DeviceName = name,
        };

    [Fact]
    public void Faded_SetsAlpha60_PreservesRgb()
    {
        var faded = DashboardDeviceColorPalette.Faded(OxyColor.FromRgb(10, 20, 30));

        faded.A.ShouldBe((byte)60);
        faded.R.ShouldBe((byte)10);
        faded.G.ShouldBe((byte)20);
        faded.B.ShouldBe((byte)30);
    }

    [Fact]
    public void GetColors_Empty_ReturnsEmpty() =>
        new DashboardDeviceColorPalette().GetColorsForDevices([], new Dictionary<string, double>()).ShouldBeEmpty();

    [Fact]
    public void GetColors_AllZeroMinutes_AllOthersColor()
    {
        Device[] devices = [Dev("K1", DeviceTypes.Keyboard, "a"), Dev("M1", DeviceTypes.Mouse, "b")];

        var colors = new DashboardDeviceColorPalette().GetColorsForDevices(devices, new Dictionary<string, double>());

        colors["K1"].ShouldBe(DashboardDeviceColorPalette.OthersColor);
        colors["M1"].ShouldBe(DashboardDeviceColorPalette.OthersColor);
    }

    [Fact]
    public void GetColors_SingleActiveDevice_GetsPaletteColor()
    {
        Device[] devices = [Dev("K1", DeviceTypes.Keyboard, "a")];
        var minutes = new Dictionary<string, double> { ["K1"] = 100 };

        new DashboardDeviceColorPalette()
            .GetColorsForDevices(devices, minutes)["K1"]
            .ShouldNotBe(DashboardDeviceColorPalette.OthersColor);
    }

    [Fact]
    public void GetColors_ShareBelowThreshold_OthersColor()
    {
        Device[] devices = [Dev("K1", DeviceTypes.Keyboard, "a"), Dev("K2", DeviceTypes.Keyboard, "b")];
        var minutes = new Dictionary<string, double> { ["K1"] = 1000, ["K2"] = 1 }; // K2 share ~0.1%

        var colors = new DashboardDeviceColorPalette().GetColorsForDevices(devices, minutes);

        colors["K1"].ShouldNotBe(DashboardDeviceColorPalette.OthersColor);
        colors["K2"].ShouldBe(DashboardDeviceColorPalette.OthersColor);
    }

    [Fact]
    public void GetColors_UnknownType_AbsentFromMap()
    {
        Device[] devices = [Dev("U1", DeviceTypes.Unknown, "a")];
        var minutes = new Dictionary<string, double> { ["U1"] = 100 };

        // Only Keyboard and Mouse types are assigned colors.
        new DashboardDeviceColorPalette()
            .GetColorsForDevices(devices, minutes)
            .ContainsKey("U1")
            .ShouldBeFalse();
    }
}
