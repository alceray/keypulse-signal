using KeyPulse.Models;

namespace KeyPulse.Tests.Models;

public class PostgreSqlConnectionSettingsTests
{
    [Fact]
    public void Describe_RendersTheConnectionTarget()
    {
        var settings = new PostgreSqlConnectionSettings
        {
            Host = "db.local",
            Port = 5544,
            Database = "keypulse_custom",
            Username = "keypulse_user",
        };

        settings.Describe().ShouldBe("keypulse_user@db.local:5544/keypulse_custom");
    }

    [Fact]
    public void Describe_OmitsTheSslMode()
    {
        var settings = new PostgreSqlConnectionSettings { SslMode = PostgreSqlSslMode.VerifyFull };

        settings.Describe().ShouldNotContain("VerifyFull");
    }
}
