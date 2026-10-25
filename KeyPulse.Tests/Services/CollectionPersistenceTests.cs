using System.IO;
using KeyPulse.Data;
using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KeyPulse.Tests.Services;

public class CollectionPersistenceTests
{
    [Fact]
    public async Task Sqlite_PersistenceAndRefreshContract()
    {
        using var db = new SqliteTestDatabase();
        await ExerciseContract(db.Factory);
    }

    [Fact]
    public void Sqlite_Constraints()
    {
        using var db = new SqliteTestDatabase();
        ExerciseConstraints(db.Factory);
    }

    internal static PartDescription Custom(string name = "Café") =>
        new(name, null, null, null, new SwitchPartDetails { Variants = ["45g", "55g"] });

    internal static async Task ExerciseContract(IDbContextFactory<ApplicationDbContext> source)
    {
        var factory = new FaultFactory(source);
        using var timer = new AppTimerService();
        using var daily = new DailyStatsService(factory, timer);
        using var databaseLock = new DatabaseInstanceLock();
        var catalog = CatalogServiceTests.Catalog();
        var data = new DataService(factory, daily, databaseLock, catalog);
        data.AddCustomCollectionItem(PartKinds.Switch, Custom(), 1, null).Succeeded.ShouldBeFalse();
        data.ReadCollectionItems().Succeeded.ShouldBeTrue();
        (await catalog.LoadAsync(PartKinds.Switch)).Status.ShouldBe(CatalogLoadStatus.Loaded);
        var added = data.AddCatalogCollectionItem(PartKinds.Switch, "sample", 70);
        added.Succeeded.ShouldBeTrue();
        var original = added.Items.Single();
        var increased = data.AddCatalogCollectionItem(PartKinds.Switch, "sample", 30).Items.Single();
        increased.Quantity.ShouldBe(100);
        increased.ItemKey.ShouldBe(original.ItemKey);
        increased.Revision.ShouldBe(2);
        data.AddCatalogCollectionItem(PartKinds.Switch, "sample", 10_000)
            .Status.ShouldBe(CollectionResultStatus.ValidationError);
        data.CollectionItems.Single().Quantity.ShouldBe(100);
        data.UpdateCollectionItem(original.ItemKey, original.Revision, 2, null)
            .Status.ShouldBe(CollectionResultStatus.Conflict);
        data.RemoveCollectionItem(original.ItemKey, original.Revision).Status.ShouldBe(CollectionResultStatus.Conflict);
        data.UpdateCollectionItem(increased.ItemKey, increased.Revision, 100, null).Items.Single().Revision.ShouldBe(2);
        data.UpdateCollectionItem(increased.ItemKey, increased.Revision, 100, "owned", Custom())
            .Status.ShouldBe(CollectionResultStatus.ValidationError);
        var edited = data.UpdateCollectionItem(increased.ItemKey, increased.Revision, 100, "owned").Items.Single();
        edited.Revision.ShouldBe(3);
        data.AddCustomCollectionItem(PartKinds.Switch, Custom(), 10, "gift").Succeeded.ShouldBeTrue();
        data.AddCustomCollectionItem(PartKinds.Switch, Custom("CAFE"), 1, null)
            .Status.ShouldBe(CollectionResultStatus.ValidationError);
        data.AddCustomCollectionItem(PartKinds.Switch, Custom("Other"), 1, null).Succeeded.ShouldBeTrue();
        data.AddCustomCollectionItem(PartKinds.Keycap, new("Café", null, null, null, new KeycapPartDetails()), 1, null)
            .Succeeded.ShouldBeTrue();
        data.AddCustomCollectionItem(PartKinds.Switch, Custom("Invalid"), 0, null)
            .Status.ShouldBe(CollectionResultStatus.ValidationError);
        data.AddCustomCollectionItem(PartKinds.Switch, Custom("Invalid"), 1, new string('x', 2001))
            .Status.ShouldBe(CollectionResultStatus.ValidationError);
        var custom = data.CollectionItems.Single(x => x.Name == "Café" && x.PartKind == PartKinds.Switch);
        using (var ctx = source.CreateDbContext())
        {
            var row = ctx.CollectionItems.Single(x => x.ItemKey == custom.ItemKey);
            row.DetailsJson = " \n" + row.DetailsJson.Replace(",", ", ");
            ctx.SaveChanges();
        }
        data.UpdateCollectionItem(custom.ItemKey, custom.Revision, 10, "gift", Custom())
            .Items.Single(x => x.ItemKey == custom.ItemKey)
            .Revision.ShouldBe(custom.Revision);
        data.UpdateCollectionItem(custom.ItemKey, custom.Revision, 10, "gift", Custom("Renamed"))
            .Succeeded.ShouldBeTrue();
        var updatedCatalog = CatalogServiceTests.Catalog(
            CatalogServiceTests.SwitchJson.Replace("Sample", "Updated").Replace("62g", "63g")
        );
        await updatedCatalog.LoadAsync(PartKinds.Switch);
        var refreshed = new DataService(factory, daily, databaseLock, updatedCatalog);
        refreshed.ReadCollectionItems().Succeeded.ShouldBeTrue();
        factory.FailAfterSave = true;
        refreshed.RefreshCollectionSnapshots(PartKinds.Switch).Status.ShouldBe(CollectionResultStatus.StorageFailure);
        refreshed.CollectionItems.Single(x => x.ItemKey == edited.ItemKey).Name.ShouldBe("Sample");
        using (var ctx = source.CreateDbContext())
            ctx.CollectionItems.Single(x => x.ItemKey == edited.ItemKey).Name.ShouldBe("Sample");
        factory.FailAfterSave = false;
        var after = refreshed
            .RefreshCollectionSnapshots(PartKinds.Switch)
            .Items.Single(x => x.ItemKey == edited.ItemKey);
        after.Name.ShouldBe("Updated");
        after.Quantity.ShouldBe(100);
        after.Notes.ShouldBe("owned");
        after.AddedAt.ShouldBe(edited.AddedAt);
        after.Revision.ShouldBe(4);
        refreshed
            .RefreshCollectionSnapshots(PartKinds.Switch)
            .Items.Single(x => x.ItemKey == edited.ItemKey)
            .Revision.ShouldBe(4);
        var empty = CatalogServiceTests.Catalog(CatalogServiceTests.EmptyJson);
        await empty.LoadAsync(PartKinds.Switch);
        var missing = new DataService(factory, daily, databaseLock, empty);
        missing.ReadCollectionItems();
        var retained = missing
            .RefreshCollectionSnapshots(PartKinds.Switch)
            .Items.Single(x => x.ItemKey == after.ItemKey);
        (retained with { Details = after.Details }).ShouldBe(after);
        PartDetailsCodec
            .Serialize(PartKinds.Switch, retained.Details!, true)
            .ShouldBe(PartDetailsCodec.Serialize(PartKinds.Switch, after.Details!, true));
        const string unreadable = "{ \"future\" : [1,2] }";
        using (var ctx = source.CreateDbContext())
        {
            var row = ctx.CollectionItems.Single(x => x.ItemKey == after.ItemKey);
            row.DetailsJson = unreadable;
            row.DetailsSchemaVersion = 99;
            ctx.SaveChanges();
        }
        refreshed.ReadCollectionItems().Succeeded.ShouldBeTrue();
        refreshed.CollectionItems.Single(x => x.ItemKey == after.ItemKey).DetailsError.ShouldNotBeNull();
        refreshed.RefreshCollectionSnapshots(PartKinds.Switch).Succeeded.ShouldBeTrue();
        refreshed
            .UpdateCollectionItem(after.ItemKey, after.Revision, 2, null)
            .Status.ShouldBe(CollectionResultStatus.ValidationError);
        refreshed
            .AddCatalogCollectionItem(PartKinds.Switch, "sample", 1)
            .Status.ShouldBe(CollectionResultStatus.ValidationError);
        using (var ctx = source.CreateDbContext())
            ctx.CollectionItems.Single(x => x.ItemKey == after.ItemKey).DetailsJson.ShouldBe(unreadable);
        refreshed.RemoveCollectionItem(after.ItemKey, after.Revision).Succeeded.ShouldBeTrue();
        refreshed.RemoveCollectionItem(after.ItemKey, after.Revision).Succeeded.ShouldBeTrue();
        factory.FailRead = true;
        refreshed.ReadCollectionItems().Status.ShouldBe(CollectionResultStatus.StorageFailure);
        refreshed.CollectionItems.Length.ShouldBe(3);
        factory.FailRead = false;
        refreshed.ReadCollectionItems().Items.Length.ShouldBe(3);
        await ExerciseQuantityLimits(source, databaseLock);
    }

    private static async Task ExerciseQuantityLimits(
        IDbContextFactory<ApplicationDbContext> factory,
        DatabaseInstanceLock databaseLock
    )
    {
        var catalog = new CatalogService(name =>
            CatalogServiceTests.StreamOf(
                name.Contains("switches")
                    ? CatalogServiceTests.SwitchJson.Replace("sample", "limit-test")
                    : """{"schemaVersion":1,"catalogVersion":1,"totalCount":1,"entries":[{"id":"limit-test","name":"Limit test"}]}"""
            )
        );
        using var timer = new AppTimerService();
        using var daily = new DailyStatsService(factory, timer);
        var data = new DataService(factory, daily, databaseLock, catalog);
        data.ReadCollectionItems().Succeeded.ShouldBeTrue();
        foreach (var (kind, maximum) in new[] { (PartKinds.Switch, 10_000), (PartKinds.Keycap, 100) })
        {
            (await catalog.LoadAsync(kind)).Status.ShouldBe(CatalogLoadStatus.Loaded);
            PartDescription description = new(
                "Quantity boundary",
                null,
                null,
                null,
                kind == PartKinds.Switch ? new SwitchPartDetails() : new KeycapPartDetails()
            );
            data.AddCustomCollectionItem(kind, description, maximum + 1, null)
                .Status.ShouldBe(CollectionResultStatus.ValidationError);
            data.AddCustomCollectionItem(kind, description, maximum, null).Succeeded.ShouldBeTrue();
            data.AddCatalogCollectionItem(kind, "limit-test", maximum + 1)
                .Status.ShouldBe(CollectionResultStatus.ValidationError);
            data.AddCatalogCollectionItem(kind, "limit-test", maximum - 1).Succeeded.ShouldBeTrue();
            data.AddCatalogCollectionItem(kind, "limit-test", 1).Succeeded.ShouldBeTrue();
            var before = data.CollectionItems;
            data.AddCatalogCollectionItem(kind, "limit-test", 1)
                .Status.ShouldBe(CollectionResultStatus.ValidationError);
            foreach (var item in before.Where(x => x.PartKind == kind && x.Quantity == maximum))
            {
                data.UpdateCollectionItem(item.ItemKey, item.Revision, maximum + 1, null)
                    .Status.ShouldBe(CollectionResultStatus.ValidationError);
                data.UpdateCollectionItem(item.ItemKey, item.Revision, 0, null)
                    .Status.ShouldBe(CollectionResultStatus.ValidationError);
            }
            data.CollectionItems.ShouldBe(before);
            data.ReadCollectionItems()
                .Items.Where(x => x.PartKind == kind && x.Quantity == maximum)
                .Count()
                .ShouldBe(2);
        }
    }

    internal static void ExerciseConstraints(IDbContextFactory<ApplicationDbContext> factory)
    {
        static CollectionItem Row() =>
            new()
            {
                PartKind = PartKinds.Switch,
                QuantityUnit = "Piece",
                Name = "Fixture",
                DetailsJson = "{}",
            };
        using (var ctx = factory.CreateDbContext())
        {
            ctx.CollectionItems.AddRange(Row(), Row());
            ctx.SaveChanges();
        }
        foreach (
            var invalid in new Action<CollectionItem>[]
            {
                x => x.CatalogEntryId = "",
                x => x.CatalogEntryId = "   ",
                x => x.Quantity = -1,
                x => x.Quantity = 1_000_001,
            }
        )
        {
            using var ctx = factory.CreateDbContext();
            var row = Row();
            invalid(row);
            ctx.Add(row);
            Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
        }
        using (var ctx = factory.CreateDbContext())
        {
            var row = Row();
            row.CatalogEntryId = "unique";
            ctx.Add(row);
            ctx.SaveChanges();
        }
        using (var ctx = factory.CreateDbContext())
        {
            var row = Row();
            row.CatalogEntryId = "unique";
            ctx.Add(row);
            Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
        }
        using (var ctx = factory.CreateDbContext())
        {
            var row = Row();
            row.ItemKey = ctx.CollectionItems.First().ItemKey;
            ctx.Add(row);
            Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
        }
        using (var ctx = factory.CreateDbContext())
        {
            var row = ctx.CollectionItems.First();
            row.Quantity = 0;
            Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
        }
    }

    internal sealed class FaultFactory(IDbContextFactory<ApplicationDbContext> inner)
        : IDbContextFactory<ApplicationDbContext>
    {
        internal bool FailRead { get; set; }
        internal bool FailAfterSave { get; set; }
        internal Action? AfterSave { get; set; }

        public ApplicationDbContext CreateDbContext()
        {
            if (FailRead)
                throw new IOException("Injected read failure");
            var context = inner.CreateDbContext();
            context.SavedChanges += (_, _) =>
            {
                AfterSave?.Invoke();
                if (FailAfterSave)
                    throw new IOException("Injected failure after SQL writes");
            };
            return context;
        }
    }
}

[Collection("PostgreSQL switches")]
public class CollectionPostgreSqlTests(PostgreSqlTestServer server)
{
    [PostgreSqlFact]
    public async Task PostgreSql_PersistenceAndRefreshContract()
    {
        var settings = await server.CreateDatabaseAsync();
        await server.CreateSchemaAsync(settings);
        await CollectionPersistenceTests.ExerciseContract(new Factory(() => server.Open(settings)));
    }

    [PostgreSqlFact]
    public async Task PostgreSql_Constraints()
    {
        var settings = await server.CreateDatabaseAsync();
        await server.CreateSchemaAsync(settings);
        CollectionPersistenceTests.ExerciseConstraints(new Factory(() => server.Open(settings)));
    }

    private sealed class Factory(Func<ApplicationDbContext> open) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => open();
    }
}
