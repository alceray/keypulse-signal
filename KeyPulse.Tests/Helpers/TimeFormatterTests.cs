using KeyPulse.Helpers;

namespace KeyPulse.Tests.Helpers;

/// <summary>
/// Tier 1 coverage for the pure time helpers. These are timezone-independent on purpose:
/// inputs are either UTC, or Local-kind values that pass through ToLocalTime unchanged, so the
/// assertions hold on any machine regardless of the host's local zone.
/// </summary>
public class TimeFormatterTests
{
    // --- TruncateToMinute: zeroes sub-minute components, PRESERVES DateTimeKind ---

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void TruncateToMinute_ZeroesSecondsAndBelow_AndPreservesKind(DateTimeKind kind)
    {
        var input = new DateTime(2026, 5, 27, 14, 35, 47, 123, kind).AddTicks(456);

        var result = input.TruncateToMinute();

        result.ShouldBe(new DateTime(2026, 5, 27, 14, 35, 0, kind));
        result.Second.ShouldBe(0);
        result.Millisecond.ShouldBe(0);
        result.Kind.ShouldBe(kind); // key contract: Kind is carried through
    }

    // --- TruncateToSecond: zeroes sub-second components, PRESERVES DateTimeKind ---

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void TruncateToSecond_ZeroesMillisecondsAndBelow_AndPreservesKind(DateTimeKind kind)
    {
        var input = new DateTime(2026, 5, 27, 14, 35, 47, 123, kind).AddTicks(456);

        var result = input.TruncateToSecond();

        result.ShouldBe(new DateTime(2026, 5, 27, 14, 35, 47, kind));
        result.Second.ShouldBe(47); // seconds retained, unlike TruncateToMinute
        result.Millisecond.ShouldBe(0);
        result.Kind.ShouldBe(kind);
    }

    // --- NormalizeUtcMinute: zeroes sub-minute components, ALWAYS returns Utc kind ---

    [Fact]
    public void NormalizeUtcMinute_OnUtcInput_TruncatesToMinute_AndStaysUtc()
    {
        var input = new DateTime(2026, 5, 27, 14, 35, 47, 123, DateTimeKind.Utc);

        var result = TimeFormatter.NormalizeUtcMinute(input);

        result.ShouldBe(new DateTime(2026, 5, 27, 14, 35, 0, DateTimeKind.Utc));
        result.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void NormalizeUtcMinute_OnLocalInput_ConvertsToUtc()
    {
        var local = new DateTime(2026, 5, 27, 14, 35, 47, DateTimeKind.Local);

        var result = TimeFormatter.NormalizeUtcMinute(local);

        // Equivalent to converting to UTC then dropping seconds — verified against the framework
        // conversion so the assertion is correct in every timezone.
        var expected = local.ToUniversalTime();
        result.ShouldBe(
            new DateTime(
                expected.Year,
                expected.Month,
                expected.Day,
                expected.Hour,
                expected.Minute,
                0,
                DateTimeKind.Utc
            )
        );
        result.Kind.ShouldBe(DateTimeKind.Utc);
    }

    /// <summary>
    /// The whole reason both helpers exist: given the same Local-kind instant, NormalizeUtcMinute
    /// forces Utc while TruncateToMinute keeps Local. They are NOT interchangeable (see CLAUDE.md).
    /// </summary>
    [Fact]
    public void NormalizeUtcMinute_And_TruncateToMinute_DifferOnKind()
    {
        var local = new DateTime(2026, 5, 27, 14, 35, 47, DateTimeKind.Local);

        TimeFormatter.NormalizeUtcMinute(local).Kind.ShouldBe(DateTimeKind.Utc);
        local.TruncateToMinute().Kind.ShouldBe(DateTimeKind.Local);
    }

    // --- ToLocalDay: a Local-kind value maps to its own calendar day ---

    [Fact]
    public void ToLocalDay_OnLocalInput_ReturnsThatCalendarDay()
    {
        var local = new DateTime(2026, 5, 27, 23, 59, 0, DateTimeKind.Local);

        TimeFormatter.ToLocalDay(local).ShouldBe(new DateOnly(2026, 5, 27));
    }

    // --- FormatDuration: top-3 significant units, no negative/zero output ---

    [Theory]
    [InlineData(0, "0s")]
    [InlineData(-5, "0s")]
    [InlineData(1, "1s")]
    [InlineData(90, "1m 30s")]
    [InlineData(3600, "1h")]
    [InlineData(86400 * 2 + 3600 * 3 + 60 * 4 + 5, "2d 3h 4m")] // capped at top 3 units
    [InlineData(86400 * 366, "1y 1d")] // 366 days => 1 year + 1 day
    public void FormatDuration_FormatsTopThreeUnits(long totalSeconds, string expected)
    {
        TimeFormatter.FormatDuration(TimeSpan.FromSeconds(totalSeconds)).ShouldBe(expected);
    }

    // --- FormatDateRange ---

    [Fact]
    public void FormatDateRange_WithNullFrom_ReturnsAllTime()
    {
        TimeFormatter
            .FormatDateRange(null, new DateTime(2026, 5, 27, 0, 0, 0, DateTimeKind.Local))
            .ShouldBe("All Time");
    }

    [Fact]
    public void FormatDateRange_SameDay_CollapsesToSingleDate()
    {
        var from = new DateTime(2026, 5, 27, 8, 0, 0, DateTimeKind.Local);
        var to = new DateTime(2026, 5, 27, 20, 0, 0, DateTimeKind.Local);

        TimeFormatter.FormatDateRange(from, to).ShouldBe("May 27, 2026");
    }
}
