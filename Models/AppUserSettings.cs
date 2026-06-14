namespace KeyPulse.Models;

public class AppUserSettings
{
#if DEBUG
    public bool LaunchOnLogin { get; set; }
#else
    public bool LaunchOnLogin { get; set; } = true;
#endif
    public bool IsFirstLaunch { get; set; } = true;
    public bool AutoInstallUpdates { get; set; } = true;

    /// <summary>When true, closing the window keeps the app running in the tray instead of exiting.</summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>Set when the user opts out of the close-to-tray reminder via its "don't show again" checkbox.</summary>
    public bool SuppressCloseToTrayHint { get; set; }

    /// <summary>Months of per-minute activity detail to keep; 0 keeps everything forever.</summary>
    public int ActivityRetentionMonths { get; set; }
}
