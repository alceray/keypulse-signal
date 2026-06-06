using KeyPulse.Models;

namespace KeyPulse.ViewModels.Dashboard;

/// <summary>
/// Lets long chart ranges be served from per-day hourly aggregates instead of raw minute rows, so
/// query cost scales with the displayed range and pruned history still charts fully. Pseudo-snapshots
/// are shaped so the chart's per-device-type value selector reads the aggregated count exactly once.
/// </summary>
internal static class DashboardHourlyActivityAdapter
{
    /// <summary>True when the range resolves to hour-aligned buckets, which hourly aggregates fill exactly.</summary>
    public static bool CanServeFromHourlyAggregates(TimeSpan rangeSpan) =>
        DashboardActivityChartBuilder.ResolveBucketSize(rangeSpan).BucketMinutes >= 60;

    /// <summary>
    /// Expands each day's non-zero hours into hour-bucket pseudo-snapshots. The combined hourly count
    /// goes into the field the device's chart selector reads; for single-role devices that equals the
    /// selector's own slice, while combo devices chart their combined input at hour granularity.
    /// </summary>
    public static IReadOnlyList<ActivitySnapshot> ToHourlySnapshots(
        IReadOnlyList<DailyDeviceStat> stats,
        IReadOnlyDictionary<string, DeviceTypes> deviceTypesById
    )
    {
        var snapshots = new List<ActivitySnapshot>();
        foreach (var stat in stats)
        {
            var dayStart = stat.Day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
            deviceTypesById.TryGetValue(stat.DeviceId, out var deviceType);
            var isKeyboard = deviceType == DeviceTypes.Keyboard;

            var hours = Math.Min(stat.HourlyInputCount.Length, 24);
            for (var hour = 0; hour < hours; hour++)
            {
                var count = stat.HourlyInputCount[hour];
                if (count <= 0)
                    continue;

                snapshots.Add(
                    new ActivitySnapshot
                    {
                        DeviceId = stat.DeviceId,
                        Minute = dayStart.AddHours(hour),
                        Keystrokes = isKeyboard ? (int)count : 0,
                        MouseClicks = isKeyboard ? 0 : (int)count,
                    }
                );
            }
        }

        return snapshots;
    }
}
