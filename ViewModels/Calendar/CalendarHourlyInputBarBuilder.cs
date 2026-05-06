namespace KeyPulse.ViewModels.Calendar;

internal static class CalendarHourlyInputBarBuilder
{
    private const int HOURS_PER_DAY = 24;
    private const double BASELINE_HEIGHT = 2.0;
    private const double MAX_HEIGHT = 46.0;

    public static IReadOnlyList<CalendarHourlyInputBar> Build(IReadOnlyList<long> hourlyInputCount)
    {
        var totals = new long[HOURS_PER_DAY];
        var copyCount = Math.Min(hourlyInputCount.Count, HOURS_PER_DAY);
        for (var i = 0; i < copyCount; i++)
            totals[i] = hourlyInputCount[i];

        var peak = totals.Max();
        var result = new List<CalendarHourlyInputBar>(HOURS_PER_DAY);

        for (var hour = 0; hour < HOURS_PER_DAY; hour++)
        {
            var value = totals[hour];
            var barHeight =
                value <= 0 || peak <= 0
                    ? BASELINE_HEIGHT
                    : BASELINE_HEIGHT + ((double)value / peak) * (MAX_HEIGHT - BASELINE_HEIGHT);

            result.Add(
                new CalendarHourlyInputBar
                {
                    Hour = hour,
                    Total = value,
                    BarHeight = barHeight,
                    IsPeak = peak > 0 && value == peak,
                }
            );
        }

        return result;
    }
}
