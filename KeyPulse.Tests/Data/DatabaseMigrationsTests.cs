using System.Data;
using KeyPulse.Data;
using KeyPulse.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KeyPulse.Tests.Data;

/// <summary>
/// Asserts what the one-time data migrations actually DO (not just that they run). The UTC-conversion
/// pass is timezone-dependent, so these tests pre-mark it done and exercise the timezone-independent
/// second-precision pass: sub-second truncation plus the dedup/merge that protects the unique indexes.
/// Rows are seeded with raw SQL (bypassing the EF converters) to simulate legacy data, and read back
/// raw so assertions don't depend on the machine timezone.
/// </summary>
public class DatabaseMigrationsTests : IDisposable
{
    private const string UtcMigrationMarkerKey = "UtcTimestampMigrationV1";

    private readonly SqliteTestDatabase _db = new();

    public DatabaseMigrationsTests()
    {
        _db.EnsureAppMetaTable();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void SecondPrecisionMigration_TruncatesAndDedupsCollidingDeviceEvents()
    {
        using var ctx = _db.CreateContext();
        MarkUtcMigrationDone(ctx);

        // Two events identical except sub-second precision -> collide once truncated to the second.
        ExecSql(
            ctx,
            "INSERT INTO DeviceEvents (DeviceId, EventTime, EventType) VALUES ('D1','2026-05-20 09:00:00.123','Connected');"
        );
        ExecSql(
            ctx,
            "INSERT INTO DeviceEvents (DeviceId, EventTime, EventType) VALUES ('D1','2026-05-20 09:00:00.456','Connected');"
        );

        DatabaseMigrations.RunAll(ctx);

        Scalar(ctx, "SELECT COUNT(*) FROM DeviceEvents;").ShouldBe(1L); // deduped
        Scalar(ctx, "SELECT EventTime FROM DeviceEvents;").ShouldBe("2026-05-20 09:00:00"); // truncated
    }

    [Fact]
    public void SecondPrecisionMigration_MergesCollidingActivitySnapshots()
    {
        using var ctx = _db.CreateContext();
        MarkUtcMigrationDone(ctx);

        // Same minute after truncation -> merged: Keystrokes/MouseClicks summed, MouseMovementSeconds maxed.
        ExecSql(
            ctx,
            "INSERT INTO ActivitySnapshots (DeviceId, Minute, Keystrokes, MouseClicks, MouseMovementSeconds) "
                + "VALUES ('D1','2026-05-20 09:00:00.100', 3, 1, 10);"
        );
        ExecSql(
            ctx,
            "INSERT INTO ActivitySnapshots (DeviceId, Minute, Keystrokes, MouseClicks, MouseMovementSeconds) "
                + "VALUES ('D1','2026-05-20 09:00:00.900', 4, 2, 20);"
        );

        DatabaseMigrations.RunAll(ctx);

        Scalar(ctx, "SELECT COUNT(*) FROM ActivitySnapshots;").ShouldBe(1L);
        Scalar(ctx, "SELECT Keystrokes FROM ActivitySnapshots;").ShouldBe(7L); // 3 + 4
        Scalar(ctx, "SELECT MouseClicks FROM ActivitySnapshots;").ShouldBe(3L); // 1 + 2
        Scalar(ctx, "SELECT MouseMovementSeconds FROM ActivitySnapshots;").ShouldBe(20L); // max(10, 20)
    }

    [Fact]
    public void RunAll_IsIdempotent_SecondRunIsANoOp()
    {
        using var ctx = _db.CreateContext();
        MarkUtcMigrationDone(ctx);
        ExecSql(
            ctx,
            "INSERT INTO DeviceEvents (DeviceId, EventTime, EventType) VALUES ('D1','2026-05-20 09:00:00.123','Connected');"
        );

        DatabaseMigrations.RunAll(ctx);
        DatabaseMigrations.RunAll(ctx); // marker now set => no further changes

        Scalar(ctx, "SELECT COUNT(*) FROM DeviceEvents;").ShouldBe(1L);
        Scalar(ctx, "SELECT EventTime FROM DeviceEvents;").ShouldBe("2026-05-20 09:00:00");
    }

    private static void MarkUtcMigrationDone(ApplicationDbContext ctx) =>
        ExecSql(ctx, $"INSERT INTO AppMeta (MetaKey, MetaValue) VALUES ('{UtcMigrationMarkerKey}','done');");

    private static void ExecSql(ApplicationDbContext ctx, string sql) => ctx.Database.ExecuteSqlRaw(sql);

    private static object? Scalar(ApplicationDbContext ctx, string sql)
    {
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }
}
