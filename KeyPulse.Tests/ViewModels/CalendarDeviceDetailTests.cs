using KeyPulse.ViewModels.Calendar;

namespace KeyPulse.Tests.ViewModels;

/// <summary>Coverage for CalendarDeviceDetail's computed time text: seconds tick on today's row, while
/// settled past days floor to the minute.</summary>
public class CalendarDeviceDetailTests
{
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
