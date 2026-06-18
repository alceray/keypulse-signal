using KeyPulse.Helpers;

namespace KeyPulse.Tests.Helpers;

/// <summary>
/// Coverage for the live active-time accumulator. Deterministic: timestamps are passed in, so no
/// dependence on the wall clock. All times use a single local day unless a test crosses midnight.
/// </summary>
public class ActiveSecondsAccumulatorTests
{
    private static readonly DateTime Base = new(2026, 6, 18, 9, 0, 0, DateTimeKind.Local);

    [Fact]
    public void UnknownDevice_IsZero()
    {
        var acc = new ActiveSecondsAccumulator();
        acc.GetActiveSeconds("DEV").ShouldBe(0);
    }

    [Fact]
    public void RecordActivity_CollapsesBurstWithinOneSecond()
    {
        var acc = new ActiveSecondsAccumulator();
        acc.RecordActivity("DEV", Base);
        acc.RecordActivity("DEV", Base.AddMilliseconds(200));
        acc.RecordActivity("DEV", Base.AddMilliseconds(900));
        acc.GetActiveSeconds("DEV").ShouldBe(1);
    }

    [Fact]
    public void RecordActivity_CountsEachDistinctSecond()
    {
        var acc = new ActiveSecondsAccumulator();
        acc.RecordActivity("DEV", Base);
        acc.RecordActivity("DEV", Base.AddSeconds(1));
        acc.RecordActivity("DEV", Base.AddSeconds(5));
        acc.GetActiveSeconds("DEV").ShouldBe(3);
    }

    [Fact]
    public void RecordActivity_TracksDevicesIndependently()
    {
        var acc = new ActiveSecondsAccumulator();
        acc.RecordActivity("A", Base);
        acc.RecordActivity("A", Base.AddSeconds(1));
        acc.RecordActivity("B", Base);
        acc.GetActiveSeconds("A").ShouldBe(2);
        acc.GetActiveSeconds("B").ShouldBe(1);
    }

    [Fact]
    public void Seed_SetsBaseline_ThenTicksAboveIt()
    {
        var acc = new ActiveSecondsAccumulator();
        acc.Seed("DEV", 1800); // 30 persisted active minutes
        acc.GetActiveSeconds("DEV").ShouldBe(1800);

        acc.RecordActivity("DEV", Base);
        acc.GetActiveSeconds("DEV").ShouldBe(1801);
    }

    [Fact]
    public void Seed_IsIgnoredOnRepeat()
    {
        var acc = new ActiveSecondsAccumulator();
        acc.Seed("DEV", 60);
        acc.Seed("DEV", 6000); // already seeded today -> ignored
        acc.GetActiveSeconds("DEV").ShouldBe(60);
    }

    [Fact]
    public void Seed_KeepsLargerOfBaselineAndAlreadyRecorded()
    {
        var acc = new ActiveSecondsAccumulator();
        acc.RecordActivity("DEV", Base);
        acc.RecordActivity("DEV", Base.AddSeconds(1));
        acc.RecordActivity("DEV", Base.AddSeconds(2)); // 3 live seconds before the seed arrives

        acc.Seed("DEV", 1); // baseline lower than what we already counted
        acc.GetActiveSeconds("DEV").ShouldBe(3);
    }

    [Fact]
    public void ResetIfDayChanged_ClearsStateOnNewDay()
    {
        var acc = new ActiveSecondsAccumulator();
        var today = DateOnly.FromDateTime(Base);
        acc.ResetIfDayChanged(today).ShouldBeTrue(); // first call establishes the day

        acc.Seed("DEV", 600);
        acc.RecordActivity("DEV", Base);
        acc.GetActiveSeconds("DEV").ShouldBe(601);

        acc.ResetIfDayChanged(today).ShouldBeFalse(); // same day -> no-op
        acc.GetActiveSeconds("DEV").ShouldBe(601);

        acc.ResetIfDayChanged(today.AddDays(1)).ShouldBeTrue();
        acc.GetActiveSeconds("DEV").ShouldBe(0); // new day starts empty
    }
}
