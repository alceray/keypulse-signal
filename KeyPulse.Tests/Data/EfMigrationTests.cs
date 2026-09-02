using System.Data.Common;
using System.IO;
using KeyPulse.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KeyPulse.Tests.Data;

/// <summary>
/// Data-preservation coverage for EF schema migrations, which nothing else exercises. The shared
/// SqliteTestDatabase fixture builds its schema with EnsureCreated, so no migration runs there.
/// Not to be confused with DatabaseMigrationsTests, which covers the raw SQL data migrations.
/// </summary>
public sealed class EfMigrationTests
{
    // Anchored to the migration immediately before RemoveProjectedAt. A migration inserted between the
    // two would still pass, but would seed against a different schema than the one intended.
    private const string PreviousMigration = "20260619232033_AddDaysConnected";
    private const string RemoveProjectedAtMigration = "20260831080514_RemoveProjectedAt";

    [Fact]
    public void RemoveProjectedAt_PreservesProjectionRows_AndDownBackfillsMinute()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"keypulse-migration-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(
                    $"Data Source={databasePath}",
                    sqlite => sqlite.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name)
                )
                .Options;

            using var ctx = new ApplicationDbContext(options);
            var migrator = ctx.GetService<IMigrator>();
            migrator.Migrate(PreviousMigration);

            ctx.Database.ExecuteSqlRaw(
                """
                INSERT INTO "ActivityProjections" ("DeviceId", "Minute", "ProjectedAt")
                VALUES
                    ('USB\VID_0001&PID_0001', '2026-08-30 12:34:00', '2026-08-30 12:35:00'),
                    ('USB\VID_0002&PID_0002', '2026-08-30 13:45:00', '2026-08-30 13:46:00');
                """
            );

            var before = ReadProjectionRows(ctx.Database.GetDbConnection());

            migrator.Migrate(RemoveProjectedAtMigration);

            ReadColumnNames(ctx.Database.GetDbConnection()).ShouldNotContain("ProjectedAt");
            ReadProjectionRows(ctx.Database.GetDbConnection()).ShouldBe(before);

            migrator.Migrate(PreviousMigration);

            ReadColumnNames(ctx.Database.GetDbConnection()).ShouldContain("ProjectedAt");
            ReadProjectedAtPairs(ctx.Database.GetDbConnection()).ShouldAllBe(pair => pair.Minute == pair.ProjectedAt);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(databasePath);
            TryDelete(databasePath + "-wal");
            TryDelete(databasePath + "-shm");
        }
    }

    private static IReadOnlyList<ProjectionRow> ReadProjectionRows(DbConnection connection)
    {
        EnsureOpen(connection);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "ActivityProjectionId", "DeviceId", "Minute"
            FROM "ActivityProjections"
            ORDER BY "ActivityProjectionId";
            """;
        using var reader = command.ExecuteReader();
        var rows = new List<ProjectionRow>();
        while (reader.Read())
            rows.Add(new ProjectionRow(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
        return rows;
    }

    private static IReadOnlyList<string> ReadColumnNames(DbConnection connection)
    {
        EnsureOpen(connection);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('ActivityProjections');";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(1));
        return names;
    }

    private static IReadOnlyList<(string Minute, string ProjectedAt)> ReadProjectedAtPairs(DbConnection connection)
    {
        EnsureOpen(connection);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "Minute", "ProjectedAt"
            FROM "ActivityProjections"
            ORDER BY "ActivityProjectionId";
            """;
        using var reader = command.ExecuteReader();
        var rows = new List<(string Minute, string ProjectedAt)>();
        while (reader.Read())
            rows.Add((reader.GetString(0), reader.GetString(1)));
        return rows;
    }

    private static void EnsureOpen(DbConnection connection)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort; the OS eventually reclaims test temp files.
        }
    }

    private sealed record ProjectionRow(long ActivityProjectionId, string DeviceId, string Minute);
}
