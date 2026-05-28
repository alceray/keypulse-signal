using KeyPulse.Models;

namespace KeyPulse.Tests.Models;

public class EventTypeExtensionsTests
{
    [Theory]
    [InlineData(EventTypes.ConnectionStarted, true)]
    [InlineData(EventTypes.Connected, true)]
    [InlineData(EventTypes.ConnectionEnded, false)]
    [InlineData(EventTypes.Disconnected, false)]
    [InlineData(EventTypes.AppStarted, false)]
    [InlineData(EventTypes.AppEnded, false)]
    public void IsOpeningEvent(EventTypes type, bool expected) => type.IsOpeningEvent().ShouldBe(expected);

    [Theory]
    [InlineData(EventTypes.ConnectionEnded, true)]
    [InlineData(EventTypes.Disconnected, true)]
    [InlineData(EventTypes.ConnectionStarted, false)]
    [InlineData(EventTypes.Connected, false)]
    [InlineData(EventTypes.AppStarted, false)]
    [InlineData(EventTypes.AppEnded, false)]
    public void IsClosingEvent(EventTypes type, bool expected) => type.IsClosingEvent().ShouldBe(expected);

    [Theory]
    [InlineData(EventTypes.AppStarted, true)]
    [InlineData(EventTypes.AppEnded, true)]
    [InlineData(EventTypes.ConnectionStarted, false)]
    [InlineData(EventTypes.Connected, false)]
    [InlineData(EventTypes.ConnectionEnded, false)]
    [InlineData(EventTypes.Disconnected, false)]
    public void IsAppEvent(EventTypes type, bool expected) => type.IsAppEvent().ShouldBe(expected);

    [Theory]
    [InlineData(EventTypes.AppStarted)]
    [InlineData(EventTypes.AppEnded)]
    [InlineData(EventTypes.ConnectionStarted)]
    [InlineData(EventTypes.ConnectionEnded)]
    [InlineData(EventTypes.Connected)]
    [InlineData(EventTypes.Disconnected)]
    public void EachKnownEvent_BelongsToExactlyOneCategory(EventTypes type)
    {
        var matches = (type.IsOpeningEvent() ? 1 : 0) + (type.IsClosingEvent() ? 1 : 0) + (type.IsAppEvent() ? 1 : 0);
        matches.ShouldBe(1);
    }

    [Fact]
    public void OutOfRangeValue_BelongsToNoCategory()
    {
        var bogus = (EventTypes)99;
        bogus.IsOpeningEvent().ShouldBeFalse();
        bogus.IsClosingEvent().ShouldBeFalse();
        bogus.IsAppEvent().ShouldBeFalse();
    }
}
