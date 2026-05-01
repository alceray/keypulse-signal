using KeyPulse.Models;

namespace KeyPulse.ViewModels.Dashboard;

/// <summary>
/// Resolves dashboard time-range selections to concrete start timestamps.
/// </summary>
internal static class DashboardRangeResolver
{
    public const string RangeOneDay = "1 Day";
    public const string RangeOneWeek = "1 Week";
    public const string RangeOneMonth = "1 Month";
    public const string RangeOneYear = "1 Year";
    public const string RangeAllTime = "All Time";
    public const string DefaultRange = RangeOneWeek;

    /// <summary>Supported range filters shown in the dashboard toolbar.</summary>
    public static readonly IReadOnlyList<string> RangeOptions =
    [
        RangeOneDay,
        RangeOneWeek,
        RangeOneMonth,
        RangeOneYear,
        RangeAllTime,
    ];

    /// <summary>
    /// Converts a selected range label to an optional range start.
    /// Returns <c>null</c> for all-time queries.
    /// </summary>
    public static DateTime? ResolveRangeStart(string selectedRange, DateTime now)
    {
        return selectedRange switch
        {
            RangeOneDay => now.AddDays(-1),
            RangeOneWeek => now.AddDays(-7),
            RangeOneMonth => now.AddMonths(-1),
            RangeOneYear => now.AddYears(-1),
            RangeAllTime => null,
            _ => now.AddDays(-7),
        };
    }
}

/// <summary>
/// Computes dashboard connection duration totals from device lifecycle events.
/// </summary>
internal static class DashboardConnectionDurationCalculator
{
    /// <summary>
    /// Calculates per-device connection-duration minutes within the requested range by pairing opening and closing events.
    /// Open sessions are clipped to <paramref name="rangeEnd"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, double> ComputeConnectionDurationMinutesByDevice(
        IReadOnlyList<DeviceEvent> events,
        DateTime? rangeStart,
        DateTime rangeEnd
    )
    {
        var openByDevice = new Dictionary<string, DateTime>();
        var connectionDurationMinutesByDevice = new Dictionary<string, double>();

        foreach (var deviceEvent in events)
        {
            if (deviceEvent.EventType.IsOpeningEvent())
            {
                openByDevice[deviceEvent.DeviceId] = deviceEvent.EventTime;
                continue;
            }

            if (!deviceEvent.EventType.IsClosingEvent())
                continue;

            if (!openByDevice.TryGetValue(deviceEvent.DeviceId, out var startTime))
                continue;

            AddIntervalConnectionDuration(
                connectionDurationMinutesByDevice,
                deviceEvent.DeviceId,
                startTime,
                deviceEvent.EventTime,
                rangeStart,
                rangeEnd
            );
            openByDevice.Remove(deviceEvent.DeviceId);
        }

        foreach (var (deviceId, startTime) in openByDevice)
            AddIntervalConnectionDuration(
                connectionDurationMinutesByDevice,
                deviceId,
                startTime,
                rangeEnd,
                rangeStart,
                rangeEnd
            );

        return connectionDurationMinutesByDevice;
    }

    /// <summary>
    /// Adds a clipped interval duration to the target connection-duration accumulator.
    /// </summary>
    private static void AddIntervalConnectionDuration(
        IDictionary<string, double> connectionDurationMinutesByDevice,
        string deviceId,
        DateTime intervalStart,
        DateTime intervalEnd,
        DateTime? rangeStart,
        DateTime rangeEnd
    )
    {
        if (intervalEnd <= intervalStart)
            return;

        var start = rangeStart.HasValue && intervalStart < rangeStart.Value ? rangeStart.Value : intervalStart;
        var end = intervalEnd > rangeEnd ? rangeEnd : intervalEnd;

        if (end <= start)
            return;

        var connectionDurationMinutes = (end - start).TotalMinutes;
        connectionDurationMinutesByDevice[deviceId] = connectionDurationMinutesByDevice.TryGetValue(
            deviceId,
            out var existing
        )
            ? existing + connectionDurationMinutes
            : connectionDurationMinutes;
    }
}
