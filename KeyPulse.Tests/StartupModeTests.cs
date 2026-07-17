namespace KeyPulse.Tests;

public class StartupModeTests
{
    [Theory]
    [InlineData(true, System.Windows.ShutdownMode.OnExplicitShutdown)]
    [InlineData(false, System.Windows.ShutdownMode.OnMainWindowClose)]
    public void ResolveShutdownMode_MatchesStartupMode(
        bool runInBackground,
        System.Windows.ShutdownMode expected
    )
    {
        App.ResolveShutdownMode(runInBackground).ShouldBe(expected);
    }

    [Theory]
    [InlineData("--tray")]
    [InlineData("--TRAY")]
    [InlineData("--Tray")]
    public void ShouldForceTrayFromArgs_WithTrayArg_ReturnsTrue(string arg)
    {
        App.ShouldForceTrayFromArgs(new[] { arg }).ShouldBeTrue();
    }

    [Fact]
    public void ShouldForceTrayFromArgs_TrayArgAmongOthers_ReturnsTrue()
    {
        App.ShouldForceTrayFromArgs(new[] { "--other", "--tray", "value" }).ShouldBeTrue();
    }

    [Fact]
    public void ShouldForceTrayFromArgs_NoArgs_ReturnsFalse()
    {
        App.ShouldForceTrayFromArgs(Array.Empty<string>()).ShouldBeFalse();
    }

    [Fact]
    public void ShouldForceTrayFromArgs_UnrelatedArgs_ReturnsFalse()
    {
        // The legacy --startup arg was renamed to --tray and must no longer force tray mode.
        App.ShouldForceTrayFromArgs(new[] { "--startup", "--foo" }).ShouldBeFalse();
    }
}
