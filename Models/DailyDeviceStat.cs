using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KeyPulse.Models;

/// <summary>
/// Day-level aggregate of device connection and activity metrics.
/// One row per (local calendar day, device). Updated by two paths:
///   - DeviceEvents: write-through on every lifecycle event.
///   - ActivitySnapshots: minute-delayed projector applied once per closed (DeviceId, Minute).
/// </summary>
[Table("DailyDeviceStats")]
public class DailyDeviceStat
{
    [Key]
    public int DailyDeviceStatId { get; set; }

    // Grain key ─────────────────────────────────────────────────────────────
    /// <summary>Local calendar day this row covers.</summary>
    [Required]
    public DateOnly Day { get; set; }

    /// <summary>Device this row covers. Format: USB\VID_xxxx&amp;PID_xxxx</summary>
    [Required]
    public string DeviceId { get; set; } = "";

    // Connection metrics (from DeviceEvents) ─────────────────────────────────
    /// <summary>Number of sessions with non-zero overlap on this local day.</summary>
    public int SessionCount { get; set; }

    /// <summary>Total seconds the device was connected on this local day, summed across all sessions.</summary>
    public long ConnectionSeconds { get; set; }

    /// <summary>Longest single session overlap on this local day (seconds).</summary>
    public long LongestSessionSeconds { get; set; }

    // Activity metrics (from ActivitySnapshots via minute projector) ──────────
    /// <summary>Total keystrokes across all projected minute snapshots for this day.</summary>
    public long Keystrokes { get; set; }

    /// <summary>Total mouse button-down events across all projected minute snapshots for this day.</summary>
    public long MouseClicks { get; set; }

    /// <summary>Total mouse movement seconds across all projected minute snapshots for this day.</summary>
    public long MouseMovementSeconds { get; set; }

    /// <summary>Number of distinct minutes with any activity on this day.</summary>
    public int ActiveMinutes { get; set; }

    /// <summary>
    /// Combined input count by local clock-hour (index 0-23).
    /// Value = keystrokes + mouse clicks + mouse movement seconds for that hour.
    /// </summary>
    public long[] HourlyInputCount { get; set; } = new long[24];

    /// <summary>When this row was last written.</summary>
    public DateTime UpdatedAt { get; set; }
}
