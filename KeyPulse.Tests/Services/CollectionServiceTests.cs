using System.Collections.Concurrent;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.Tests.Infrastructure;

namespace KeyPulse.Tests.Services;

public class CollectionServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CachedState_RemainsReadableUntilCommitOrRollback(bool failAfterSave)
    {
        using var db = new SqliteTestDatabase();
        var factory = new CollectionPersistenceTests.FaultFactory(db.Factory);
        using var timer = new AppTimerService();
        using var daily = new DailyStatsService(factory, timer);
        using var databaseLock = new DatabaseInstanceLock();
        var catalogs = CatalogServiceTests.Catalog();
        var data = new DataService(factory, daily, databaseLock, catalogs);
        var service = new CollectionService(data, catalogs, _ => { });
        await service.InitializeAsync();
        (await service.AddCatalogAsync(PartKinds.Switch, "sample", 1)).Succeeded.ShouldBeTrue();
        var before = service.Items;
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        factory.FailAfterSave = failAfterSave;
        factory.AfterSave = () =>
        {
            saved.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Save was not released.");
        };
        var write = service.UpdateAsync(before.Single(), 2, "updated");
        try
        {
            await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var cached = await Task.Run(
                    () => (service.Items, service.IsLoaded, Owned: service.FindOwned(PartKinds.Switch, "sample"))
                )
                .WaitAsync(TimeSpan.FromSeconds(5));
            cached.Items.ShouldBe(before);
            cached.IsLoaded.ShouldBeTrue();
            cached.Owned.ShouldBeSameAs(before.Single());
            write.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            release.Set();
            await write;
        }
        (await write).Status.ShouldBe(
            failAfterSave ? CollectionResultStatus.StorageFailure : CollectionResultStatus.Success
        );
        if (failAfterSave)
            service.Items.ShouldBe(before);
        else
        {
            var committed = service.Items.Single();
            committed.Quantity.ShouldBe(2);
            committed.Notes.ShouldBe("updated");
            committed.Revision.ShouldBe(before.Single().Revision + 1);
        }
    }

    [Fact]
    public async Task ConcurrentWritesAndDelayedNotifications_ReadLatestCommittedState()
    {
        using var db = new SqliteTestDatabase();
        using var timer = new AppTimerService();
        using var daily = new DailyStatsService(db.Factory, timer);
        using var databaseLock = new DatabaseInstanceLock();
        var catalogs = CatalogServiceTests.Catalog();
        var data = new DataService(db.Factory, daily, databaseLock, catalogs);
        var notifications = new ConcurrentQueue<Action>();
        var service = new CollectionService(data, catalogs, notifications.Enqueue);
        service.IsLoaded.ShouldBeFalse();
        var load = service.LoadAsync();
        service.LoadAsync().ShouldBeSameAs(load);
        (await service.InitializeAsync()).Succeeded.ShouldBeTrue();
        var results = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => service.AddCatalogAsync(PartKinds.Switch, "sample", 5))
        );
        results.ShouldAllBe(x => x.Succeeded);
        service.FindOwned(PartKinds.Switch, "sample")!.Quantity.ShouldBe(100);
        service.FindOwned(PartKinds.Switch, "sample")!.Revision.ShouldBe(20);
        var seen = new List<int>();
        service.Changed += _ => seen.Add(service.Items.Single().Quantity);
        foreach (var notification in notifications.Reverse())
            notification();
        seen.ShouldNotBeEmpty();
        seen.ShouldAllBe(x => x == 100);
        var before = service.Items.Single();
        (await service.UpdateAsync(before, 101, "new")).Succeeded.ShouldBeTrue();
        (await service.UpdateAsync(before, 2, "stale")).Status.ShouldBe(CollectionResultStatus.Conflict);
        (await service.RemoveAsync(before)).Status.ShouldBe(CollectionResultStatus.Conflict);
        var pending = service.AddCustomAsync(PartKinds.Switch, CollectionPersistenceTests.Custom(), 1, null);
        await service.StopAndDrainAsync();
        (await pending).Succeeded.ShouldBeTrue();
        (await service.RemoveAsync(service.Items.First())).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task OwnedRowsLoadBeforeCatalogs_AndFailuresAllowSafeWritesAndRetry()
    {
        using var db = new SqliteTestDatabase();
        using var timer = new AppTimerService();
        using var daily = new DailyStatsService(db.Factory, timer);
        using var databaseLock = new DatabaseInstanceLock();
        var factory = new CollectionPersistenceTests.FaultFactory(db.Factory);
        var failCatalog = true;
        var catalogs = new CatalogService(name =>
            CatalogServiceTests.StreamOf(
                name.Contains("switches") && failCatalog ? "broken" : CatalogServiceTests.EmptyJson
            )
        );
        var data = new DataService(factory, daily, databaseLock, catalogs);
        var service = new CollectionService(data, catalogs, _ => { });
        factory.FailRead = true;
        (await service.InitializeAsync()).Status.ShouldBe(CollectionResultStatus.StorageFailure);
        service.IsLoaded.ShouldBeFalse();
        factory.FailRead = false;
        (await service.LoadAsync(true)).Succeeded.ShouldBeTrue();
        catalogs.GetState(PartKinds.Switch).Status.ShouldBe(CatalogLoadStatus.NotLoaded);
        await service.InitializeAsync();
        service.GetCatalogStatus(PartKinds.Switch).ShouldBe(CollectionCatalogStatus.CatalogUnavailable);
        service.GetCatalogStatus(PartKinds.Keycap).ShouldBe(CollectionCatalogStatus.Ready);
        (
            await service.AddCustomAsync(PartKinds.Switch, CollectionPersistenceTests.Custom(), 1, null)
        ).Succeeded.ShouldBeTrue();
        (await service.AddCatalogAsync(PartKinds.Switch, "sample", 1)).Succeeded.ShouldBeFalse();
        failCatalog = false;
        factory.FailAfterSave = true;
        await service.RefreshAsync(PartKinds.Switch, true);
        service.GetCatalogStatus(PartKinds.Switch).ShouldBe(CollectionCatalogStatus.RefreshFailed);
        factory.FailAfterSave = false;
        await service.RefreshAsync(PartKinds.Switch, true);
        service.GetCatalogStatus(PartKinds.Switch).ShouldBe(CollectionCatalogStatus.Ready);
        service.GetItemStatus(service.Items.Single()).ShouldBe("Custom");
    }
}
