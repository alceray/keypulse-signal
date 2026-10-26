using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
                    var initialGrid = Descendants<DataGrid>(host).Single();
                    foreach (var column in initialGrid.Columns.Where(x => x.Visibility == Visibility.Visible))
                        column.ActualWidth.ShouldBe(column.Width.Value, 1);
                    host.Switches.IsCollectionExpanded = false;
                    host.Switches.IsCatalogExpanded = false;
                    await PartsViewModelTests.Pump();
                    host.Switches.IsCollectionExpanded = true;
                    host.Switches.IsCatalogExpanded = true;
                    await PartsViewModelTests.Pump();
                    foreach (var column in initialGrid.Columns.Where(x => x.Visibility == Visibility.Visible))
                        column.ActualWidth.ShouldBe(column.Width.Value, 1);
                }
                Save(owner, "empty-collection-wide", false);
                owner.Width = 1000;
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
                        && System.Windows.Automation.AutomationProperties.GetName(x) == "Save quantity"
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
                row.Section.ShouldBe("Your collection");
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
                        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
                tabs.SelectedIndex = 1;
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
                    .Single(x =>
                        ((System.Windows.Data.CollectionViewGroup)x.DataContext).Name.Equals("Your collection")
                    );
                var catalogSection = Descendants<Expander>(host)
                    .Single(x => ((System.Windows.Data.CollectionViewGroup)x.DataContext).Name.Equals("Catalog"));
                collectionSection.SetCurrentValue(Expander.IsExpandedProperty, false);
                catalogSection.SetCurrentValue(Expander.IsExpandedProperty, false);
                host.Switches.IsCollectionExpanded.ShouldBeFalse();
                host.Switches.IsCatalogExpanded.ShouldBeFalse();
                host.Switches.ApplySearch();
                tabs.SelectedIndex = 1;
                await PartsViewModelTests.Pump();
                tabs.SelectedIndex = 0;
                await PartsViewModelTests.Pump();
                Descendants<Expander>(host).ShouldAllBe(x => !x.IsExpanded);
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
                        .DetailsVisibility.ShouldBe(Visibility.Visible);
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
