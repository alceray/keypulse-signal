namespace KeyPulse.Models;

public enum DatabaseProvider
{
    Sqlite,
    PostgreSql,
}

public enum PostgreSqlSslMode
{
    Prefer,
    Require,
    VerifyFull,
    Disable,
}

public sealed class PostgreSqlConnectionSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = BuildInfo.IsDebug ? "keypulse_signal_test" : "keypulse_signal";
    public string Username { get; set; } = "keypulse";
    public PostgreSqlSslMode SslMode { get; set; } = PostgreSqlSslMode.Prefer;

    public PostgreSqlConnectionSettings Copy() =>
        new()
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            SslMode = SslMode,
        };
}

public static class BuildInfo
{
#if DEBUG
    public const bool IsDebug = true;
    public const string EnvironmentName = "Debug";
#else
    public const bool IsDebug = false;
    public const string EnvironmentName = "Release";
#endif
}
