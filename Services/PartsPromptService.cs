using System.Collections.Concurrent;
using System.Windows;
using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.ViewModels;
using KeyPulse.Views;
using Serilog;

namespace KeyPulse.Services;

public sealed class PartsPromptService : IDisposable
{
    private readonly IEnumerable<Device> _devices;
    private readonly DeviceBuildService _builds;
    private readonly CollectionService _collection;
    private readonly AppTimerService _timer;
    private readonly Action? _stopListening;
    private readonly Dictionary<string, DevicePartsEditorWindow> _editors = [];
    private readonly Dictionary<string, (DeviceConnectedEvent Event, long Request)> _pending = [];
    private readonly ConcurrentDictionary<string, long> _requests = new();
    private DevicePartsPromptWindow? _prompt;
    private string? _promptDeviceId;
    private long _promptRequest;
    private DateOnly _today = DateOnly.FromDateTime(DateTime.Now);
    private bool _disposed;

    public PartsPromptService(
        UsbMonitorService usb,
        DeviceBuildService builds,
        CollectionService collection,
        AppTimerService timer
    )
        : this(usb.DeviceList, builds, collection, timer)
    {
        usb.DeviceConnected += Connected;
        usb.ConnectionChanged += ConnectionChanged;
        _stopListening = () =>
        {
            usb.DeviceConnected -= Connected;
            usb.ConnectionChanged -= ConnectionChanged;
        };
    }

    internal PartsPromptService(
        IEnumerable<Device> devices,
        DeviceBuildService builds,
        CollectionService collection,
        AppTimerService timer
    )
    {
        (_devices, _builds, _collection, _timer) = (devices, builds, collection, timer);
        timer.SecondTick += OnSecondTick;
        builds.Changed += BuildsChanged;
    }

    internal static bool ShouldPrompt(DeviceConnectedEvent connection, DeviceBuildSnapshot? build) =>
        connection.Device.DeviceType == DeviceTypes.Keyboard
        && connection.IsReconnection
        && build is { Parts.IsEmpty: false }
        && (
            !connection.PreviousDisconnect.HasValue
            || connection.SessionStartedAt.ToUniversalTime() - connection.PreviousDisconnect.Value.ToUniversalTime()
                >= TimeSpan.FromSeconds(10)
        );

    private async void Connected(DeviceConnectedEvent connection)
    {
        try
        {
            await ReceiveAsync(connection).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not show keyboard parts prompt");
        }
    }

    internal async Task ReceiveAsync(DeviceConnectedEvent connection)
    {
        if (_disposed || connection.Device.DeviceType != DeviceTypes.Keyboard)
            return;
        var id = connection.Device.DeviceId;
        var dispatcher = Application.Current?.Dispatcher;
        if (!ShutdownDispose.IsDispatcherUsable(dispatcher))
            return;
        var request = await dispatcher!.InvokeAsync(
            () => !_disposed && MatchesConnection(connection) ? _requests.AddOrUpdate(id, 1, (_, old) => old + 1) : 0
        );
        if (request == 0)
            return;
        var day = DateOnly.FromDateTime(DateTime.Now);
        var initialized = await _builds.InitializeDayAsync(id, day).ConfigureAwait(false);
        if (!initialized.Succeeded)
            return;
        if (!ShutdownDispose.IsDispatcherUsable(dispatcher))
            return;
        await dispatcher!.InvokeAsync(() =>
        {
            if (_disposed || !IsCurrent(connection, request))
                return;
            _pending.Remove(id);
            if (_promptDeviceId == id)
                _prompt?.Close();
            if (ShouldPrompt(connection, _builds.GetCurrent(id, day)))
                _pending[id] = (connection, request);
            ShowNext();
        });
    }

    private bool IsCurrent(DeviceConnectedEvent connection, long request) =>
        _requests.GetValueOrDefault(connection.Device.DeviceId) == request && MatchesConnection(connection);

    private bool MatchesConnection(DeviceConnectedEvent connection) =>
        _devices.Any(x =>
            x.DeviceId == connection.Device.DeviceId
            && x.DeviceType == DeviceTypes.Keyboard
            && x.IsConnected
            && x.SessionStartedAt == connection.SessionStartedAt
        );

    internal void ConnectionChanged(string id)
    {
        _requests.AddOrUpdate(id, 1, (_, old) => old + 1);
        Dispatch(() =>
        {
            if (_disposed)
                return;
            if (_pending.TryGetValue(id, out var item) && !IsCurrent(item.Event, item.Request))
                _pending.Remove(id);
            if (_promptDeviceId == id && _promptRequest != _requests.GetValueOrDefault(id))
                _prompt?.Close();
        });
    }

    private void ShowNext()
    {
        if (_disposed || _prompt != null)
            return;
        while (_pending.Count > 0)
        {
            var next = _pending.First();
            _pending.Remove(next.Key);
            var (connection, request) = next.Value;
            var build = _builds.GetCurrent(next.Key, DateOnly.FromDateTime(DateTime.Now));
            if (!IsCurrent(connection, request) || !ShouldPrompt(connection, build))
                continue;
            var device = _devices.First(x => x.DeviceId == next.Key);
            var prompt = new DevicePartsPromptWindow(device, build!);
            _prompt = prompt;
            _promptDeviceId = next.Key;
            _promptRequest = request;
            prompt.ChangeRequested += () =>
            {
                if (IsCurrent(connection, request))
                    OpenEditor(device);
            };
            prompt.Closed += (_, _) =>
            {
                _prompt = null;
                _promptDeviceId = null;
                ShowNext();
            };
            prompt.Show();
            return;
        }
    }

    public void OpenEditor(Device device, DateOnly? day = null, Guid? buildKey = null)
    {
        if (_disposed || device.DeviceType != DeviceTypes.Keyboard || (!buildKey.HasValue && !device.IsConnected))
            return;
        var targetDay = day ?? DateOnly.FromDateTime(DateTime.Now);
        if (_editors.TryGetValue(device.DeviceId, out var existing))
        {
            var model = (DevicePartsEditorViewModel)existing.DataContext;
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Activate();
            if (model.Day == targetDay && model.EditBuildKey == buildKey)
                return;
            if (
                model.IsBusy
                || (
                    model.HasChanges
                    && MessageBox.Show(
                        existing,
                        "Discard the unsaved selection and open the requested build?",
                        "Open build",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    ) != MessageBoxResult.Yes
                )
            )
                return;
            existing.Close();
        }
        var editor = new DevicePartsEditorWindow(new(device, _builds, _collection, _timer, targetDay, buildKey));
        _editors.Add(device.DeviceId, editor);
        editor.Closed += (_, _) => _editors.Remove(device.DeviceId);
        editor.Show();
    }

    private async void OnSecondTick(object? sender, EventArgs e)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (_disposed || _today == today)
            return;
        _today = today;
        _pending.Clear();
        _prompt?.Close();
        foreach (var device in _devices.Where(x => x.DeviceType == DeviceTypes.Keyboard && x.IsConnected).ToArray())
            await _builds.InitializeDayAsync(device.DeviceId, today);
    }

    private void BuildsChanged(string id, DateOnly day) =>
        Dispatch(() =>
        {
            if (_disposed || _promptDeviceId != id || day != DateOnly.FromDateTime(DateTime.Now))
                return;
            var build = _builds.GetCurrent(id, day);
            if (build == null || build.Parts.IsEmpty)
                _prompt?.Close();
            else
                _prompt?.UpdateBuild(build);
        });

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (ShutdownDispose.IsDispatcherUsable(dispatcher))
            dispatcher!.BeginInvoke(action);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            Log.Debug("Parts prompts dispose skipped because it was already disposed");
            return;
        }
        _disposed = true;
        _stopListening?.Invoke();
        _timer.SecondTick -= OnSecondTick;
        _builds.Changed -= BuildsChanged;
        var dispatcher = Application.Current?.Dispatcher;
        if (!ShutdownDispose.IsDispatcherUsable(dispatcher))
            return;
        void CloseWindows()
        {
            _pending.Clear();
            _prompt?.Close();
            foreach (var editor in _editors.Values.ToArray())
                editor.Close();
            _editors.Clear();
        }
        if (dispatcher!.CheckAccess())
            CloseWindows();
        else
            dispatcher.BeginInvoke(CloseWindows);
    }
}
