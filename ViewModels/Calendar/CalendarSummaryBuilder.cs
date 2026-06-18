using KeyPulse.Models;

namespace KeyPulse.ViewModels.Calendar;

/// <summary>
/// Pure mapping from daily-stat rows plus device metadata to the calendar presentation DTOs, keeping
/// the data layer free of presentation types. Hidden-device filtering happens upstream in the query;
/// live connection state is overlaid later by the view-model.
/// </summary>
public static class CalendarSummaryBuilder
{
    /// <summary>One summary per day of the month; days without rows render as empty tiles.</summary>
    public static IReadOnlyList<CalendarDaySummary> BuildMonthSummaries(
        int year,
        int month,
        IReadOnlyList<DailyDeviceStat> statRows,
        IReadOnlyDictionary<string, Device> devicesById
    )
    {
        var from = new DateOnly(year, month, 1);
        var to = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var grouped = statRows.GroupBy(d => d.Day).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<CalendarDaySummary>(to.Day);
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            grouped.TryGetValue(day, out var dayRows);
            result.Add(ToDaySummary(day, dayRows, devicesById));
        }

        return result.AsReadOnly();
    }

    /// <summary>Per-device detail rows for a single day, sorted by connection seconds descending.</summary>
    public static IReadOnlyList<CalendarDeviceDetail> BuildDayDetails(
        IReadOnlyList<DailyDeviceStat> dayRows,
        IReadOnlyDictionary<string, Device> devicesById
    )
    {
        return dayRows
            .Select(row =>
            {
                devicesById.TryGetValue(row.DeviceId, out var device);
                return new CalendarDeviceDetail
                {
                    DeviceId = row.DeviceId,
                    DeviceName = device?.DeviceName ?? row.DeviceId,
                    DeviceType = device?.DeviceType ?? DeviceTypes.Unknown,
                    IsConnected = false,
                    SessionCount = row.SessionCount,
                    ConnectionSeconds = row.ConnectionSeconds,
                    Keystrokes = row.Keystrokes,
                    MouseClicks = row.MouseClicks,
                    ActiveSeconds = row.ActiveMinutes * 60L,
                    HourlyInputBars = CalendarHourlyInputBarBuilder.Build(row.HourlyInputCount),
                };
            })
            .OrderByDescending(r => r.ConnectionSeconds)
            .ToList()
            .AsReadOnly();
    }

    private static CalendarDaySummary ToDaySummary(
        DateOnly day,
        IReadOnlyCollection<DailyDeviceStat>? dayRows,
        IReadOnlyDictionary<string, Device> devicesById
    )
    {
        if (dayRows == null || dayRows.Count == 0)
            return new CalendarDaySummary { Day = day, HasData = false };

        var tileDevices = dayRows
            .Select(r =>
            {
                devicesById.TryGetValue(r.DeviceId, out var device);
                return new CalendarTileDevice
                {
                    DeviceId = r.DeviceId,
                    DeviceName = device?.DeviceName ?? r.DeviceId,
                    DeviceType = device?.DeviceType ?? DeviceTypes.Unknown,
                };
            })
            .OrderBy(d =>
                d.DeviceType == DeviceTypes.Keyboard ? 0
                : d.DeviceType == DeviceTypes.Mouse ? 1
                : 2
            )
            .ThenBy(d => d.DeviceName)
            .ToList();

        return new CalendarDaySummary
        {
            Day = day,
            HasData = true,
            Devices = tileDevices,
        };
    }
}
