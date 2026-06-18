using KeyPulse.ViewModels.Calendar;

namespace KeyPulse.Tests.ViewModels;

/// <summary>Coverage for CalendarDeviceDetail's computed "active time (share of connected)" text.</summary>
public class CalendarDeviceDetailTests
{
    [Fact]
    public void ActiveTimeText_AppendsShareOfConnectedTime()
    {
        var detail = new CalendarDeviceDetail { ActiveSeconds = 3347, ConnectionSeconds = 9844 };
        detail.ActiveTimeText.ShouldBe("55m 47s · 34%");
    }

    [Fact]
    public void ActiveTimeText_CapsShareAt100Percent()
    {
        var detail = new CalendarDeviceDetail { ActiveSeconds = 600, ConnectionSeconds = 600 };
        detail.ActiveTimeText.ShouldBe("10m · 100%");
    }

    [Fact]
    public void ActiveTimeText_OmitsShareWhenNotConnected()
    {
        var detail = new CalendarDeviceDetail { ActiveSeconds = 0, ConnectionSeconds = 0 };
        detail.ActiveTimeText.ShouldBe("0s");
    }
}
