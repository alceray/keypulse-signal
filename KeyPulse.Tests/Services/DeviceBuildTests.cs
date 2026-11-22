using System.IO;
using System.Text.Json;
using KeyPulse.Data;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KeyPulse.Tests.Services;

public class DeviceBuildTests
{
    [Fact]
    public void Sqlite_DailyBuildContract()
    {
        using var db = new SqliteTestDatabase();
        ExerciseContract(db.Factory);
    }

    internal static void ExerciseContract(IDbContextFactory<ApplicationDbContext> factory)
    {
        using var scope = new BuildScope(factory);
        var data = scope.Data;
        var id = scope.Device.DeviceId;
        var date = new DateOnly(2026, 3, 8);
        var a = scope.First.ItemKey;
        var b = scope.Second.ItemKey;
        DeviceBuildDaySnapshot Day(DateOnly? day = null) =>
            data.ReadDeviceBuildDays().Single(x => x.Day == (day ?? date));
        BuildResult Add(params Guid[] keys) => data.SaveDeviceBuild(id, date, Day(), null, keys, date);

        data.InitializeDeviceBuildDay(id, date).Changed.ShouldBeTrue();
        var initialized = Day();
        initialized.Builds.ShouldBeEmpty();
        initialized.Revision.ShouldBe(1);
        data.InitializeDeviceBuildDay(id, date).Changed.ShouldBeFalse();
        Add(a, a).Status.ShouldBe(BuildResultStatus.ValidationError);
        Add(Guid.NewGuid()).Status.ShouldBe(BuildResultStatus.ValidationError);
        Add(a).Changed.ShouldBeTrue();
        var first = Day().Current!;
        first.Parts.Single().Variant.ShouldBe("Base kit");
        data.SaveDeviceBuild(id, date, initialized, null, [b], date).Status.ShouldBe(BuildResultStatus.Conflict);
        data.SaveDeviceBuild(id, date, null, null, [b], date).Status.ShouldBe(BuildResultStatus.Conflict);
        Add(a).Changed.ShouldBeFalse();
        Add(a, b).Changed.ShouldBeTrue();
        var second = Day().Current!;
        Add(b, a).Changed.ShouldBeFalse();
        Add(b).Changed.ShouldBeTrue();
        var third = Day().Current!;
        Day().Builds.Select(x => x.BuildKey).ShouldBe([first.BuildKey, second.BuildKey, third.BuildKey]);

        var tomorrow = date.AddDays(1);
        data.InitializeDeviceBuildDay(id, tomorrow).Changed.ShouldBeTrue();
        var copied = Day(tomorrow).Current!;
        copied.BuildKey.ShouldNotBe(third.BuildKey);
        copied.Parts.ShouldBe(third.Parts);
        data.SaveDeviceBuild(id, date, Day(), first.BuildKey, [b], tomorrow).Changed.ShouldBeTrue();
        Day().Builds[0].BuildKey.ShouldBe(first.BuildKey);
        Day().Builds[0].Parts.ShouldBe(third.Parts);
        Day().Current!.BuildKey.ShouldBe(third.BuildKey);
        Day(tomorrow).Revision.ShouldBe(1);
        Day(tomorrow).Current!.Parts.ShouldBe(copied.Parts);

        using (var context = factory.CreateDbContext())
        {
            context.CollectionItems.Single(x => x.ItemKey == a).Name = "Renamed";
            context.CollectionItems.Remove(context.CollectionItems.Single(x => x.ItemKey == b));
            context.SaveChanges();
        }
        scope.SetConnected(false);
        data.SaveDeviceBuild(id, tomorrow, Day(tomorrow), null, [a], tomorrow)
            .Status.ShouldBe(BuildResultStatus.ValidationError);
        data.InitializeDeviceBuildDay(id, tomorrow.AddDays(1)).Status.ShouldBe(BuildResultStatus.ValidationError);
        data.SaveDeviceBuild(id, date, Day(), third.BuildKey, [a, b], tomorrow).Changed.ShouldBeTrue();
        Day().Current!.Parts.Single(x => x.ItemKey == b).Name.ShouldBe(scope.Second.Name);
        Day().Current!.Parts.Single(x => x.ItemKey == a).Name.ShouldBe("Renamed");
        Day().Builds[1].Parts.Single(x => x.ItemKey == a).Name.ShouldBe(scope.First.Name);
        data.SaveDeviceBuild(id, date, Day(), first.BuildKey, [], tomorrow).Changed.ShouldBeTrue();
        Day().Builds[0].Summary.ShouldBe("No parts assigned");
        var stale = Day();
        data.DeleteDeviceBuild(Day(), second.BuildKey).Changed.ShouldBeTrue();
        data.DeleteDeviceBuild(stale, first.BuildKey).Status.ShouldBe(BuildResultStatus.Conflict);
        data.SaveDeviceBuild(id, date, Day(), second.BuildKey, [a], tomorrow)
            .Status.ShouldBe(BuildResultStatus.Conflict);
        data.DeleteDeviceBuild(Day(), third.BuildKey).Changed.ShouldBeTrue();
        Day().Current!.BuildKey.ShouldBe(first.BuildKey);
        data.DeleteDeviceBuild(Day(), first.BuildKey).Changed.ShouldBeTrue();
        Day().Builds.ShouldBeEmpty();
        Day().DayKey.ShouldBe(initialized.DayKey);
        data.DeleteDeviceBuild(Day(tomorrow), copied.BuildKey).Changed.ShouldBeTrue();
        scope.SetConnected(true);
        data.InitializeDeviceBuildDay(id, date).Changed.ShouldBeFalse();
        data.InitializeDeviceBuildDay(id, tomorrow).Changed.ShouldBeFalse();
        data.InitializeDeviceBuildDay(id, tomorrow.AddDays(1)).Changed.ShouldBeTrue();
        Day(tomorrow.AddDays(1)).Current.ShouldBeNull();
        data.SaveDeviceBuild(id, date, Day(), null, [a], tomorrow).Status.ShouldBe(BuildResultStatus.ValidationError);

        using (var context = factory.CreateDbContext())
        {
            context.DailyDeviceStats.Count().ShouldBe(0);
            context.DeviceEvents.Count().ShouldBe(0);
            context.ActivitySnapshots.Count().ShouldBe(0);
        }
        var beforeRebuild = data.ReadDeviceBuildDays().Select(x => (x.DayKey, x.Day, x.Revision)).ToArray();
        scope.Daily.RecomputeDailyDeviceStatsForRange(date, tomorrow.AddDays(1));
        data.ReadDeviceBuildDays().Select(x => (x.DayKey, x.Day, x.Revision)).ShouldBe(beforeRebuild);
        var beforeCorrupt = Day();
        using (var context = factory.CreateDbContext())
        {
            var raw = context.DeviceBuildDays.Single(x => x.Day == date);
            raw.BuildsSchemaVersion = 99;
            raw.BuildsJson = " future payload ";
            context.SaveChanges();
        }
        Should.Throw<JsonException>(() => data.ReadDeviceBuildDays());
        data.SaveDeviceBuild(id, date, beforeCorrupt, first.BuildKey, [a], tomorrow)
            .Status.ShouldBe(BuildResultStatus.StorageFailure);
        using var verify = factory.CreateDbContext();
        verify.DeviceBuildDays.Single(x => x.Day == date).BuildsJson.ShouldBe(" future payload ");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[{\"BuildKey\":\"00000000-0000-0000-0000-000000000001\"}]")]
    public void UnreadableJson_IsRejected(string json) =>
        Should.Throw<JsonException>(() => DeviceBuildJson.Snapshot(new() { BuildsJson = json }));

    [Fact]
    public void Json_ValidatesKeysAndParts_PreservesBuildOrder_AndSortsPartsOnly()
    {
        var a = new BuildPartSnapshot(Guid.NewGuid(), PartKinds.Switch, "Switch", "45g");
        var b = new BuildPartSnapshot(Guid.NewGuid(), PartKinds.Keycap, "Keycap", "Base");
        var first = new DeviceBuildSnapshot(Guid.NewGuid(), [a, b]);
        var second = new DeviceBuildSnapshot(Guid.NewGuid(), []);
        DeviceBuildJson.Write([first]).ShouldBe(DeviceBuildJson.Write([first with { Parts = [b, a] }]));
        DeviceBuildJson.Write([first, second]).ShouldNotBe(DeviceBuildJson.Write([second, first]));
        foreach (
            var invalid in new[]
            {
                first with
                {
                    BuildKey = Guid.Empty,
                },
                first with
                {
                    Parts = [a, a],
                },
                first with
                {
                    Parts = [a with { PartKind = "Future" }],
                },
                first with
                {
                    Parts = [a with { Variant = null! }],
                },
            }
        )
            Should.Throw<JsonException>(
                () => DeviceBuildJson.Snapshot(new() { BuildsJson = DeviceBuildJson.Write([invalid]) })
            );
        Should.Throw<JsonException>(
            () => DeviceBuildJson.Snapshot(new() { BuildsJson = DeviceBuildJson.Write([first, first]) })
        );
    }

    [Fact]
    public async Task CompetingSaves_CommitOnce_FailedTransactionRollsBack_AndDrainStopsWrites()
    {
        using var db = new SqliteTestDatabase();
        var factory = new CollectionPersistenceTests.FaultFactory(db.Factory);
        using var scope = new BuildScope(factory);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var id = scope.Device.DeviceId;
        factory.FailAfterSave = true;
        (await scope.Builds.SaveAsync(id, today, null, null, [scope.First.ItemKey])).Status.ShouldBe(
            BuildResultStatus.StorageFailure
        );
        scope.Data.ReadDeviceBuildDays().ShouldBeEmpty();
        factory.FailAfterSave = false;
        var changes = 0;
        scope.Builds.Changed += (_, _) => changes++;
        var results = await Task.WhenAll(
            scope.Builds.SaveAsync(id, today, null, null, [scope.First.ItemKey]),
            scope.Builds.SaveAsync(id, today, null, null, [scope.Second.ItemKey])
        );
        results.Count(x => x.Changed).ShouldBe(1);
        results.Count(x => x.Status == BuildResultStatus.Conflict).ShouldBe(1);
        changes.ShouldBe(1);
        var day = scope.Builds.GetDay(id, today)!;
        factory.FailAfterSave = true;
        (await scope.Builds.DeleteAsync(day, day.Current!.BuildKey)).Status.ShouldBe(BuildResultStatus.StorageFailure);
        scope.Builds.GetDay(id, today)!.Revision.ShouldBe(day.Revision);
        scope.Data.ReadDeviceBuildDays().Single().Current!.BuildKey.ShouldBe(day.Current.BuildKey);
        factory.FailAfterSave = false;
        (
            await scope.Builds.SaveAsync(id, today, day, null, day.Current.Parts.Select(x => x.ItemKey).ToArray())
        ).Changed.ShouldBeFalse();
        changes.ShouldBe(1);
        scope.SetConnected(false);
        (await scope.Builds.SaveAsync(id, today, day, null, [])).Status.ShouldBe(BuildResultStatus.ValidationError);
        await scope.Builds.StopAndDrainAsync();
        (await scope.Builds.DeleteAsync(day, day.Current.BuildKey)).Succeeded.ShouldBeFalse();
    }

    [Theory]
    [InlineData(DeviceTypes.Keyboard, true, 9, false)]
    [InlineData(DeviceTypes.Keyboard, true, 10, true)]
    [InlineData(DeviceTypes.Keyboard, true, 11, true)]
    [InlineData(DeviceTypes.Keyboard, false, 100, false)]
    [InlineData(DeviceTypes.Mouse, true, 100, false)]
    public void PromptPolicy(DeviceTypes kind, bool reconnection, int gap, bool expected)
    {
        var time = DateTime.UtcNow;
        var connection = new DeviceConnectedEvent(
            new() { DeviceId = "test", DeviceType = kind },
            time,
            reconnection,
            time.AddSeconds(-gap)
        );
        var build = new DeviceBuildSnapshot(Guid.NewGuid(), [new(Guid.NewGuid(), PartKinds.Switch, "Switch", "45g")]);
        PartsPromptService.ShouldPrompt(connection, build).ShouldBe(expected);
        PartsPromptService.ShouldPrompt(connection, build with { Parts = [] }).ShouldBeFalse();
        PartsPromptService.ShouldPrompt(connection, null).ShouldBeFalse();
    }

    [Fact]
    public async Task BuildsOnly_CopyRawDays_AndEveryFieldAffectsFingerprint()
    {
        using var scope = new DatabaseSwitchTestScope();
        using var source = scope.CreateDatabase();
        using var target = scope.CreateDatabase(Path.Combine(scope.DirectoryPath, "target.db"));
        Seed(source);
        Seed(target);
        (await DatabaseSwitchService.HasApplicationDataAsync(source)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(
            () => DatabaseSwitchService.CopyHistoryAsync(source, target, "copy", false)
        );
        var original = await DatabaseHistoryFingerprint.ReadAsync(source);
        await DatabaseSwitchService.CopyHistoryAsync(source, target, "copy", true);
        (await DatabaseHistoryFingerprint.ReadAsync(target)).ShouldBe(original);
        target
            .DeviceBuildDays.Select(x => x.DeviceBuildDayId)
            .ToArray()
            .ShouldNotBe(source.DeviceBuildDays.Select(x => x.DeviceBuildDayId).ToArray());
        foreach (
            var change in new Action<DeviceBuildDay>[]
            {
                x => x.DayKey = Guid.NewGuid(),
                x => x.DeviceId += "!",
                x => x.Day = x.Day.AddDays(10),
                x => x.Revision++,
                x => x.BuildsSchemaVersion++,
                x => x.BuildsJson += " ",
            }
        )
        {
            using var transaction = source.Database.BeginTransaction();
            source.ChangeTracker.Clear();
            change(source.DeviceBuildDays.First());
            source.SaveChanges();
            (await DatabaseHistoryFingerprint.ReadAsync(source)).Builds.ShouldNotBe(original.Builds);
            transaction.Rollback();
        }
    }

    [Fact]
    public void SqliteUpgrade_AddsDailyBuildsAndPreservesOtherHistory()
    {
        using var scope = new DatabaseSwitchTestScope();
        using var context = ConfiguredDbContextFactory.CreateSqliteContext(scope.SqlitePath, pooling: false);
        ExerciseUpgrade(context);
    }

    internal static void ExerciseUpgrade(ApplicationDbContext context)
    {
        context
            .GetService<IMigrator>()
            .Migrate(context.Database.GetMigrations().Single(x => x.EndsWith("_AddCollectionItemVariant")));
        DatabaseSwitchTestScope.Seed(context);
        var before = context.Devices.Single().DeviceName;
        var collection = context.CollectionItems.OrderBy(x => x.ItemKey).Select(x => x.DetailsJson).ToArray();
        var events = context.DeviceEvents.Count();
        context.Database.Migrate();
        context.Database.HasPendingModelChanges().ShouldBeFalse();
        context.DeviceBuildDays.ShouldBeEmpty();
        context.Devices.Single().DeviceName.ShouldBe(before);
        context.CollectionItems.OrderBy(x => x.ItemKey).Select(x => x.DetailsJson).ToArray().ShouldBe(collection);
        context.DeviceEvents.Count().ShouldBe(events);
        Seed(context);
        context.DeviceBuildDays.Count().ShouldBe(3);
        using var transaction = context.Database.BeginTransaction();
        var row = context.DeviceBuildDays.First();
        context.DeviceBuildDays.Add(new() { DeviceId = row.DeviceId, Day = row.Day });
        Should.Throw<DbUpdateException>(() => context.SaveChanges());
        transaction.Rollback();
    }

    internal static void Seed(ApplicationDbContext context)
    {
        context.DeviceBuildDays.AddRange(
            Enumerable
                .Range(1, 3)
                .Select(i => new DeviceBuildDay
                {
                    DeviceBuildDayId = 100 + i,
                    DeviceId = "build-only",
                    Day = new DateOnly(2026, 11, i),
                    Revision = i,
                    BuildsSchemaVersion = i == 1 ? 1 : 99,
                    BuildsJson = i == 1 ? "[]" : " future JSON " + i,
                })
        );
        context.SaveChanges();
    }

    internal sealed class BuildScope : IDisposable
    {
        private readonly IDbContextFactory<ApplicationDbContext> _factory;
        private readonly DatabaseInstanceLock _databaseLock = new();
        internal AppTimerService Timer { get; } = new();
        internal DailyStatsService Daily { get; }
        internal DataService Data { get; }
        internal DeviceBuildService Builds { get; }
        internal CollectionService Collection { get; }
        internal Device Device { get; } =
            new()
            {
                DeviceId = "keyboard",
                DeviceName = "Test keyboard",
                DeviceType = DeviceTypes.Keyboard,
                SessionStartedAt = DateTime.Now,
            };
        internal CollectionItem First { get; } =
            new()
            {
                Name = "First",
                PartKind = PartKinds.Keycap,
                CatalogEntryId = "sample",
                Variant = "Base kit",
                QuantityUnit = "Kit",
            };
        internal CollectionItem Second { get; } =
            new()
            {
                Name = "Second",
                PartKind = PartKinds.Switch,
                QuantityUnit = "Piece",
            };

        internal BuildScope(IDbContextFactory<ApplicationDbContext> factory)
        {
            _factory = factory;
            Daily = new(factory, Timer);
            var catalogs = new CatalogService();
            Data = new(factory, Daily, _databaseLock, catalogs);
            Collection = new(Data, catalogs, action => action());
            Builds = new(Data);
            using var context = factory.CreateDbContext();
            context.Devices.Add(Device);
            context.CollectionItems.AddRange(First, Second);
            context.SaveChanges();
        }

        internal void SetConnected(bool connected)
        {
            Device.SessionStartedAt = connected ? DateTime.Now : null;
            using var context = _factory.CreateDbContext();
            context.Devices.Single(x => x.DeviceId == Device.DeviceId).SessionStartedAt = Device.SessionStartedAt;
            context.SaveChanges();
        }

        public void Dispose()
        {
            Builds.StopAndDrainAsync().GetAwaiter().GetResult();
            Collection.StopAndDrainAsync().GetAwaiter().GetResult();
            Daily.Dispose();
            Timer.Dispose();
            _databaseLock.Dispose();
        }
    }
}

[Collection("PostgreSQL switches")]
public class DeviceBuildPostgreSqlTests(PostgreSqlTestServer server)
{
    [PostgreSqlFact]
    public async Task PostgreSql_DailyBuildContract()
    {
        var settings = await server.CreateDatabaseAsync();
        await server.CreateSchemaAsync(settings);
        DeviceBuildTests.ExerciseContract(new Factory(() => server.Open(settings)));
    }

    [PostgreSqlFact]
    public async Task PostgreSqlUpgrade_AddsDailyBuildsAndPreservesOtherHistory()
    {
        var settings = await server.CreateDatabaseAsync();
        using var context = server.Open(settings);
        DeviceBuildTests.ExerciseUpgrade(context);
    }

    [PostgreSqlFact]
    public async Task BuildsOnly_AllTransferDirections()
    {
        using var scope = new DatabaseSwitchTestScope();
        var first = await server.CreateDatabaseAsync();
        var second = await server.CreateDatabaseAsync();
        DatabaseHistoryFingerprint expected;
        using (var source = scope.CreateDatabase())
        {
            DeviceBuildTests.Seed(source);
            expected = await DatabaseHistoryFingerprint.ReadAsync(source);
        }
        scope.Credentials.Password = "";
        scope.Schedule(DatabaseProvider.Sqlite, DatabaseProvider.PostgreSql, first);
        await scope.Switch.ProcessPendingSwitchAsync();
        using (var target = server.Open(first))
            (await DatabaseHistoryFingerprint.ReadAsync(target)).ShouldBe(expected);
        new DatabaseConnectionSettingsService(scope.Settings, scope.Credentials).SchedulePostgreSql(
            scope.Settings.GetSettings(),
            second,
            "",
            true,
            false
        );
        await scope.Switch.ProcessPendingSwitchAsync();
        using (var target = server.Open(second))
            (await DatabaseHistoryFingerprint.ReadAsync(target)).ShouldBe(expected);
        scope.Schedule(DatabaseProvider.PostgreSql, DatabaseProvider.Sqlite, second);
        await scope.Switch.ProcessPendingSwitchAsync();
        using var exported = scope.OpenDatabase();
        (await DatabaseHistoryFingerprint.ReadAsync(exported)).ShouldBe(expected);
    }

    private sealed class Factory(Func<ApplicationDbContext> open) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => open();
    }
}
