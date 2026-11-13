using KeyPulse.Models;
using KeyPulse.ViewModels.Calendar;

namespace KeyPulse.Tests.ViewModels;

public class CalendarHourlyInputBarBuilderTests
{
    [Theory]
    [InlineData(24)]
    [InlineData(0)] // empty -> zero-padded to 24
    [InlineData(30)] // oversized -> truncated to 24
    public void Build_AlwaysReturns24Bars(int inputLength) =>
        CalendarHourlyInputBarBuilder.Build(new long[inputLength]).Count.ShouldBe(24);

    [Fact]
    public void Build_AllZero_AllBaselineNoPeak()
    {
        var bars = CalendarHourlyInputBarBuilder.Build(new long[24]);

        bars.ShouldAllBe(b => !b.IsPeak);
        bars.ShouldAllBe(b => b.BarHeight == 2.0);
    }

    [Fact]
    public void Build_SinglePeak_MaxHeightAndSingleFlag()
    {
        var input = new long[24];
        input[9] = 100;

        var bars = CalendarHourlyInputBarBuilder.Build(input);

        bars[9].IsPeak.ShouldBeTrue();
        bars[9].BarHeight.ShouldBe(46.0);
        bars.Count(b => b.IsPeak).ShouldBe(1);
    }

    [Fact]
    public void Build_TiedMaxima_MultiplePeaks()
    {
        var input = new long[24];
        input[1] = 50;
        input[2] = 50;

        CalendarHourlyInputBarBuilder.Build(input).Count(b => b.IsPeak).ShouldBe(2);
    }

    [Fact]
    public void Build_BeyondHour23_Ignored()
    {
        var input = new long[26];
        input[25] = 999;

        var bars = CalendarHourlyInputBarBuilder.Build(input);

        bars.Count.ShouldBe(24);
        bars.ShouldAllBe(b => b.Total != 999);
    }
}

public class CalendarDtoTests
{
    [Theory]
    [InlineData(DeviceTypes.Keyboard, "⌨")]
    [InlineData(DeviceTypes.Mouse, "🖱")]
    [InlineData(DeviceTypes.Other, "?")]
    [InlineData(DeviceTypes.Unknown, "?")]
    public void TileDevice_TypeIcon(DeviceTypes type, string icon) =>
        new CalendarTileDevice { DeviceType = type }.TypeIcon.ShouldBe(icon);

    [Fact]
    public void DaySummary_IsToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        new CalendarDaySummary { Day = today }.IsToday.ShouldBeTrue();
        new CalendarDaySummary { Day = today.AddDays(-1) }.IsToday.ShouldBeFalse();
    }

    [Fact]
    public void DeviceDetail_DeviceTypeText_IsEnumName() =>
        new CalendarDeviceDetail { DeviceType = DeviceTypes.Keyboard }.DeviceTypeText.ShouldBe("Keyboard");

    [Fact]
    public void DeviceDetail_DefaultHourlyBars_Are24() => new CalendarDeviceDetail().HourlyInputBars.Count.ShouldBe(24);

    [Theory]
    [InlineData(0, "12am")]
    [InlineData(12, "12pm")]
    [InlineData(6, "")]
    [InlineData(23, "")]
    public void HourlyBar_MajorLabel(int hour, string label) =>
        new CalendarHourlyInputBar { Hour = hour }.MajorLabel.ShouldBe(label);

    [Theory]
    [InlineData(4, "4am")]
    [InlineData(8, "8am")]
    [InlineData(16, "4pm")]
    [InlineData(20, "8pm")]
    [InlineData(0, "")]
    [InlineData(13, "")]
    public void HourlyBar_MinorLabel(int hour, string label) =>
        new CalendarHourlyInputBar { Hour = hour }.MinorLabel.ShouldBe(label);

    [Fact]
    public void HourlyBar_Tooltip_DistinguishesActivity()
    {
        new CalendarHourlyInputBar { Hour = 9, Total = 0 }.Tooltip.ShouldContain("0 inputs");
        new CalendarHourlyInputBar { Hour = 9, Total = 5 }.Tooltip.ShouldContain("5 inputs");
    }
    /// <summary>Coverage for CalendarDeviceDetail's computed time text: seconds tick on today's row, while
    /// settled past days floor to the minute.</summary>
    [Fact]
    public void ActiveTimeText_Today_ShowsSecondsWithShareOfConnectedTime()
    {
        var detail = new CalendarDeviceDetail
        {
            ActiveSeconds = 3347,
            ConnectionSeconds = 9844,
            IsToday = true,
        };
        detail.ActiveTimeText.ShouldBe("55m 47s · 34%");
    }

    [Fact]
    public void ActiveTimeText_PastDay_TruncatesSeconds()
    {
        var detail = new CalendarDeviceDetail
        {
            ActiveSeconds = 3347,
            ConnectionSeconds = 9844,
            IsToday = false,
        };
        detail.ActiveTimeText.ShouldBe("55m · 34%");
    }

    [Fact]
    public void ActiveTimeText_CapsShareAt100Percent()
    {
        var detail = new CalendarDeviceDetail
        {
            ActiveSeconds = 600,
            ConnectionSeconds = 600,
            IsToday = true,
        };
        detail.ActiveTimeText.ShouldBe("10m · 100%");
    }

    [Fact]
    public void ActiveTimeText_OmitsShareWhenNotConnected()
    {
        var detail = new CalendarDeviceDetail
        {
            ActiveSeconds = 0,
            ConnectionSeconds = 0,
            IsToday = true,
        };
        detail.ActiveTimeText.ShouldBe("0s");
    }

    [Fact]
    public void ConnectedTimeText_Today_ShowsSeconds()
    {
        var detail = new CalendarDeviceDetail { ConnectionSeconds = 9844, IsToday = true };
        detail.ConnectedTimeText.ShouldBe("2h 44m 4s");
    }

    [Fact]
    public void ConnectedTimeText_PastDay_TruncatesSeconds()
    {
        var detail = new CalendarDeviceDetail { ConnectionSeconds = 9844, IsToday = false };
        detail.ConnectedTimeText.ShouldBe("2h 44m");
    }
}
