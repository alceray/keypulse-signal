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
}
