namespace KeyPulse.ViewModels.Calendar;

/// <summary>Lightweight device entry shown on a calendar day tile.</summary>
public sealed class CalendarTileDevice
{
    public string DeviceId { get; init; } = "";
    public string DeviceName { get; init; } = "";
    public string DeviceType { get; init; } = "";

    public string TypeIcon =>
        DeviceType switch
        {
            "Keyboard" => "⌨",
            "Mouse" => "🖱",
            _ => "?",
        };
}

/// <summary>Day-level summary across all devices. Used to populate calendar grid tiles.</summary>
public sealed class CalendarDaySummary
{
    public DateOnly Day { get; init; }
    public bool IsToday { get; init; }
    public bool HasData { get; init; }
    public bool IsSelected { get; init; }

    /// <summary>Devices for this day, ordered keyboards first then mice, alphabetically within each group.</summary>
    public IReadOnlyList<CalendarTileDevice> Devices { get; init; } = [];
}

/// <summary>Per-device breakdown for a single day. Used in the day detail panel.</summary>
public sealed class CalendarDeviceDetail
{
    public string DeviceId { get; init; } = "";
    public string DeviceName { get; init; } = "";
    public string DeviceType { get; init; } = "";

    // Connection
    public int SessionCount { get; init; }
    public long ConnectionDuration { get; init; } // seconds
    public long LongestSessionDuration { get; init; } // seconds

    // Activity
    public long Keystrokes { get; init; }
    public long MouseClicks { get; init; }
    public long MouseMovementSeconds { get; init; }
    public long LiveInputDelta { get; init; } // UI-only, not persisted
    public long TotalInput => Keystrokes + MouseClicks + MouseMovementSeconds + LiveInputDelta;
    public int ActiveMinutes { get; init; }
    public int DistinctActiveHours { get; init; }
    public int PeakInputHour { get; init; }
    public string? PeakInputHourDisplay => PeakInputHour >= 0 ? $"{PeakInputHour:D2}:00" : null;
}
