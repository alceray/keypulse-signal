using KeyPulse.Models;

namespace KeyPulse.Tests.Models;

public class AppUserSettingsTests
{
    [Fact]
    public void Defaults()
    {
        var settings = new AppUserSettings();

        settings.IsFirstLaunch.ShouldBeTrue();
        settings.AutoInstallUpdates.ShouldBeTrue();

        // LaunchOnLogin default is build-config dependent (off in Debug so dev runs don't register autostart).
#if DEBUG
        settings.LaunchOnLogin.ShouldBeFalse();
#else
        settings.LaunchOnLogin.ShouldBeTrue();
#endif
    }
}
