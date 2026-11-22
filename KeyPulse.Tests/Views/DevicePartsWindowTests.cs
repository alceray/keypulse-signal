using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeyPulse.Configuration;
using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.Tests.Infrastructure;
using KeyPulse.Tests.Services;
using KeyPulse.Tests.ViewModels;
using KeyPulse.ViewModels;
using KeyPulse.ViewModels.Calendar;
using KeyPulse.Views;

namespace KeyPulse.Tests.Views;

[Collection("WPF theme")]
public class DevicePartsWindowTests
{
    [Fact]
    public async Task Windows_PromptEditorDevicesAndCalendar()
    {
        if (Environment.GetEnvironmentVariable("KEYPULSE_DEVICE_PARTS_CHILD") != "1")
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("vstest");
            start.ArgumentList.Add(typeof(DevicePartsWindowTests).Assembly.Location);
            start.ArgumentList.Add("/TestCaseFilter:FullyQualifiedName~DevicePartsWindowTests");
            start.Environment["KEYPULSE_DEVICE_PARTS_CHILD"] = "1";
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
            foreach (var name in new[] { "ThemeControls", "AppStyles" })
                app.Resources.MergedDictionaries.Add(
                    new ResourceDictionary
                    {
                        Source = new Uri($"/KeyPulse Signal;component/Views/Styles/{name}.xaml", UriKind.Relative),
                    }
                );
            using var db = new SqliteTestDatabase();
            using var scope = new DeviceBuildTests.BuildScope(db.Factory);
            using var model = new DevicePartsEditorViewModel(
                scope.Device,
                scope.Builds,
                scope.Collection,
                scope.Timer,
                DateOnly.FromDateTime(DateTime.Now)
            );
            var editor = new DevicePartsEditorWindow(model);
            try
            {
                editor.Show();
                await WaitUntil(() => model.CanEdit);
                await WaitUntil(() => Descendants<CheckBox>(editor).Any());
                var check = Descendants<CheckBox>(editor).First();
                check.IsChecked = true;
                model.HasChanges.ShouldBeTrue();
                FindButton(editor, "Save").Visibility.ShouldBe(Visibility.Visible);
                await PartsViewModelTests.Pump();
                Click(FindButton(editor, "Save"));
                await WaitUntil(
                    () =>
                        scope.Builds.GetDay(scope.Device.DeviceId, DateOnly.FromDateTime(DateTime.Now))?.Builds.Length
                            == 1
                        && !model.IsBusy
                );
                var build = scope.Builds.GetCurrent(scope.Device.DeviceId, DateOnly.FromDateTime(DateTime.Now))!;
                build.Parts.Length.ShouldBe(1);
                var foreground = GetForegroundWindow();
                var prompt = new DevicePartsPromptWindow(scope.Device, build);
                prompt.Show();
                await PartsViewModelTests.Pump();
                prompt.ShowActivated.ShouldBeFalse();
                prompt.IsActive.ShouldBeFalse();
                GetForegroundWindow().ShouldBe(foreground);
                Descendants<Button>(prompt).Any(x => x.IsDefault).ShouldBeFalse();
                // A real mouse-activation message must preserve focus while permitting the click.
                SendMessage(new WindowInteropHelper(prompt).Handle, 0x0021, IntPtr.Zero, IntPtr.Zero)
                    .ShouldBe(new IntPtr(3));
                var changed = false;
                prompt.ChangeRequested += () => changed = true;
                foreach (var dark in new[] { false, true })
                {
                    AppColorPalette.Apply(app.Resources, dark);
                    await PartsViewModelTests.Pump();
                    Save(editor, "device-parts-editor", dark);
                    Save(prompt, "device-parts-prompt", dark);
                }
                FindButton(prompt, "Change").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                changed.ShouldBeTrue();
                prompt.IsVisible.ShouldBeFalse();
                var same = new DevicePartsPromptWindow(scope.Device, build);
                same.Show();
                FindButton(same, "Same").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                same.IsVisible.ShouldBeFalse();
                scope
                    .Builds.GetDay(scope.Device.DeviceId, DateOnly.FromDateTime(DateTime.Now))!
                    .Builds.Length.ShouldBe(1);
                model.Choices.First().IsSelected = !model.Choices.First().IsSelected;
                scope.SetConnected(false);
                await PartsViewModelTests.Pump();
                model.HasConflict.ShouldBeFalse();
                model.HasChanges.ShouldBeTrue();
                FindButton(editor, "Save").IsEnabled.ShouldBeFalse();
                Click(FindButton(editor, "Reload"));
                await WaitUntil(() => model.CanEdit && !model.HasConflict && !model.HasChanges);
                model.HasChanges.ShouldBeFalse();
                editor.Width = 540;
                await PartsViewModelTests.Pump();
                Save(editor, "device-parts-editor-narrow", true);

                scope.Device.PartsSummary = build.Summary;
                scope.Device.PartsBuild = build;
                var devices = new DeviceListView(new DeviceListSample { DeviceListCollection = [scope.Device] });
                var day = DateOnly.FromDateTime(DateTime.Now);
                scope.SetConnected(true);
                var savedDay = scope.Builds.GetDay(scope.Device.DeviceId, day)!;
                (
                    await scope.Builds.SaveAsync(scope.Device.DeviceId, day, savedDay, null, [scope.First.ItemKey])
                ).Changed.ShouldBeTrue();
                savedDay = scope.Builds.GetDay(scope.Device.DeviceId, day)!;
                scope.SetConnected(false);
                editor.Close();
                using var calendarPrompts = new PartsPromptService(
                    new[] { scope.Device },
                    scope.Builds,
                    scope.Collection,
                    scope.Timer
                );
                var detail = new CalendarDeviceDetail
                {
                    DeviceId = scope.Device.DeviceId,
                    DeviceType = DeviceTypes.Keyboard,
                    DeviceName = scope.Device.DeviceName,
                    ConnectionSeconds = 300,
                    Keystrokes = 42,
                    Builds = savedDay.Builds.Select((item, i) => new CalendarBuildRow(savedDay, item, i + 1)).ToArray(),
                };
                var details = new System.Collections.ObjectModel.ObservableCollection<CalendarDeviceDetail> { detail };
                CalendarBuildRow? edited = null,
                    deleted = null;
                var month = day.AddDays(1 - day.Day);
                var cells = Enumerable
                    .Range(0, ((int)month.DayOfWeek + 6) % 7)
                    .Select(_ => (object?)null)
                    .Concat(
                        Enumerable
                            .Range(1, DateTime.DaysInMonth(day.Year, day.Month))
                            .Select(n =>
                                (object)
                                    new CalendarDaySummary
                                    {
                                        Day = new DateOnly(day.Year, day.Month, n),
                                        HasData = n == day.Day,
                                        Devices =
                                            n == day.Day
                                                ?
                                                [
                                                    new()
                                                    {
                                                        DeviceId = scope.Device.DeviceId,
                                                        DeviceName = scope.Device.DeviceName,
                                                        DeviceType = DeviceTypes.Keyboard,
                                                    },
                                                ]
                                                : [],
                                    }
                            )
                    )
                    .ToArray();
                var calendar = new CalendarView(
                    new
                    {
                        HasSelectedDay = true,
                        IsLoading = false,
                        SelectedDayTitle = day.ToString("dddd, MMMM d, yyyy"),
                        MonthTitle = day.ToString("MMMM yyyy"),
                        EditBuildCommand = new RelayCommand(row =>
                        {
                            edited = (CalendarBuildRow)row!;
                            calendarPrompts.OpenEditor(scope.Device, edited.Day.Day, edited.Build.BuildKey);
                        }),
                        DeleteBuildCommand = new RelayCommand(async row =>
                        {
                            deleted = (CalendarBuildRow)row!;
                            (
                                await scope.Builds.DeleteAsync(deleted.Day, deleted.Build.BuildKey)
                            ).Changed.ShouldBeTrue();
                            var remaining = scope.Builds.GetDay(scope.Device.DeviceId, day)!;
                            details[0] = details[0] with
                            {
                                Builds = remaining
                                    .Builds.Select((item, i) => new CalendarBuildRow(remaining, item, i + 1))
                                    .ToArray(),
                            };
                        }),
                        CalendarGridItems = cells,
                        SelectedDayDetails = details,
                    }
                );
                var host = new Window
                {
                    Width = 1000,
                    Height = 740,
                    Content = devices,
                    Style = (Style)app.Resources["AppWindowStyle"],
                };
                host.Show();
                await PartsViewModelTests.Pump();
                Descendants<DataGrid>(devices).Single().Columns.Any(x => Equals(x.Header, "Parts")).ShouldBeTrue();
                Descendants<TextBlock>(devices).Any(x => x.Text == build.Summary).ShouldBeTrue();
                host.Content = calendar;
                await PartsViewModelTests.Pump();
                Descendants<TextBlock>(calendar).Any(x => x.Text.Contains(build.Summary)).ShouldBeTrue();
                foreach (var dark in new[] { false, true })
                {
                    AppColorPalette.Apply(app.Resources, dark);
                    await PartsViewModelTests.Pump();
                    Save(host, "device-parts-calendar", dark);
                }
                Descendants<Button>(calendar).Count(x => Equals(x.Content, "Edit")).ShouldBe(2);
                Click(Descendants<Button>(calendar).First(x => Equals(x.Content, "Edit")));
                await WaitUntil(() => app.Windows.OfType<DevicePartsEditorWindow>().Any());
                var historical = app.Windows.OfType<DevicePartsEditorWindow>().Single();
                var historicalModel = (DevicePartsEditorViewModel)historical.DataContext;
                await WaitUntil(() => historicalModel.CanEdit);
                historicalModel.EditBuildKey.ShouldBe(build.BuildKey);
                historicalModel.Choices.Single(x => x.Part.ItemKey == scope.First.ItemKey).IsSelected = true;
                await PartsViewModelTests.Pump();
                Click(FindButton(historical, "Save"));
                await WaitUntil(() => !historicalModel.IsBusy && !historicalModel.HasChanges);
                scope.Builds.GetDay(scope.Device.DeviceId, day)!.Builds.Length.ShouldBe(2);
                historical.Close();
                var refreshed = scope.Builds.GetDay(scope.Device.DeviceId, day)!;
                details[0] = details[0] with
                {
                    Builds = refreshed
                        .Builds.Select((item, i) => new CalendarBuildRow(refreshed, item, i + 1))
                        .ToArray(),
                };
                await PartsViewModelTests.Pump();
                Click(Descendants<Button>(calendar).First(x => Equals(x.Content, "Delete")));
                await WaitUntil(() => details[0].Builds.Count == 1);
                edited!.Build.BuildKey.ShouldBe(build.BuildKey);
                deleted!.Build.BuildKey.ShouldBe(build.BuildKey);
                details[0].Builds.Single().Build.BuildKey.ShouldBe(savedDay.Current!.BuildKey);
                details[0].Builds.Single().Label.ShouldContain("Build 1");
                host.Width = 800;
                calendar.LayoutTransform = new ScaleTransform(1.25, 1.25);
                await PartsViewModelTests.Pump();
                Save(host, "device-parts-calendar-narrow-scaled", true);
                host.Close();
                await VerifyPromptQueue(app, db, scope);
            }
            finally
            {
                editor.Close();
                app.Shutdown();
            }
        });
    }

    private static async Task VerifyPromptQueue(
        Application app,
        SqliteTestDatabase db,
        DeviceBuildTests.BuildScope scope
    )
    {
        var time = DateTime.Now;
        scope.SetConnected(true);
        var secondDevice = new Device
        {
            DeviceId = "second-keyboard",
            DeviceName = "Second keyboard",
            DeviceType = DeviceTypes.Keyboard,
            SessionStartedAt = time,
        };
        var day = DateOnly.FromDateTime(DateTime.Now);
        using (var context = db.CreateContext())
        {
            context.DeviceBuildDays.Add(
                new()
                {
                    DeviceId = secondDevice.DeviceId,
                    Day = day,
                    BuildsJson = DeviceBuildJson.Write([scope.Builds.GetCurrent(scope.Device.DeviceId, day)!]),
                }
            );
            context.Devices.Add(secondDevice);
            context.SaveChanges();
        }
        await scope.Builds.LoadAsync(retry: true);
        using var prompts = new PartsPromptService(
            new[] { scope.Device, secondDevice },
            scope.Builds,
            scope.Collection,
            scope.Timer
        );
        await prompts.ReceiveAsync(new(scope.Device, scope.Device.SessionStartedAt!.Value, true, time.AddMinutes(-2)));
        app.Windows.OfType<DevicePartsPromptWindow>().Count().ShouldBe(1);
        var original = time;
        await prompts.ReceiveAsync(new(secondDevice, original, true, time.AddMinutes(-2)));
        secondDevice.SessionStartedAt = time.AddSeconds(1);
        prompts.ConnectionChanged(secondDevice.DeviceId);
        await prompts.ReceiveAsync(new(secondDevice, secondDevice.SessionStartedAt.Value, true, time.AddMinutes(-2)));
        // A delayed old connection must not invalidate the newer pending prompt.
        await prompts.ReceiveAsync(new(secondDevice, original, true, time.AddMinutes(-2)));
        app.Windows.OfType<DevicePartsPromptWindow>().Count().ShouldBe(1);
        app.Windows.OfType<DevicePartsPromptWindow>().Single().Close();
        await PartsViewModelTests.Pump();
        var next = app.Windows.OfType<DevicePartsPromptWindow>().Single();
        next.Title.ShouldBe("Second keyboard reconnected");
        FindButton(next, "Change").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var opened = app.Windows.OfType<DevicePartsEditorWindow>().Single();
        var model = (DevicePartsEditorViewModel)opened.DataContext;
        await WaitUntil(() => model.CanEdit);
        model.HasConflict.ShouldBeFalse();
        prompts.OpenEditor(secondDevice);
        app.Windows.OfType<DevicePartsEditorWindow>().Count().ShouldBe(1);
        opened.Close();
        secondDevice.SessionStartedAt = time.AddMinutes(1);
        prompts.ConnectionChanged(secondDevice.DeviceId);
        await prompts.ReceiveAsync(new(secondDevice, secondDevice.SessionStartedAt.Value, true, time.AddMinutes(-2)));
        var stale = app.Windows.OfType<DevicePartsPromptWindow>().Single();
        secondDevice.SessionStartedAt = null;
        prompts.ConnectionChanged(secondDevice.DeviceId);
        FindButton(stale, "Change").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        app.Windows.OfType<DevicePartsEditorWindow>().ShouldBeEmpty();
        await PartsViewModelTests.Pump();
        app.Windows.OfType<DevicePartsPromptWindow>().ShouldBeEmpty();
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(10))
                throw new TimeoutException();
            await PartsViewModelTests.Pump();
        }
    }

    private static Button FindButton(DependencyObject root, string label) =>
        Descendants<Button>(root).Single(x => Equals(x.Content, label));

    private sealed class DeviceListSample
    {
        public Device[] DeviceListCollection { get; init; } = [];
        public string DeviceTitleWithCount => "Devices (1)";
        public bool ShowAllDevices { get; set; } = true;
        public string CurrentSessionTime => "0:00";
        public Visibility ToastVisibility => Visibility.Collapsed;
    }

    private static void Click(Button button) =>
        (
            (System.Windows.Automation.Provider.IInvokeProvider)
                new System.Windows.Automation.Peers.ButtonAutomationPeer(button).GetPattern(
                    System.Windows.Automation.Peers.PatternInterface.Invoke
                )
        ).Invoke();

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
                yield return typed;
            foreach (var nested in Descendants<T>(child))
                yield return nested;
        }
    }

    private static void Save(FrameworkElement view, string name, bool dark)
    {
        var directory = Environment.GetEnvironmentVariable("KEYPULSE_THEME_PREVIEW_DIR");
        if (directory == null)
            return;
        Directory.CreateDirectory(directory);
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(
            (int)view.ActualWidth,
            (int)view.ActualHeight,
            96,
            96,
            PixelFormats.Pbgra32
        );
        bitmap.Render(view);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, name + (dark ? "-dark" : "-light") + ".png"));
        png.Save(file);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
}
