using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.Services;

namespace KeyPulse.ViewModels;

public sealed class DevicePartChoice(BuildPartSnapshot part, bool selected, bool removed) : ObservableObject
{
    private bool _selected = selected;
    public BuildPartSnapshot Part { get; } = part;
    public string Label => Part.Label;
    public string Status => removed ? "Removed from collection" : "";
    public bool IsSelected
    {
        get => _selected;
        set
        {
            if (_selected == value)
                return;
            _selected = value;
            OnPropertyChanged();
        }
    }
}

public sealed class DevicePartsEditorViewModel : ToastMessageViewModelBase
{
    private readonly Device _device;
    private readonly DeviceBuildService _builds;
    private readonly CollectionService _collection;
    private readonly AppTimerService _timer;
    private readonly Func<DateOnly> _today;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private DeviceBuildDaySnapshot? _captured;
    private HashSet<Guid> _saved = [];
    private string _switchSearch = "",
        _keycapSearch = "";
    private bool _busy,
        _loaded,
        _conflict,
        _disposed;
    public DateOnly Day { get; private set; }
    public Guid? EditBuildKey { get; }
    public string Title =>
        $"{(EditBuildKey.HasValue ? "Edit build" : "Add build")} for {_device.DeviceName} · {Day:yyyy-MM-dd}";
    public ObservableCollection<DevicePartChoice> Choices { get; } = [];
    public IEnumerable<DevicePartChoice> Switches => Filter(PartKinds.Switch, _switchSearch);
    public IEnumerable<DevicePartChoice> Keycaps => Filter(PartKinds.Keycap, _keycapSearch);
    public string SwitchSearch
    {
        get => _switchSearch;
        set
        {
            _switchSearch = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Switches));
        }
    }
    public string KeycapSearch
    {
        get => _keycapSearch;
        set
        {
            _keycapSearch = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Keycaps));
        }
    }
    public bool IsBusy => _busy;
    public bool CanEdit => _loaded && !_busy;
    public bool HasChanges => !_saved.SetEquals(Choices.Where(x => x.IsSelected).Select(x => x.Part.ItemKey));
    public bool HasConflict => _conflict;
    public bool NeedsRetry => !_loaded && !_busy;
    public string Status =>
        _busy ? "Loading or saving parts…"
        : _conflict ? "This day or its builds changed. Reload to continue; your selection is still here."
        : !_loaded ? "Parts could not be loaded. Retry to continue."
        : !EditBuildKey.HasValue && !_device.IsConnected ? "Connect this keyboard before adding a build."
        : EditBuildKey.HasValue ? "Save changes this build only. Other days stay unchanged."
        : "Save adds a build for today. Earlier builds stay in Calendar.";
    public ICommand SaveCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand CancelCommand { get; }

    public DevicePartsEditorViewModel(
        Device device,
        DeviceBuildService builds,
        CollectionService collection,
        AppTimerService timer,
        DateOnly day,
        Guid? editBuildKey = null,
        Func<DateOnly>? today = null
    )
    {
        (_device, _builds, _collection, _timer, Day, EditBuildKey) = (
            device,
            builds,
            collection,
            timer,
            day,
            editBuildKey
        );
        _today = today ?? (() => DateOnly.FromDateTime(DateTime.Now));
        SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave);
        ReloadCommand = new RelayCommand(async _ => await ReloadAsync(), _ => !_busy);
        CancelCommand = new RelayCommand(
            _ =>
            {
                foreach (var choice in Choices)
                    choice.IsSelected = _saved.Contains(choice.Part.ItemKey);
            },
            _ => !_busy && HasChanges
        );
        _builds.Changed += BuildsChanged;
        _device.PropertyChanged += DeviceChanged;
        _timer.SecondTick += OnSecondTick;
    }

    private bool CanSave => CanEdit && HasChanges && !_conflict && (EditBuildKey.HasValue || _device.IsConnected);

    private IEnumerable<DevicePartChoice> Filter(string kind, string search) =>
        Choices
            .Where(x => x.Part.PartKind == kind && x.Label.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(x => x.IsSelected)
            .ThenBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public async Task ReloadAsync()
    {
        if (_busy || _disposed)
            return;
        _busy = true;
        _loaded = false;
        _conflict = false;
        if (!EditBuildKey.HasValue)
            Day = _today();
        NotifyState();
        var loaded = await _builds.LoadAsync(retry: true);
        if (loaded && !EditBuildKey.HasValue && _device.IsConnected)
            loaded = (await _builds.InitializeDayAsync(_device.DeviceId, Day)).Succeeded;
        _captured = _builds.GetDay(_device.DeviceId, Day);
        var current = EditBuildKey.HasValue
            ? _captured?.Builds.FirstOrDefault(x => x.BuildKey == EditBuildKey)
            : _captured?.Current;
        var owned = await _collection.LoadAsync(retry: true, reload: true);
        if (_disposed)
            return;
        if (loaded && owned.Succeeded && (!EditBuildKey.HasValue || current != null))
        {
            foreach (var choice in Choices)
                choice.PropertyChanged -= ChoiceChanged;
            Choices.Clear();
            var saved = current?.Parts.ToDictionary(x => x.ItemKey) ?? [];
            _saved = saved.Keys.ToHashSet();
            var ownedKeys = owned.Items.Select(x => x.ItemKey).ToHashSet();
            foreach (
                var part in owned
                    .Items.Where(x => x.PartKind is PartKinds.Switch or PartKinds.Keycap)
                    .Select(x =>
                        saved.GetValueOrDefault(x.ItemKey)
                        ?? new BuildPartSnapshot(x.ItemKey, x.PartKind, x.Name, x.Variant)
                    )
                    .Concat(saved.Values.Where(x => !ownedKeys.Contains(x.ItemKey)))
            )
            {
                var choice = new DevicePartChoice(
                    part,
                    _saved.Contains(part.ItemKey),
                    !ownedKeys.Contains(part.ItemKey)
                );
                choice.PropertyChanged += ChoiceChanged;
                Choices.Add(choice);
            }
            _loaded = true;
        }
        else
            ToastMessage =
                _builds.LoadError
                ?? owned.Message
                ?? (
                    EditBuildKey.HasValue && current == null
                        ? "This build was deleted. Close this editor."
                        : "Could not load builds."
                );
        _busy = false;
        CheckContext();
        NotifyState();
    }

    public async Task SaveAsync()
    {
        CheckContext();
        NotifyState();
        if (!CanSave)
            return;
        _busy = true;
        NotifyState();
        var result = await _builds.SaveAsync(
            _device.DeviceId,
            Day,
            _captured,
            EditBuildKey,
            Choices.Where(x => x.IsSelected).Select(x => x.Part.ItemKey).ToArray()
        );
        _busy = false;
        if (_disposed)
            return;
        if (result.Succeeded)
        {
            await ReloadAsync();
            ToastMessage = "Saved";
        }
        else
        {
            _conflict |= result.Status == BuildResultStatus.Conflict;
            ToastMessage = result.Error ?? "Could not save builds.";
            NotifyState();
        }
    }

    private void ChoiceChanged(object? sender, PropertyChangedEventArgs e) => NotifyState();

    private void DeviceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Device.IsConnected))
            RefreshState();
    }

    private void BuildsChanged(string deviceId, DateOnly day)
    {
        if (deviceId == _device.DeviceId && day == Day)
            RefreshState();
    }

    private void OnSecondTick(object? sender, EventArgs e)
    {
        if (!EditBuildKey.HasValue && Day != _today() && !_conflict)
            RefreshState();
    }

    private void RefreshState()
    {
        if (ShutdownDispose.IsDispatcherUsable(_dispatcher))
            _dispatcher.BeginInvoke(() =>
            {
                if (!_disposed && !_busy)
                {
                    CheckContext();
                    NotifyState();
                }
            });
    }

    private void CheckContext()
    {
        var latest = _builds.GetDay(_device.DeviceId, Day);
        _conflict |=
            (!EditBuildKey.HasValue && Day != _today())
            || latest?.DayKey != _captured?.DayKey
            || latest?.Revision != _captured?.Revision;
    }

    private void NotifyState()
    {
        foreach (
            var property in new[]
            {
                nameof(Title),
                nameof(Switches),
                nameof(Keycaps),
                nameof(IsBusy),
                nameof(CanEdit),
                nameof(HasChanges),
                nameof(HasConflict),
                nameof(NeedsRetry),
                nameof(Status),
            }
        )
            OnPropertyChanged(property);
        CommandManager.InvalidateRequerySuggested();
    }

    public override void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _builds.Changed -= BuildsChanged;
        _device.PropertyChanged -= DeviceChanged;
        _timer.SecondTick -= OnSecondTick;
        foreach (var choice in Choices)
            choice.PropertyChanged -= ChoiceChanged;
        base.Dispose();
    }
}
