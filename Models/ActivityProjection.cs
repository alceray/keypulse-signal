using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KeyPulse.Models;

/// <summary>
/// Exactly-once projection checkpoint for the minute-delayed activity projector.
/// One row per (DeviceId, Minute) that has been successfully applied to DailyDeviceStats.
/// Prevents double-applying the same snapshot across restarts or projector retries.
/// </summary>
[Table("ActivityProjections")]
public class ActivityProjection
{
    [Key]
    public int ActivityProjectionId { get; set; }

    /// <summary>Device this projection covers. Format: USB\VID_xxxx&amp;PID_xxxx</summary>
    [Required]
    public string DeviceId { get; set; } = "";

    /// <summary>The minute bucket (matches ActivitySnapshot.Minute) that was projected.</summary>
    [Required]
    public DateTime Minute { get; set; }

    /// <summary>When the projection was applied.</summary>
    public DateTime ProjectedAt { get; set; }
}
