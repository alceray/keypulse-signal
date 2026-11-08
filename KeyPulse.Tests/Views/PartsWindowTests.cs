using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeyPulse.Configuration;
using KeyPulse.Models;
using KeyPulse.Tests.ViewModels;
using KeyPulse.ViewModels.Collection;
using KeyPulse.Views.Collection;

namespace KeyPulse.Tests.Views;

[Collection("WPF theme")]
public class PartsWindowTests
{
    [Fact]
    public async Task Windows_GroupedCatalogSubtabsAndInlineQuantity()
    {
        // Application.Shutdown tears down WPF's process-wide pack resources. Isolate this
        // real-window test so it cannot break the existing application-free theme tests.
        if (Environment.GetEnvironmentVariable("KEYPULSE_PARTS_WINDOW_TEST_CHILD") != "1")
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("vstest");
            start.ArgumentList.Add(typeof(PartsWindowTests).Assembly.Location);
            start.ArgumentList.Add("/TestCaseFilter:FullyQualifiedName~PartsWindowTests");
            start.Environment["KEYPULSE_PARTS_WINDOW_TEST_CHILD"] = "1";
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90));
            }
            catch (TimeoutException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }
            process.ExitCode.ShouldBe(0, await output + await error);
            return;
        }
        PartsViewModelTests.Run(async () =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ThemeResources());
            app.Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source = new Uri("/KeyPulse Signal;component/Views/Styles/ThemeControls.xaml", UriKind.Relative),
                }
            );
            app.Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source = new Uri("/KeyPulse Signal;component/Views/Styles/AppStyles.xaml", UriKind.Relative),
                }
            );
            using var errors = new BindingErrors();
            PresentationTraceSources.DataBindingSource.Listeners.Add(errors);
            using var scope = new PartsViewModelTests.Scope(new KeyPulse.Services.CatalogService());
            using var host = new CollectionView(scope.Collection, scope.Catalogs);
            var owner = new Window
            {
                Content = host,
                Width = 1000,
                Height = 740,
                Title = "Parts UI verification",
                Style = (Style)app.Resources["AppWindowStyle"],
            };
            try
            {
                owner.Show();
                await scope.Collection.InitializeAsync();
                await PartsViewModelTests.Pump();
                host.Switches.IsCollectionExpanded.ShouldBeTrue();
                host.Switches.IsCatalogExpanded.ShouldBeFalse();
                host.Switches.IsCatalogExpanded = true;
                await PartsViewModelTests.Pump();
                var filterMenus = Descendants<ComboBox>(host).Where(x => x.DataContext is PartFilter).ToArray();
                var filterView = Descendants<PartListView>(host).Single();
                try
                {
                    filterMenus[0].IsDropDownOpen = true;
                    await PartsViewModelTests.Pump();
                    var targetPosition = filterMenus[1]
                        .TranslatePoint(
                            new Point(filterMenus[1].ActualWidth / 2, filterMenus[1].ActualHeight / 2),
                            filterView
                        );
                    filterView.SwitchFilterAt(filterMenus[0], targetPosition).ShouldBeTrue();
                    // The new popup must not acquire capture during the old popup's mouse event.
                    filterMenus[0].IsDropDownOpen.ShouldBeFalse();
                    filterMenus[1].IsDropDownOpen.ShouldBeFalse();
                    await PartsViewModelTests.Pump();
                    filterMenus[0].IsDropDownOpen.ShouldBeFalse();
                    filterMenus[1].IsDropDownOpen.ShouldBeTrue();
                    var firstPosition = filterMenus[0].TranslatePoint(new Point(5, 5), filterView);
                    filterView.SwitchFilterAt(filterMenus[1], firstPosition).ShouldBeTrue();
                    filterMenus[0].IsDropDownOpen.ShouldBeFalse();
                    await PartsViewModelTests.Pump();
                    filterMenus[0].IsDropDownOpen.ShouldBeTrue();
                    filterMenus[1].IsDropDownOpen.ShouldBeFalse();
                    filterView.SwitchFilterAt(filterMenus[1], new Point(-1, -1)).ShouldBeFalse();
                }
                finally
                {
                    foreach (var menu in filterMenus)
                        menu.IsDropDownOpen = false;
                }
                var performance = new List<string>();
                var measuredGrid = Descendants<DataGrid>(host).Single();
                var measuredScroll = Descendants<ScrollViewer>(measuredGrid).First();
                var realizedRows = Descendants<DataGridRow>(host).Count();
                realizedRows.ShouldBeLessThan(100);
                Descendants<TextBox>(host).Count().ShouldBeLessThan(realizedRows * 2);
                Descendants<Button>(host).Count().ShouldBeLessThan(realizedRows * 2 + 20);
                performance.Add(
                    $"Initial: {Descendants<DataGridRow>(host).Count()} realized rows, {Descendants<TextBox>(host).Count()} text boxes"
                );
                var scrollWatch = Stopwatch.StartNew();
                for (var step = 0; step < 20; step++)
                {
                    measuredScroll.PageDown();
                    owner.UpdateLayout();
                    await PartsViewModelTests.Pump();
                }
                performance.Add(
                    $"20 page scrolls: {scrollWatch.Elapsed.TotalMilliseconds:F1} ms; {Descendants<DataGridRow>(host).Count()} realized rows"
                );
                Descendants<DataGridRow>(host).Count().ShouldBeLessThan(100);
                measuredScroll.ScrollToTop();
                await PartsViewModelTests.Pump();
                // A wide, initially empty collection must not squeeze catalog columns
                // down to DataGrid's minimum width when groups are rebuilt or collapsed.
                foreach (var width in new[] { 1600, 800, 1600 })
                {
                    owner.Width = width;
                    await PartsViewModelTests.Pump();
                    if (width == 800)
                    {
                        var lastFilter = Descendants<ComboBox>(host).Last(x => x.DataContext is PartFilter);
                        var addCustom = Descendants<Button>(host).Single(x => Equals(x.Content, "Add custom item"));
                        addCustom
                            .TranslatePoint(new Point(0, addCustom.ActualHeight), owner)
                            .Y.ShouldBe(lastFilter.TranslatePoint(new Point(0, lastFilter.ActualHeight), owner).Y, 1);
                    }
                    var initialGrid = Descendants<DataGrid>(host).Single();
                    AssertColumnWidths(initialGrid);
                    host.Switches.IsCollectionExpanded = false;
                    host.Switches.IsCatalogExpanded = false;
                    await PartsViewModelTests.Pump();
                    host.Switches.IsCollectionExpanded = true;
                    host.Switches.IsCatalogExpanded = true;
                    await PartsViewModelTests.Pump();
                    AssertColumnWidths(initialGrid);
                }
                Save(owner, "empty-collection-wide", false);
                var sizingTabs = Descendants<TabControl>(host).Single();
                sizingTabs.SelectedIndex = 1;
                foreach (var width in new[] { 1800, 800, 1800 })
                {
                    owner.Width = width;
                    await PartsViewModelTests.Pump();
                    AssertColumnWidths(Descendants<DataGrid>(host).Single());
                    host.Keycaps.IsCollectionExpanded = false;
                    host.Keycaps.IsCatalogExpanded = false;
                    await PartsViewModelTests.Pump();
                    AssertColumnWidths(Descendants<DataGrid>(host).Single());
                    host.Keycaps.IsCollectionExpanded = true;
                    host.Keycaps.IsCatalogExpanded = true;
                    await PartsViewModelTests.Pump();
                }
                Save(owner, "keycaps-expanded-width", false);
                sizingTabs.SelectedIndex = 0;
                owner.Width = 1000;
                await PartsViewModelTests.Pump();
                host.Switches.IsCatalogExpanded = true;
                await scope.Collection.AddCustomAsync(
                    PartKinds.Switch,
                    new(
                        "Desk switches",
                        "Maker",
                        null,
                        "Designer",
                        new SwitchPartDetails
                        {
                            SwitchType = "linear",
                            SwitchFamily = "MX",
                            Variants = ["45g", "55g"],
                        }
                    ),
                    73,
                    "Daily board"
                );
                await scope.Collection.AddCustomAsync(
                    PartKinds.Keycap,
                    new(
                        "Desk keycaps",
                        null,
                        null,
                        null,
                        new KeycapPartDetails
                        {
                            Profile = "Cherry",
                            Material = "PBT",
                            Variants = ["Base", "Novelties"],
                        }
                    ),
                    2,
                    "Spare set"
                );
                await PartsViewModelTests.Pump();
                var switchModel = host.Switches;
                for (var index = 0; index < 24; index++)
                    await scope.Collection.AddCustomAsync(
                        PartKinds.Switch,
                        new($"Reopen test {index}", null, null, null, new SwitchPartDetails()),
                        10,
                        null
                    );
                await PartsViewModelTests.Pump();
                var pinnedHeader = Descendants<ToggleButton>(host).Single(x => x.Name == "PinnedCollectionHeader");
                var headerPosition = pinnedHeader.TranslatePoint(new Point(), owner);
                var columnHeaders = Descendants<DataGridColumnHeadersPresenter>(measuredGrid).Single();
                (
                    columnHeaders.TranslatePoint(new Point(0, columnHeaders.ActualHeight), owner).Y <= headerPosition.Y
                ).ShouldBeTrue();
                foreach (var offset in new[] { 2000, 5000, 50000 })
                {
                    switchModel.IsCollectionExpanded.ShouldBeTrue();
                    measuredScroll.ScrollToVerticalOffset(offset);
                    await PartsViewModelTests.Pump();
                    switchModel.IsCollectionExpanded.ShouldBeFalse();
                    pinnedHeader.TranslatePoint(new Point(), owner).ShouldBe(headerPosition);
                    pinnedHeader.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                    pinnedHeader.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    owner.UpdateLayout();
                    await PartsViewModelTests.Pump();
                    switchModel.IsCollectionExpanded.ShouldBeTrue();
                    Save(owner, "reopened-collection", false);
                    measuredScroll.VerticalOffset.ShouldBe(0);
                    switchModel.IsCollectionExpanded.ShouldBeTrue();
                }
                foreach (
                    var temporary in scope.Collection.Items.Where(x => x.Name.StartsWith("Reopen test ")).ToArray()
                )
                    await scope.Collection.RemoveAsync(temporary);
                await PartsViewModelTests.Pump();
                var tabs = Descendants<TabControl>(host).Single();
                Descendants<RadioButton>(host).ShouldBeEmpty();
                var input = Descendants<TextBox>(host)
                    .First(x =>
                        x.DataContext is PartRow { Item: null }
                        && System.Windows.Automation.AutomationProperties.GetName(x) == "Quantity"
                    );
                var row = (PartRow)input.DataContext;
                var originalCount = switchModel.Rows.Count;
                input.SetCurrentValue(TextBox.TextProperty, "70");
                input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                await PartsViewModelTests.Pump();
                var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void Changed(string kind)
                {
                    if (scope.Collection.FindOwned(PartKinds.Switch, row.Entry!.Id) != null)
                        saved.TrySetResult();
                }
                scope.Collection.Changed += Changed;
                var save = Descendants<Button>(host)
                    .Single(x =>
                        ReferenceEquals(x.DataContext, row)
                        && System.Windows.Automation.AutomationProperties.GetName(x) == "Save changes"
                    );
                save.IsEnabled.ShouldBeTrue();
                var saveWatch = Stopwatch.StartNew();
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await PartsViewModelTests.Pump();
                performance.Add($"Catalog add including UI refresh: {saveWatch.Elapsed.TotalMilliseconds:F1} ms");
                var performanceDirectory = Environment.GetEnvironmentVariable("KEYPULSE_THEME_PREVIEW_DIR");
                if (performanceDirectory != null)
                {
                    Directory.CreateDirectory(performanceDirectory);
                    File.WriteAllLines(Path.Combine(performanceDirectory, "render-performance.txt"), performance);
                }
                scope.Collection.Changed -= Changed;
                row.Section.ShouldBe("My collection");
                switchModel.Rows.Count.ShouldBe(originalCount);
                switchModel.Selected.ShouldBeSameAs(row);
                async Task ClickAction(PartRow target, string action, int expected)
                {
                    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    void Updated(string kind)
                    {
                        if (
                            scope.Collection.Items.Any(x =>
                                x.CatalogEntryId == target.Entry!.Id && x.Quantity == expected
                            )
                        )
                            completed.TrySetResult();
                    }
                    scope.Collection.Changed += Updated;
                    try
                    {
                        var button = Descendants<Button>(host)
                            .Single(x =>
                                ReferenceEquals(x.DataContext, target)
                                && System.Windows.Automation.AutomationProperties.GetName(x) == action
                            );
                        button.IsEnabled.ShouldBeTrue();
                        var original = target.Quantity;
                        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        await PartsViewModelTests.Pump();
                        target.QuantityValue.ShouldBe(expected);
                        target.Quantity.ShouldBe(original);
                        completed.Task.IsCompleted.ShouldBeFalse();
                        var commit = Descendants<Button>(host)
                            .Single(x =>
                                ReferenceEquals(x.DataContext, target)
                                && System.Windows.Automation.AutomationProperties.GetName(x) == "Save changes"
                            );
                        commit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                        await PartsViewModelTests.Pump();
                        target.Quantity.ShouldBe(expected);
                    }
                    finally
                    {
                        scope.Collection.Changed -= Updated;
                    }
                }
                await ClickAction(row, "Increase quantity", 80);
                await ClickAction(row, "Decrease quantity", 70);
                foreach (
                    var action in new[]
                    {
                        "Increase quantity",
                        "Increase quantity",
                        "Decrease quantity",
                        "Decrease quantity",
                    }
                )
                {
                    Descendants<Button>(host)
                        .Single(x =>
                            ReferenceEquals(x.DataContext, row)
                            && System.Windows.Automation.AutomationProperties.GetName(x) == action
                        )
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await PartsViewModelTests.Pump();
                    row.Quantity.ShouldBe(70);
                }
                row.QuantityValue.ShouldBe(70);
                row.HasQuantityDraft.ShouldBeFalse();
                Descendants<Button>(host)
                    .Where(x =>
                        ReferenceEquals(x.DataContext, row)
                        && System.Windows.Automation.AutomationProperties.GetName(x) == "Save changes"
                    )
                    .ShouldBeEmpty();
                async Task CommitNotes(string text, bool enter)
                {
                    var notes = Descendants<TextBox>(host)
                        .Single(x =>
                            ReferenceEquals(x.DataContext, row)
                            && System.Windows.Automation.AutomationProperties.GetName(x) == "Notes"
                        );
                    var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    void NotesChanged(string kind)
                    {
                        if (scope.Collection.FindOwned(PartKinds.Switch, row.Entry!.Id)?.Notes == text)
                            committed.TrySetResult();
                    }
                    scope.Collection.Changed += NotesChanged;
                    try
                    {
                        notes.SetCurrentValue(TextBox.TextProperty, text);
                        notes.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                        row.CanRemove.ShouldBeFalse();
                        if (enter)
                            notes.RaiseEvent(
                                new KeyEventArgs(
                                    Keyboard.PrimaryDevice,
                                    PresentationSource.FromVisual(notes),
                                    0,
                                    Key.Enter
                                )
                                {
                                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                                }
                            );
                        else
                            notes.RaiseEvent(
                                new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, notes, tabs)
                                {
                                    RoutedEvent = Keyboard.LostKeyboardFocusEvent,
                                }
                            );
                        await committed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                        await PartsViewModelTests.Pump();
                    }
                    finally
                    {
                        scope.Collection.Changed -= NotesChanged;
                    }
                }
                row.QuantityText = "90";
                await CommitNotes("Enter saves notes", true);
                row.Quantity.ShouldBe(70);
                row.QuantityText.ShouldBe("90");
                switchModel.CancelEdit(row);
                await PartsViewModelTests.Pump();
                await CommitNotes("Click away saves notes", false);
                row.CanRemove.ShouldBeTrue();
                var cancelNotes = Descendants<TextBox>(host)
                    .Single(x =>
                        ReferenceEquals(x.DataContext, row)
                        && System.Windows.Automation.AutomationProperties.GetName(x) == "Notes"
                    );
                cancelNotes.SetCurrentValue(TextBox.TextProperty, "Do not save this");
                cancelNotes.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                await PartsViewModelTests.Pump();
                var cancelButton = Descendants<Button>(host)
                    .Single(x => ReferenceEquals(x.DataContext, row) && x.Name == "CancelRowButton");
                cancelNotes.RaiseEvent(
                    new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, cancelNotes, cancelButton)
                    {
                        RoutedEvent = Keyboard.LostKeyboardFocusEvent,
                    }
                );
                cancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await PartsViewModelTests.Pump();
                row.Editor!.Notes.ShouldBe("Click away saves notes");
                scope.Collection.FindOwned(PartKinds.Switch, row.Entry!.Id)!.Notes.ShouldBe("Click away saves notes");
                tabs.SelectedIndex = 1;
                await PartsViewModelTests.Pump();
                host.Keycaps.IsCollectionExpanded.ShouldBeTrue();
                host.Keycaps.IsCatalogExpanded.ShouldBeFalse();
                host.Keycaps.IsCatalogExpanded = true;
                await PartsViewModelTests.Pump();
                var capRow = (PartRow)
                    Descendants<Button>(host)
                        .First(x =>
                            x.IsVisible
                            && System.Windows.Automation.AutomationProperties.GetName(x) == "Add to collection"
                        )
                        .DataContext;
                await ClickAction(capRow, "Add to collection", 1);
                await ClickAction(capRow, "Increase quantity", 2);
                await ClickAction(capRow, "Decrease quantity", 1);
                var removed = new TaskCompletionSource();
                void ObserveRemoval(string kind)
                {
                    if (kind == PartKinds.Keycap && scope.Collection.FindOwned(kind, capRow.Entry!.Id) == null)
                        removed.TrySetResult();
                }
                scope.Collection.Changed += ObserveRemoval;
                try
                {
                    var decrement = Descendants<Button>(host)
                        .Single(x =>
                            ReferenceEquals(x.DataContext, capRow)
                            && System.Windows.Automation.AutomationProperties.GetName(x) == "Decrease quantity"
                        );
                    decrement.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    capRow.CanDecrease.ShouldBeFalse();
                    capRow.IsPendingRemoval.ShouldBeTrue();
                    host.Keycaps.CancelEdit(capRow);
                    capRow.IsPendingRemoval.ShouldBeFalse();
                    await PartsViewModelTests.Pump();
                    Descendants<Button>(host)
                        .Single(x =>
                            ReferenceEquals(x.DataContext, capRow)
                            && System.Windows.Automation.AutomationProperties.GetName(x) == "Remove item"
                        )
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    capRow.IsPendingRemoval.ShouldBeTrue();
                    scope.Collection.FindOwned(PartKinds.Keycap, capRow.Entry!.Id)!.Quantity.ShouldBe(1);
                    await PartsViewModelTests.Pump();
                    Descendants<DataGridRow>(host)
                        .Single(x => ReferenceEquals(x.Item, capRow))
                        .BorderThickness.Left.ShouldBe(3);
                    Descendants<Button>(host)
                        .Single(x => ReferenceEquals(x.DataContext, capRow) && x.Name == "SaveRowButton")
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await removed.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    await PartsViewModelTests.Pump();
                    capRow.Section.ShouldBe("Catalog");
                    capRow.Quantity.ShouldBe(0);
                    capRow.CanDecrease.ShouldBeFalse();
                }
                finally
                {
                    scope.Collection.Changed -= ObserveRemoval;
                }
                await ClickAction(capRow, "Add to collection", 1);
                host.Keycaps.Search = "desk";
                host.Keycaps.ApplySearch();
                await PartsViewModelTests.Pump();
                var clearSearch = Descendants<Button>(host)
                    .Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "Clear search");
                clearSearch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                host.Keycaps.Search.ShouldBeEmpty();
                host.Keycaps.Search = "desk";
                host.Keycaps.ApplySearch();
                tabs.SelectedIndex = 0;
                await PartsViewModelTests.Pump();
                host.Switches.Search.ShouldBeEmpty();
                var collectionSection = Descendants<Expander>(host)
                    .Single(x => ((System.Windows.Data.CollectionViewGroup)x.DataContext).Name.Equals("My collection"));
                var catalogSection = Descendants<Expander>(host)
                    .Single(x => ((System.Windows.Data.CollectionViewGroup)x.DataContext).Name.Equals("Catalog"));
                collectionSection.SetCurrentValue(Expander.IsExpandedProperty, false);
                catalogSection.SetCurrentValue(Expander.IsExpandedProperty, false);
                host.Switches.IsCollectionExpanded.ShouldBeFalse();
                host.Switches.IsCatalogExpanded.ShouldBeFalse();
                host.Switches.ApplySearch();
                row.QuantityText = "0";
                var abandonedDraft = host.Switches.AddCustom()!;
                abandonedDraft.Editor!.Name = "Discard on tab switch";
                tabs.SelectedIndex = 1;
                await PartsViewModelTests.Pump();
                row.IsPendingRemoval.ShouldBeFalse();
                row.QuantityText.ShouldBe(row.Quantity.ToString());
                host.Switches.Rows.SourceCollection.Cast<PartRow>().ShouldNotContain(abandonedDraft);
                host.Switches.IsCollectionExpanded = false;
                tabs.SelectedIndex = 0;
                await PartsViewModelTests.Pump();
                host.Switches.IsCollectionExpanded.ShouldBeTrue();
                host.Switches.IsCatalogExpanded.ShouldBeFalse();
                var revealDraft = host.Switches.AddCustom()!;
                host.Switches.IsCollectionExpanded.ShouldBeTrue();
                host.Switches.IsCatalogExpanded.ShouldBeFalse();
                host.Switches.CancelEdit(revealDraft);
                host.Switches.IsCatalogExpanded = true;
                foreach (var dark in new[] { false, true })
                {
                    AppColorPalette.Apply(app.Resources, dark);
                    owner.UpdateLayout();

                    await PartsViewModelTests.Pump();
                    Save(owner, "collection-switches", dark);
                    tabs.SelectedIndex = 1;
                    await PartsViewModelTests.Pump();
                    Save(owner, "collection-keycaps", dark);
                    tabs.SelectedIndex = 0;
                    await PartsViewModelTests.Pump();
                    var draft = host.Switches.AddCustom()!;
                    var grid = Descendants<DataGrid>(host).Single();
                    grid.ScrollIntoView(draft);
                    await PartsViewModelTests.Pump();
                    Descendants<DataGridRow>(host)
                        .Single(x => ReferenceEquals(x.Item, draft))
                        .DetailsVisibility.ShouldBe(Visibility.Collapsed);
                    var nameInput = Descendants<TextBox>(host)
                        .Single(x =>
                            ReferenceEquals(x.DataContext, draft)
                            && System.Windows.Automation.AutomationProperties.GetName(x) == "Name"
                        );
                    nameInput.SetCurrentValue(TextBox.TextProperty, "Inline custom draft");
                    nameInput.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                    draft.Editor!.Name.ShouldBe("Inline custom draft");
                    Save(owner, "inline-custom", dark);
                    host.Switches.CancelEdit(draft);
                    owner.Width = 800;
                    owner.FontSize = 18;
                    await PartsViewModelTests.Pump();
                    Save(owner, "grouped-narrow-scaled", dark);
                    owner.Width = 1000;
                    owner.FontSize = 12;
                }
                owner.Hide();

                (
                    await scope.Collection.AddCustomAsync(
                        PartKinds.Keycap,
                        new("Hidden update", null, null, null, new KeycapPartDetails()),
                        1,
                        null
                    )
                ).Succeeded.ShouldBeTrue();
                await PartsViewModelTests.Pump();
                host.Keycaps.Rows.SourceCollection.Cast<PartRow>().Count(x => x.Item != null).ShouldBe(3);
                owner.Show();
                await PartsViewModelTests.Pump();
                host.Keycaps.Search.ShouldBe("desk");
                owner.OwnedWindows.Count.ShouldBe(0);
                owner.Close();
                errors.Messages.ShouldBeEmpty();
            }
            finally
            {
                host.Dispose();
                owner.Close();
                AppColorPalette.Apply(app.Resources, false);
                PresentationTraceSources.DataBindingSource.Listeners.Remove(errors);
                app.Shutdown();
            }
        });
    }

    private static void AssertColumnWidths(DataGrid grid)
    {
        var columns = grid.Columns.Where(x => x.Visibility == Visibility.Visible).ToArray();
        foreach (var column in columns)
        {
            column.ActualWidth.ShouldBeGreaterThanOrEqualTo(column.MinWidth);
            column.ActualWidth.ShouldBe(column.Width.Value, 1);
        }
        var minimum = columns.Sum(column => column.MinWidth);
        var viewport = Descendants<ScrollViewer>(grid).First().ViewportWidth;
        columns.Sum(column => column.ActualWidth).ShouldBe(Math.Max(minimum, viewport), 2);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                yield return match;
            foreach (var nested in Descendants<T>(child))
                yield return nested;
        }
    }

    private static void Save(Window window, string name, bool dark)
    {
        var directory = Environment.GetEnvironmentVariable("KEYPULSE_THEME_PREVIEW_DIR");
        if (directory == null)
            return;
        Directory.CreateDirectory(directory);
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap(
            (int)window.ActualWidth,
            (int)window.ActualHeight,
            96,
            96,
            PixelFormats.Pbgra32
        );
        bitmap.Render(window);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, name + (dark ? "-dark" : "-light") + ".png"));
        png.Save(file);
    }

    private sealed class BindingErrors : TraceListener
    {
        public List<string> Messages { get; } = [];

        public override void Write(string? message)
        {
            if (message != null)
                Messages.Add(message);
        }

        public override void WriteLine(string? message)
        {
            if (message != null)
                Messages.Add(message);
        }
    }
}
