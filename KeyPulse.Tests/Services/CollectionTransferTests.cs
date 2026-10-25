using System.IO;
using KeyPulse.Data;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KeyPulse.Tests.Services;

public class CollectionTransferTests
{
    [Fact]
    public async Task CollectionOnlyHistory_RequiresReplacement_AndFingerprintIgnoresGeneratedKey()
    {
        using var scope = new DatabaseSwitchTestScope();
        using var source = scope.CreateDatabase();
        using var target = scope.CreateDatabase(Path.Combine(scope.DirectoryPath, "target.db"));
        DatabaseSwitchTestScope.SeedCollection(source);
        DatabaseSwitchTestScope.SeedCollection(target);
        (await DatabaseSwitchService.HasApplicationDataAsync(source)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(
            () => DatabaseSwitchService.CopyHistoryAsync(source, target, "copy", false)
        );
        var fingerprint = await DatabaseHistoryFingerprint.ReadAsync(source);
        await DatabaseSwitchService.CopyHistoryAsync(source, target, "copy", true);
        (await DatabaseHistoryFingerprint.ReadAsync(target)).ShouldBe(fingerprint);
        source
            .CollectionItems.Select(x => x.CollectionItemId)
            .ToArray()
            .ShouldNotBe(target.CollectionItems.Select(x => x.CollectionItemId).ToArray());
    }

    [Fact]
    public async Task EveryCopiedCollectionField_AffectsFingerprint_IncludingRawJson()
    {
        using var scope = new DatabaseSwitchTestScope();
        using var context = scope.CreateDatabase();
        DatabaseSwitchTestScope.SeedCollection(context);
        var original = await DatabaseHistoryFingerprint.ReadAsync(context);
        foreach (
            var change in new Action<CollectionItem>[]
            {
                x => x.ItemKey = Guid.NewGuid(),
                x => x.PartKind = "Future",
                x => x.CatalogEntryId = "changed",
                x => x.Name += "!",
                x => x.Manufacturer += "!",
                x => x.Brand += "!",
                x => x.Designer += "!",
                x => x.DetailsSchemaVersion++,
                x => x.DetailsJson += " ",
                x => x.QuantityUnit = "FutureUnit",
                x => x.Revision++,
                x => x.Quantity++,
                x => x.Notes += "!",
                x => x.AddedAt = x.AddedAt.AddSeconds(1),
            }
        )
        {
            using var transaction = context.Database.BeginTransaction();
            context.ChangeTracker.Clear();
            change(context.CollectionItems.First());
            context.SaveChanges();
            (await DatabaseHistoryFingerprint.ReadAsync(context)).Collection.ShouldNotBe(original.Collection);
            transaction.Rollback();
        }
    }

    [Fact]
    public void SqliteUpgrade_PreservesExistingHistory_AndHasNoPendingModelChanges()
    {
        using var scope = new DatabaseSwitchTestScope();
        using var context = ConfiguredDbContextFactory.CreateSqliteContext(scope.SqlitePath, pooling: false);
        ExerciseUpgrade(context);
    }

    internal static void ExerciseUpgrade(ApplicationDbContext context)
    {
        var previous = context.Database.GetMigrations().Reverse().Skip(1).First();
        context.GetService<IMigrator>().Migrate(previous);
        context.Devices.Add(new Device { DeviceId = "upgrade-fixture", DeviceName = "Keep me" });
        context.SaveChanges();
        context.Database.Migrate();
        context.Devices.Single().DeviceName.ShouldBe("Keep me");
        context.CollectionItems.ShouldBeEmpty();
        context.Database.HasPendingModelChanges().ShouldBeFalse();
        DatabaseSwitchTestScope.SeedCollection(context);
        context.CollectionItems.Count().ShouldBe(3);
    }
}

[Collection("PostgreSQL switches")]
public class CollectionTransferPostgreSqlTests(PostgreSqlTestServer server)
{
    [PostgreSqlFact]
    public async Task CollectionOnly_RoundTripAcrossAllProviderRoutes()
    {
        using var scope = new DatabaseSwitchTestScope();
        var first = await server.CreateDatabaseAsync();
        var second = await server.CreateDatabaseAsync();
        DatabaseHistoryFingerprint expected;
        using (var local = scope.CreateDatabase())
        {
            DatabaseSwitchTestScope.SeedCollection(local);
            expected = await DatabaseHistoryFingerprint.ReadAsync(local);
        }
        scope.Credentials.Password = "";
        scope.Schedule(DatabaseProvider.Sqlite, DatabaseProvider.PostgreSql, first);
        await scope.Switch.ProcessPendingSwitchAsync();
        using (var target = server.Open(first))
            (await DatabaseHistoryFingerprint.ReadAsync(target)).ShouldBe(expected);
        await server.CreateSchemaAsync(second);
        using (var target = server.Open(second))
            DatabaseSwitchTestScope.SeedCollection(target);
        var connections = new DatabaseConnectionSettingsService(scope.Settings, scope.Credentials);
        connections.SchedulePostgreSql(scope.Settings.GetSettings(), second, "", true, false);
        await Should.ThrowAsync<InvalidOperationException>(() => scope.Switch.ProcessPendingSwitchAsync());
        var settings = scope.Settings.ReadPersistedSettings();
        settings.PendingDatabaseReplace = true;
        scope.Settings.SaveSettings(settings);
        await scope.Switch.ProcessPendingSwitchAsync();
        using (var target = server.Open(second))
            (await DatabaseHistoryFingerprint.ReadAsync(target)).ShouldBe(expected);
        scope.Schedule(DatabaseProvider.PostgreSql, DatabaseProvider.Sqlite, second);
        await scope.Switch.ProcessPendingSwitchAsync();
        using var exported = scope.OpenDatabase();
        (await DatabaseHistoryFingerprint.ReadAsync(exported)).ShouldBe(expected);
    }

    [PostgreSqlFact]
    public async Task PostgreSqlUpgrade_PreservesHistory_AndPendingSourceExplainsRecovery()
    {
        var settings = await server.CreateDatabaseAsync();
        using var context = server.Open(settings);
        var previous = context.Database.GetMigrations().Reverse().Skip(1).First();
        context.GetService<IMigrator>().Migrate(previous);
        var error = await Should.ThrowAsync<InvalidOperationException>(
            () => DatabaseSwitchService.ValidateSourceSchemaAsync(context, CancellationToken.None)
        );
        error.Message.ShouldContain("source database active");
        context.Database.GetPendingMigrations().Count().ShouldBe(1);
        CollectionTransferTests.ExerciseUpgrade(context);
    }
}
