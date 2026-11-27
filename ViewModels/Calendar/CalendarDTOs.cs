using KeyPulse.Configuration;
using KeyPulse.Helpers;
using KeyPulse.Models;

namespace KeyPulse.ViewModels.Calendar;

/// <summary>Lightweight device entry shown on a calendar day tile.</summary>
public sealed class CalendarTileDevice
{
    public string DeviceId { get; init; } = "";
    public string DeviceName { get; init; } = "";
    public DeviceTypes DeviceType { get; init; } = DeviceTypes.Unknown;
    public bool IsConnected { get; init; }

    public string TypeIcon =>
        DeviceType switch
        {
            DeviceTypes.Keyboard => "⌨",
            DeviceTypes.Mouse => "🖱",
            _ => "?",
        };
}

/// <summary>Day-level summary across all devices. Used to populate calendar grid tiles.</summary>
public sealed class CalendarDaySummary
{
    public DateOnly Day { get; init; }
    public bool HasData { get; init; }
    public bool IsSelected { get; init; }

    /// <summary>Computed live comparison: always returns true if Day equals today.</summary>
    public bool IsToday => Day == DateOnly.FromDateTime(DateTime.Now);
    public bool IsSelectable => HasData || IsToday;

    /// <summary>Devices for this day, ordered keyboards first then mice, alphabetically within each group.</summary>
    public IReadOnlyList<CalendarTileDevice> Devices { get; init; } = [];
}

/// <summary>Per-device breakdown for a single day. Used in the day detail panel.</summary>
public sealed record CalendarDeviceDetail
{
    public IReadOnlyList<CalendarBuildRow> Builds { get; init; } = [];
    public string BuildsMessage { get; init; } = "";
    public string DeviceId { get; init; } = "";
    public string DeviceName { get; init; } = "";
    public DeviceTypes DeviceType { get; init; } = DeviceTypes.Unknown;
    public string DeviceTypeText => DeviceType.ToString();

    // Connection
    public bool IsConnected { get; init; }
    public int SessionCount { get; init; }
    public long ConnectionSeconds { get; init; }

    // Activity
    public long Keystrokes { get; init; }
    public long MouseClicks { get; init; }

    // Seconds the device saw input. Built from per-minute active-second counts; today's row is overlaid
    // with a live total while the app runs.
    public long ActiveSeconds { get; init; }

    /// <summary>True for the current local day, whose connected/active totals still tick. Settled past
    /// days drop their seconds so a frozen value isn't shown at misleading precision.</summary>
    public bool IsToday { get; init; }

    public long AppRunningSeconds { get; init; }
    public double ConnectedPercent => RunningPercent(ConnectionSeconds);
    public double ActivePercent => RunningPercent(ActiveSeconds);

    public string ConnectedTimeText => FormatRunningShare(ConnectionSeconds, ConnectedPercent);
    public string ActiveTimeText => FormatRunningShare(ActiveSeconds, ActivePercent);
    public string ConnectedTimeToolTip => FormatTimeToolTip("Connected for", ConnectionSeconds);
    public string ActiveTimeToolTip => FormatTimeToolTip("Recorded input for", ActiveSeconds);

    private string FormatTimeToolTip(string action, long seconds)
    {
        var duration = TimeFormatter.FormatDuration(TimeSpan.FromSeconds(seconds));
        if (AppRunningSeconds <= 0)
            return $"{action} {duration}. No running time was recorded for {AppConstants.App.ShortName} that day.";

        var runtime = TimeFormatter.FormatDuration(TimeSpan.FromSeconds(AppRunningSeconds));
        return $"{action} {duration} of the {runtime} {AppConstants.App.ShortName} ran that day.";
    }

    private double RunningPercent(long seconds) =>
        AppRunningSeconds > 0 ? Math.Clamp(100.0 * seconds / AppRunningSeconds, 0, 100) : 0;

    private string FormatRunningShare(long seconds, double percent)
    {
        var duration = TimeFormatter.FormatDuration(TimeSpan.FromSeconds(seconds), includeSeconds: IsToday);
        return AppRunningSeconds > 0 ? $"{duration} · {Math.Round(percent)}%" : duration;
    }

    public IReadOnlyList<CalendarHourlyInputBar> HourlyInputBars { get; init; } =
        CalendarHourlyInputBarBuilder.Build(new long[24]);
}

public sealed class CalendarHourlyInputBar
{
    public int Hour { get; init; }
    public long Total { get; init; }
    public double BarHeight { get; init; }
    public bool IsPeak { get; init; }

    /// <summary>Always-visible major anchor labels: 12am and 12pm.</summary>
    public string MajorLabel =>
        Hour switch
        {
            0 => "12am",
            12 => "12pm",
            _ => "",
        };

    /// <summary>Intermediate labels shown only when the chart is wide enough: 4am, 8am, 4pm, 8pm.</summary>
    public string MinorLabel =>
        Hour switch
        {
            4 => "4am",
            8 => "8am",
            16 => "4pm",
            20 => "8pm",
            _ => "",
        };

    public string Tooltip => $"{DateTime.Today.AddHours(Hour):h tt} - {Total:N0} inputs";
}
