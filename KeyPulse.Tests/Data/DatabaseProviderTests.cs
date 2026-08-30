using KeyPulse.Data;
using KeyPulse.Models;
using KeyPulse.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KeyPulse.Tests.Data;

public class DatabaseProviderTests
{
    [Fact]
    public void PostgreSqlConnectionString_UsesStructuredSettings()
    {
        var settings = new PostgreSqlConnectionSettings
        {
            Host = "db.internal",
            Port = 5544,
            Database = "keypulse_test",
            Username = "collector",
            SslMode = PostgreSqlSslMode.VerifyFull,
        };

        var connectionString = DatabaseConfigurationService.BuildPostgreSqlConnectionString(settings, "secret");
        var parsed = new NpgsqlConnectionStringBuilder(connectionString);

        parsed.Host.ShouldBe("db.internal");
        parsed.Port.ShouldBe(5544);
        parsed.Database.ShouldBe("keypulse_test");
        parsed.Username.ShouldBe("collector");
        parsed.Password.ShouldBe("secret");
        parsed.SslMode.ShouldBe(SslMode.VerifyFull);
        parsed.IncludeErrorDetail.ShouldBeFalse();
    }

    [Fact]
    public void PostgreSqlConnectionString_RejectsInvalidPort()
    {
        var settings = new PostgreSqlConnectionSettings { Port = 0 };

        Should.Throw<ArgumentOutOfRangeException>(
            () => DatabaseConfigurationService.BuildPostgreSqlConnectionString(settings, "secret")
        );
    }

    [Fact]
    public void PostgreSqlContext_UsesIndependentMigrationSet()
    {
        var settings = new PostgreSqlConnectionSettings();
        using var context = ConfiguredDbContextFactory.CreatePostgreSqlContext(settings, "design-only");

        context.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
        var migrations = context.Database.GetMigrations().ToArray();
        migrations.ShouldContain(x => x.EndsWith("_PostgreSqlInitial", StringComparison.Ordinal));
        migrations.ShouldNotContain(x => x.Contains("CreateDevices", StringComparison.Ordinal));
    }
}
