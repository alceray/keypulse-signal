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

    /// <summary>Devices for this day, ordered keyboards first then mice, alphabetically within each group.</summary>
    public IReadOnlyList<CalendarTileDevice> Devices { get; init; } = [];
}

/// <summary>Per-device breakdown for a single day. Used in the day detail panel.</summary>
public sealed class CalendarDeviceDetail
{
    public string DeviceId { get; init; } = "";
    public string DeviceName { get; init; } = "";
    public DeviceTypes DeviceType { get; init; } = DeviceTypes.Unknown;
    public string DeviceTypeText => DeviceType.ToString();

    // Connection
    public bool IsConnected { get; init; }
    public int SessionCount { get; init; }
    public long ConnectionSeconds { get; init; }
    public long LongestSessionSeconds { get; init; }

    // Activity
    public long Keystrokes { get; init; }
    public long MouseClicks { get; init; }
    public long MouseMovementSeconds { get; init; }
    public long MouseMovementDelta { get; init; } // UI-only, not persisted
    public long KeystrokeDelta { get; init; } // UI-only, not persisted
    public long MouseClickDelta { get; init; } // UI-only, not persisted
    public long LiveKeystrokes => Keystrokes + KeystrokeDelta;
    public long LiveMouseClicks => MouseClicks + MouseClickDelta;
    public long LiveMouseMovementSeconds => MouseMovementSeconds + MouseMovementDelta;
    public int ActiveMinutes { get; init; }
    public IReadOnlyList<long> HourlyInputCount { get; init; } = new long[24];

    public IReadOnlyList<CalendarHourlyInputBar> HourlyInputBars =>
        CalendarHourlyInputBarBuilder.Build(HourlyInputCount);
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

    public string Tooltip =>
        Total > 0
            ? $"{DateTime.Today.AddHours(Hour):h tt} - {Total:N0} inputs"
            : $"{DateTime.Today.AddHours(Hour):h tt} - No activity";
}
