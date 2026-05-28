using KeyPulse.Helpers;
using KeyPulse.Models;

namespace KeyPulse.Tests.Models;

public class DeviceEventTests
{
    [Fact]
    public void EventTimeLocal_DelegatesToToLocalTime()
    {
        var utc = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc);
        var evt = new DeviceEvent { EventType = EventTypes.Connected, EventTime = utc };

        evt.EventTimeLocal.ShouldBe(TimeFormatter.ToLocalTime(utc));
    }
}
