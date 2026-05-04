using Microsoft.EntityFrameworkCore;
using Serilog;

namespace KeyPulse.Data;

internal static class AppMetaStore
{
    public static DateTime? ReadUtc(ApplicationDbContext ctx, string key)
    {
        try
        {
            using var connection = ctx.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MetaValue FROM AppMeta WHERE MetaKey = $key LIMIT 1;";
            var keyParam = cmd.CreateParameter();
            keyParam.ParameterName = "$key";
            keyParam.Value = key;
            cmd.Parameters.Add(keyParam);

            var value = cmd.ExecuteScalar() as string;
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read AppMeta key {MetaKey}", key);
        }

        return null;
    }

    public static void WriteUtc(ApplicationDbContext ctx, string key, DateTime value)
    {
        using var connection = ctx.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO AppMeta (MetaKey, MetaValue) VALUES ($key, $value) "
            + "ON CONFLICT(MetaKey) DO UPDATE SET MetaValue = excluded.MetaValue;";

        var keyParam = cmd.CreateParameter();
        keyParam.ParameterName = "$key";
        keyParam.Value = key;
        cmd.Parameters.Add(keyParam);

        var valueParam = cmd.CreateParameter();
        valueParam.ParameterName = "$value";
        valueParam.Value = value.ToString("O");
        cmd.Parameters.Add(valueParam);

        cmd.ExecuteNonQuery();
    }

    public static void Delete(ApplicationDbContext ctx, string key)
    {
        try
        {
            using var connection = ctx.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM AppMeta WHERE MetaKey = $key;";

            var keyParam = cmd.CreateParameter();
            keyParam.ParameterName = "$key";
            keyParam.Value = key;
            cmd.Parameters.Add(keyParam);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to clear AppMeta key {MetaKey}", key);
        }
    }
}
