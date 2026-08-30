using KeyPulse.Data;
using KeyPulse.Models;
using KeyPulse.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KeyPulse.Tests.Data;

/// <summary>
/// The store borrows the context's connection rather than owning it. Taking ownership silently aborted
/// transactions on SQLite and left PostgreSQL contexts permanently unusable, so these guard that.
/// </summary>
public class AppMetaStoreTests
{
    [Fact]
    public void ContextStaysUsableAfterMetaAccess()
    {
        using var db = new SqliteTestDatabase();
        using var ctx = db.CreateContext();

        AppMetaStore.EnsureTable(ctx);
        AppMetaStore.Write(ctx, "first", "1");
        AppMetaStore.WriteUtc(ctx, "second", new DateTime(2026, 4, 5, 6, 7, 8, DateTimeKind.Utc));

        AppMetaStore.ReadAll(ctx)["first"].ShouldBe("1");
        AppMetaStore.ReadUtc(ctx, "second").ShouldBe(new DateTime(2026, 4, 5, 6, 7, 8, DateTimeKind.Utc));

        // Entity work on the same context must still succeed after the raw meta commands.
        ctx.Devices.Add(new Device { DeviceId = "meta-device", DeviceName = "Keyboard" });
        ctx.SaveChanges();
        ctx.Devices.Count(x => x.DeviceId == "meta-device").ShouldBe(1);
    }

    [Fact]
    public void MetaWriteJoinsTheAmbientTransaction()
    {
        using var db = new SqliteTestDatabase();
        using var ctx = db.CreateContext();
        AppMetaStore.EnsureTable(ctx);

        using (var transaction = ctx.Database.BeginTransaction())
        {
            ctx.Devices.Add(new Device { DeviceId = "committed-device", DeviceName = "Mouse" });
            ctx.SaveChanges();
            AppMetaStore.Write(ctx, "committed-key", "kept");
            transaction.Commit();
        }

        ctx.ChangeTracker.Clear();
        ctx.Devices.Count(x => x.DeviceId == "committed-device").ShouldBe(1);
        AppMetaStore.ReadAll(ctx)["committed-key"].ShouldBe("kept");
    }

    [Fact]
    public void MetaWriteRollsBackWithTheAmbientTransaction()
    {
        using var db = new SqliteTestDatabase();
        using var ctx = db.CreateContext();
        AppMetaStore.EnsureTable(ctx);

        using (var transaction = ctx.Database.BeginTransaction())
        {
            ctx.Devices.Add(new Device { DeviceId = "discarded-device", DeviceName = "Mouse" });
            ctx.SaveChanges();
            AppMetaStore.Write(ctx, "discarded-key", "dropped");
            transaction.Rollback();
        }

        ctx.ChangeTracker.Clear();
        ctx.Devices.Count(x => x.DeviceId == "discarded-device").ShouldBe(0);
        AppMetaStore.ReadAll(ctx).ContainsKey("discarded-key").ShouldBeFalse();
    }

    [Fact]
    public void ReadAllCreatesTheTableWhenMissing()
    {
        using var db = new SqliteTestDatabase();
        using var ctx = db.CreateContext();

        AppMetaStore.ReadAll(ctx).ShouldBeEmpty();
    }
}
