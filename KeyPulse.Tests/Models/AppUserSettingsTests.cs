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
        settings.CloseToTray.ShouldBeTrue();
        settings.SuppressCloseToTrayHint.ShouldBeFalse(); // reminder shows until the user opts out
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
    public void Deserialize_JsonWithoutCloseToTrayKey_DefaultsToTrue()
    {
        // A settings.json written before CloseToTray existed should keep the run-in-background default.
        const string legacyJson = """{"LaunchOnLogin":true,"IsFirstLaunch":false,"AutoInstallUpdates":true}""";

        var settings = JsonSerializer.Deserialize<AppUserSettings>(legacyJson)!;

        settings.CloseToTray.ShouldBeTrue();
    }

    [Fact]
    public void SerializeRoundTrip_PreservesAllFields()
    {
        var settings = new AppUserSettings
        {
            LaunchOnLogin = true,
            IsFirstLaunch = false,
            AutoInstallUpdates = false,
            CloseToTray = false,
            SuppressCloseToTrayHint = true,
            ActivityRetentionMonths = 6,
        };

        var roundTripped = JsonSerializer.Deserialize<AppUserSettings>(JsonSerializer.Serialize(settings))!;

        roundTripped.LaunchOnLogin.ShouldBeTrue();
        roundTripped.IsFirstLaunch.ShouldBeFalse();
        roundTripped.AutoInstallUpdates.ShouldBeFalse();
        roundTripped.CloseToTray.ShouldBeFalse();
        roundTripped.SuppressCloseToTrayHint.ShouldBeTrue();
        roundTripped.ActivityRetentionMonths.ShouldBe(6);
    }
}
