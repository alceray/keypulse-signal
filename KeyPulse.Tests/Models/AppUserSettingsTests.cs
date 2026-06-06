using System.Text.Json;
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
        settings.ActivityRetentionMonths.ShouldBe(0); // 0 = keep forever

        // LaunchOnLogin default is build-config dependent (off in Debug so dev runs don't register autostart).
#if DEBUG
        settings.LaunchOnLogin.ShouldBeFalse();
#else
        settings.LaunchOnLogin.ShouldBeTrue();
#endif
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutRetentionKey_KeepsForever()
    {
        // A settings.json written before ActivityRetentionMonths existed.
        const string legacyJson = """{"LaunchOnLogin":true,"IsFirstLaunch":false,"AutoInstallUpdates":false}""";

        var settings = JsonSerializer.Deserialize<AppUserSettings>(legacyJson)!;

        settings.LaunchOnLogin.ShouldBeTrue();
        settings.IsFirstLaunch.ShouldBeFalse();
        settings.AutoInstallUpdates.ShouldBeFalse();
        settings.ActivityRetentionMonths.ShouldBe(0);
    }

    [Fact]
    public void SerializeRoundTrip_PreservesAllFields()
    {
        var settings = new AppUserSettings
        {
            LaunchOnLogin = true,
            IsFirstLaunch = false,
            AutoInstallUpdates = false,
            ActivityRetentionMonths = 6,
        };

        var roundTripped = JsonSerializer.Deserialize<AppUserSettings>(JsonSerializer.Serialize(settings))!;

        roundTripped.LaunchOnLogin.ShouldBeTrue();
        roundTripped.IsFirstLaunch.ShouldBeFalse();
        roundTripped.AutoInstallUpdates.ShouldBeFalse();
        roundTripped.ActivityRetentionMonths.ShouldBe(6);
    }
}
