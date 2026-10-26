using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows.Data;
using System.Windows.Threading;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.Tests.Infrastructure;
using KeyPulse.Tests.Services;
using KeyPulse.ViewModels.Collection;

namespace KeyPulse.Tests.ViewModels;

public class PartsViewModelTests
{
    [Fact]
    public void InlineCustomDraft_SaveCancelAndStaleNotes() =>
        Run(async () =>
        {
            using var scope = new Scope();
            using var list = scope.List();
            await list.InitializeAsync();
            list.Search = "no match";
            list.ApplySearch();
            var draft = list.AddCustom()!;
            list.AddCustom().ShouldBeSameAs(draft);
            list.Rows.Contains(draft).ShouldBeTrue();
            draft.Section.ShouldBe("Your collection");
            draft.Editor!.Quantity.Value.ShouldBe(10);
            draft.Editor.CanSave.ShouldBeFalse();
            draft.Editor.Name = "Handmade";
            draft.Editor.Notes = "First note";
            await list.SaveEditAsync(draft);
            await Pump();
            scope.Collection.Items.Single().Name.ShouldBe("Handmade");
            list.Rows.Contains(draft).ShouldBeFalse();
            list.ToastMessage.ShouldBe("Item saved.");
            list.ClearFilters();
            var owned = list.Rows.Cast<PartRow>().Single(x => x.Item != null);
            list.Edit(owned);
            list.CanChangeQuantity(owned).ShouldBeFalse();
            owned.Editor!.Notes = "Stale note";
            await scope.Collection.UpdateAsync(owned.Item!, 20, "Newer note");
            await Pump();
            await list.SaveEditAsync(owned);
            owned.Editor.HasConflict.ShouldBeTrue();
            owned.Editor.Notes.ShouldBe("Stale note");
            await owned.Editor.ReloadAsync();
            owned.Editor.Notes.ShouldBe("Newer note");
            owned.Editor.Notes = "Final note";
            await list.SaveEditAsync(owned);
            scope.Collection.Items.Single().Notes.ShouldBe("Final note");
            owned.IsEditing.ShouldBeFalse();
            var cancelled = list.AddCustom()!;
            list.CancelEdit(cancelled);
            list.Rows.Contains(cancelled).ShouldBeFalse();
            scope.Collection.Items.Length.ShouldBe(1);
        });

    [Fact]
    public void Quantity_DefaultsInvalidInputAndBounds()
    {
        var switches = new QuantityViewModel(PartKinds.Switch);
        switches.Value.ShouldBe(10);
        switches.Text = "73";
        switches.Value.ShouldBe(73);
        foreach (var invalid in new[] { "", " ", "1.5", "-1", "0", "10001", "1,000", "abc", "999999999999" })
        {
            switches.Text = invalid;
            switches.Value.ShouldBeNull();
            switches.Text.ShouldBe(invalid);
            switches.Error.ShouldNotBeEmpty();
        }
        switches.Text = "10000";
        switches.Value.ShouldBe(10000);
        var keycaps = new QuantityViewModel(PartKinds.Keycap);
        keycaps.Value.ShouldBe(1);
        keycaps.Text = "100";
        keycaps.Text = "101";
        keycaps.Value.ShouldBeNull();
    }

    [Fact]
    public void Editor_ValidationCustomCatalogConflictAndReload() =>
        Run(async () =>
        {
            using var scope = new Scope();
            await scope.Collection.InitializeAsync();
            using var create = new PartEditorViewModel(PartKinds.Keycap, scope.Collection);
            create.CanSave.ShouldBeFalse();
            create.Name = "Custom caps";
            create.Quantity.Text = "101";
            create.CanSave.ShouldBeFalse();
            create.Quantity.Text = "2";
            create.Variants = "Base\nbase";
            create.CanSave.ShouldBeFalse();
            create.Variants = "Base\nNovelties";
            create.Notes = new string('x', 2001);
            create.CanSave.ShouldBeFalse();
            create.Notes = "Saved note";
            create.CanSave.ShouldBeTrue();
            await create.SaveAsync();
            var original = scope.Collection.Items.Single();
            original.CatalogEntryId.ShouldBeNull();
            original.Quantity.ShouldBe(2);
            using var duplicate = new PartEditorViewModel(PartKinds.Keycap, scope.Collection, name: "custom CAPS");
            await duplicate.SaveAsync();
            duplicate.Message.ShouldContain("already exists");
            using var editor = new PartEditorViewModel(PartKinds.Keycap, scope.Collection, original);
            editor.Notes = "stale draft";
            await scope.Collection.UpdateAsync(original, 3, "Current note");
            await editor.SaveAsync();
            editor.HasConflict.ShouldBeTrue();
            editor.Notes.ShouldBe("stale draft");
            editor.CanSave.ShouldBeFalse();
            await editor.ReloadAsync();
            editor.HasConflict.ShouldBeFalse();
            editor.Notes.ShouldBe("Current note");
            editor.Quantity.Value.ShouldBe(3);
            editor.Name = "Renamed";
            await editor.SaveAsync();
            scope.Collection.Items.Single().Name.ShouldBe("Renamed");
            await scope.Collection.AddCatalogAsync(PartKinds.Switch, "sample", 70);
            var catalogItem = scope.Collection.Items.Single(x => x.CatalogEntryId != null);
            using var catalogEditor = new PartEditorViewModel(PartKinds.Switch, scope.Collection, catalogItem);
            catalogEditor.CanDescribe.ShouldBeFalse();
            catalogEditor.Name = "Cannot overwrite";
            catalogEditor.Quantity.Reset(75);
            await catalogEditor.SaveAsync();
            scope.Collection.FindOwned(PartKinds.Switch, "sample")!.Name.ShouldBe("Sample");
        });

    [Fact]
    public void GroupedList_AddSetRemoveAndCustomRowsNeverDuplicate() =>
        Run(async () =>
        {
            using var scope = new Scope();
            using var list = scope.List();
            await list.InitializeAsync();
            var row = list.Rows.Cast<PartRow>().Single();
            row.Quantity.ShouldBe(0);
            row.Section.ShouldBe("Catalog");
            row.QuantityText = "70";
            (await list.SaveQuantityAsync(row))!.Succeeded.ShouldBeTrue();
            await Pump();
            list.Rows.Count.ShouldBe(1);
            row.Section.ShouldBe("Your collection");
            row.Quantity.ShouldBe(70);
            list.Rows.Groups!.Cast<CollectionViewGroup>()
                .Select(x => x.Name)
                .ShouldBe(new object[] { "Your collection", "Catalog" });
            row.QuantityText = "80";
            await list.SaveQuantityAsync(row);
            await Pump();
            row.Quantity.ShouldBe(80);
            row.QuantityText = "0";
            (await list.SaveQuantityAsync(row)).ShouldBeNull();
            row.Quantity.ShouldBe(80);
            row.ResetQuantity();
            row.QuantityText.ShouldBe("80");
            row.QuantityText = "0";
            await list.SaveQuantityAsync(row, removalConfirmed: true);
            await Pump();
            row.Section.ShouldBe("Catalog");
            row.Quantity.ShouldBe(0);
            list.Rows.Count.ShouldBe(1);
            await scope.Collection.AddCustomAsync(
                PartKinds.Switch,
                CollectionPersistenceTests.Custom("Custom"),
                5,
                "Notes"
            );
            await Pump();
            var custom = list.Rows.Cast<PartRow>().Single(x => x.Item?.CatalogEntryId == null && x.Item != null);
            custom.QuantityText = "0";
            await list.SaveQuantityAsync(custom, true);
            await Pump();
            list.Rows.Count.ShouldBe(1);
            list.Category.ShouldBe("Switches");
        });

    [Fact]
    public void GroupsKeepOwnedFirstWhileSearchFiltersAndSortStayIndependent() =>
        Run(async () =>
        {
            using var scope = new Scope();
            using var list = scope.List();
            using var caps = scope.List(PartKinds.Keycap);
            await Task.WhenAll(list.InitializeAsync(), caps.InitializeAsync());
            await scope.Collection.AddCustomAsync(
                PartKinds.Switch,
                new("Zeta", "Maker", null, null, new SwitchPartDetails { SwitchType = "linear", SwitchFamily = "MX" }),
                2,
                "desk alpha"
            );
            await scope.Collection.AddCustomAsync(
                PartKinds.Switch,
                new("Alpha", "Other", null, null, new SwitchPartDetails { SwitchType = "tactile" }),
                20,
                "drawer"
            );
            await Pump();
            list.Search = "desk alpha";
            list.ApplySearch();
            list.Rows.Count.ShouldBe(1);
            list.Filters.Single(x => x.Label == "Manufacturer").Selected = "Other";
            list.Rows.Count.ShouldBe(0);
            list.ClearFilters();
            list.Rows.Count.ShouldBe(3);
            list.Rows.SortDescriptions.Clear();
            list.Rows.SortDescriptions.Add(new(nameof(PartRow.Quantity), ListSortDirection.Descending));
            var groups = list.Rows.Groups!.Cast<CollectionViewGroup>().ToArray();
            groups[0].Name.ShouldBe("Your collection");
            groups[0].Items.Cast<PartRow>().Select(x => x.Quantity).ShouldBe(new[] { 20, 2 });
            groups[1].Items.Cast<PartRow>().Single().Quantity.ShouldBe(0);
            list.Rows.SortDescriptions.Clear();
            list.Rows.SortDescriptions.Add(new(nameof(PartRow.AddedAt), ListSortDirection.Descending));
            list.Rows.Groups.Cast<CollectionViewGroup>().First().Items.Cast<PartRow>().First().Name.ShouldBe("Alpha");
            caps.Search.ShouldBeEmpty();
            var filter = list.Filters.Single(x => x.Label == "Manufacturer");
            filter.Selected = "Maker";
            await list.RemoveAsync(scope.Collection.Items.Single(x => x.Name == "Zeta"));
            await Pump();
            filter.Selected.ShouldBe("Maker");
            filter.Options.ShouldContain("Maker");
            list.Rows.Count.ShouldBe(0);
        });

    [Fact]
    public void InlineDrafts_ValidateBoundsPreserveFailuresAndDetectStaleRevision() =>
        Run(async () =>
        {
            using var scope = new Scope();
            using var list = scope.List();
            await list.InitializeAsync();
            var row = list.Rows.Cast<PartRow>().Single();
            foreach (var text in new[] { "", "1.5", "-1", "10001", "abc", "1,000" })
            {
                row.QuantityText = text;
                row.CanSaveQuantity.ShouldBeFalse();
                (await list.SaveQuantityAsync(row)).ShouldBeNull();
                row.QuantityText.ShouldBe(text);
            }
            row.QuantityText = "73";
            scope.Factory.FailAfterSave = true;
            (await list.SaveQuantityAsync(row))!.Status.ShouldBe(CollectionResultStatus.StorageFailure);
            row.QuantityText.ShouldBe("73");
            row.Quantity.ShouldBe(0);
            scope.Factory.FailAfterSave = false;
            await list.SaveQuantityAsync(row);
            await Pump();
            row.QuantityText = "74";
            await scope.Collection.UpdateAsync(row.Item!, 80, "concurrent notes");
            await Pump();
            row.QuantityText.ShouldBe("74");
            (await list.SaveQuantityAsync(row))!.Status.ShouldBe(CollectionResultStatus.Conflict);
            row.ResetQuantity();
            await list.ReloadAsync();
            row.QuantityText.ShouldBe("80");
            row.QuantityText = "81";
            await list.SaveQuantityAsync(row);
            scope.Collection.Items.Single().Notes.ShouldBe("concurrent notes");
            var cap = new PartRow(PartKinds.Keycap, "cap");
            cap.QuantityText = "101";
            cap.CanSaveQuantity.ShouldBeFalse();
            cap.QuantityText = "100";
            cap.CanSaveQuantity.ShouldBeTrue();
        });

    [Fact]
    public void Failures_RetryUnreadableAndDisposedViews() =>
        Run(async () =>
        {
            using var scope = new Scope();
            using var list = scope.List();
            scope.Factory.FailRead = true;
            await list.InitializeAsync();
            list.ShowRetry.ShouldBeTrue();
            list.CanMutate.ShouldBeFalse();
            scope.Factory.FailRead = false;
            await list.InitializeAsync(true);
            var row = list.Rows.Cast<PartRow>().Single();
            row.QuantityText = "10";
            await list.SaveQuantityAsync(row);
            using (var ctx = scope.Database.CreateContext())
            {
                ctx.CollectionItems.Single().DetailsSchemaVersion = 99;
                ctx.SaveChanges();
            }
            await list.ReloadAsync();
            await Pump();
            list.Selected = row;
            list.CanEdit.ShouldBeFalse();
            list.CanRemove.ShouldBeTrue();
            list.CanChangeQuantity(row).ShouldBeFalse();
            row.Status.ShouldBe("Details unavailable");
            list.Dispose();
            var events = 0;
            list.PropertyChanged += (_, _) => events++;
            await scope.Collection.RemoveAsync(scope.Collection.Items.Single());
            await Pump();
            events.ShouldBe(0);
        });

    [Fact]
    public void PendingWrites_GuardCommandsAndSurviveViewCloseAndDrain() =>
        Run(async () =>
        {
            using var scope = new Scope();
            using var list = scope.List();
            await list.InitializeAsync();
            var row = list.Rows.Cast<PartRow>().Single();
            row.QuantityText = "10";
            var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();
            scope.Factory.AfterSave = () =>
            {
                saved.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException();
            };
            var write = list.SaveQuantityAsync(row);
            await saved.Task;
            try
            {
                list.CanMutate.ShouldBeFalse();
                (await list.SaveQuantityAsync(row)).ShouldBeNull();
                list.Dispose();
                var drain = scope.Collection.StopAndDrainAsync();
                drain.IsCompleted.ShouldBeFalse();
                release.Set();
                await drain;
                await write;
                scope.Collection.Items.Single().Quantity.ShouldBe(10);
            }
            finally
            {
                release.Set();
            }
        });

    [Fact]
    public void CatalogFailure_StillShowsOwnedRowsAndAllowsCustomCreation() =>
        Run(async () =>
        {
            var fail = true;
            var catalogs = new CatalogService(name =>
                fail
                    ? throw new IOException()
                    : CatalogServiceTests.StreamOf(
                        name.Contains("switches") ? CatalogServiceTests.SwitchJson : CatalogServiceTests.EmptyJson
                    )
            );
            using var scope = new Scope(catalogs);
            using var list = scope.List();
            await list.InitializeAsync();
            list.ShowRetry.ShouldBeTrue();
            list.CanMutate.ShouldBeTrue();
            using var editor = new PartEditorViewModel(PartKinds.Switch, scope.Collection, name: "Custom");
            await editor.SaveAsync();
            await Pump();
            list.Rows.Count.ShouldBe(1);
            fail = false;
            await list.InitializeAsync(true);
            list.Rows.Count.ShouldBe(2);
            scope.Factory.FailAfterSave = true;
            await list.InitializeAsync(true);
            list.CatalogMessage.ShouldContain("refresh failed");
            list.CanChangeQuantity(list.Rows.Cast<PartRow>().Single(x => x.Item == null)).ShouldBeFalse();
            scope.Factory.FailAfterSave = false;
        });

    [Fact]
    public void FullCatalog_GroupFilterSortDiagnostics() =>
        Run(async () =>
        {
            using var scope = new Scope(new CatalogService());
            var times = new List<string>();
            foreach (var kind in new[] { PartKinds.Switch, PartKinds.Keycap })
            {
                using var list = scope.List(kind);
                await list.InitializeAsync();
                var watch = Stopwatch.StartNew();
                list.Search = kind == PartKinds.Switch ? "gateron" : "gmk";
                list.ApplySearch();
                list.Rows.SortDescriptions.Clear();
                list.Rows.SortDescriptions.Add(new(nameof(PartRow.Name), ListSortDirection.Descending));
                list.Rows.Count.ShouldBeGreaterThan(0);
                times.Add($"{kind} grouped search + sort: {watch.Elapsed.TotalMilliseconds:F1} ms");
                list.ClearFilters();
                var target = list.Rows.Cast<PartRow>().First();
                var untouched = list.Rows.Cast<PartRow>().Last();
                var notifications = 0;
                var resets = 0;
                untouched.PropertyChanged += (_, _) => notifications++;
                ((System.Collections.Specialized.INotifyCollectionChanged)list.Rows).CollectionChanged += (_, _) =>
                    resets++;
                target.QuantityText = "1";
                (await list.SaveQuantityAsync(target))!.Succeeded.ShouldBeTrue();
                await Pump();
                notifications.ShouldBe(0);
                resets.ShouldBe(1);
            }
            var directory = Environment.GetEnvironmentVariable("KEYPULSE_THEME_PREVIEW_DIR");
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
                File.WriteAllLines(Path.Combine(directory, "catalog-performance.txt"), times);
            }
        });

    internal static Task Pump() =>
        Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;

    internal static void Run(Func<Task> test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    await test();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60)))
            throw new TimeoutException("Parts UI test timed out.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    internal sealed class Scope : IDisposable
    {
        public SqliteTestDatabase Database { get; } = new();
        public CollectionPersistenceTests.FaultFactory Factory { get; }
        private readonly AppTimerService _timer = new();
        private readonly DailyStatsService _daily;
        private readonly DatabaseInstanceLock _databaseLock = new();
        public CatalogService Catalogs { get; }
        public CollectionService Collection { get; }

        public Scope(CatalogService? catalogs = null)
        {
            Factory = new(Database.Factory);
            Catalogs = catalogs ?? CatalogServiceTests.Catalog();
            _daily = new(Factory, _timer);
            var data = new DataService(Factory, _daily, _databaseLock, Catalogs);
            var dispatcher = Dispatcher.CurrentDispatcher;
            Collection = new(data, Catalogs, action => dispatcher.BeginInvoke(action));
        }

        public PartListViewModel List(string kind = PartKinds.Switch) => new(kind, Collection, Catalogs);

        public void Dispose()
        {
            Collection.StopAndDrainAsync().GetAwaiter().GetResult();
            _daily.Dispose();
            _timer.Dispose();
            _databaseLock.Dispose();
            Database.Dispose();
        }
    }
}
