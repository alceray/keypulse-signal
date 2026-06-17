using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.ViewModels.Settings;
using Serilog;

namespace KeyPulse.ViewModels;

public class SettingsViewModel : ToastMessageViewModelBase
{
    private readonly AppSettingsService _appSettingsService;
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly UpdateService _updateService;
    private readonly UsbMonitorService _usbMonitorService;
    private readonly DataService _dataService;
    private bool _launchOnLogin;
    private bool _autoInstallUpdates;
    private bool _closeToTray;
    private RetentionOption _selectedRetentionOption = RetentionOptions.All[0];
    private bool _isCheckingUpdates;
    private bool _isUpdateAvailable;
    private string? _latestUpdateVersion;
    private bool _suppressAutoSave;

    public SettingsViewModel(
        AppSettingsService appSettingsService,
        StartupRegistrationService startupRegistrationService,
        UpdateService updateService,
        UsbMonitorService usbMonitorService,
        DataService dataService
    )
    {
        _appSettingsService = appSettingsService;
        _startupRegistrationService = startupRegistrationService;
        _updateService = updateService;
        _usbMonitorService = usbMonitorService;
        _dataService = dataService;

        UpdateActionCommand = new AsyncRelayCommand(_ => RunUpdateActionAsync(), _ => !_isCheckingUpdates);
        UnhideDeviceCommand = new RelayCommand(ExecuteUnhideDevice, parameter => parameter is Device);

        _appSettingsService.SettingsChanged += OnSettingsChanged;
        _updateService.UpdateStatusChanged += OnUpdateStatusChanged;

        // Keep the hidden list live rather than snapshotting it: this view-model lives for the
        // process, and the device list fills in after startup and changes as devices are hidden.
        foreach (var device in _usbMonitorService.DeviceList)
            device.PropertyChanged += Device_PropertyChanged;
        _usbMonitorService.DeviceList.CollectionChanged += DeviceList_CollectionChanged;
        RebuildHiddenDevices();

        _isUpdateAvailable = _updateService.UpdateAvailable;
        _latestUpdateVersion = _updateService.LatestVersion;

        LoadSettings();
    }

    public bool LaunchOnLogin
    {
        get => _launchOnLogin;
        set
        {
            if (_launchOnLogin == value)
                return;

            _launchOnLogin = value;
            OnPropertyChanged();

            if (!_suppressAutoSave)
                SaveSettings(nameof(AppUserSettings.LaunchOnLogin), value);
        }
    }

    public bool AutoInstallUpdates
    {
        get => _autoInstallUpdates;
        set
        {
            if (_autoInstallUpdates == value)
                return;

            _autoInstallUpdates = value;
            OnPropertyChanged();

            if (!_suppressAutoSave)
                SaveSettings(nameof(AppUserSettings.AutoInstallUpdates), value);
        }
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set
        {
            if (_closeToTray == value)
                return;

            _closeToTray = value;
            OnPropertyChanged();

            if (!_suppressAutoSave)
                SaveSettings(nameof(AppUserSettings.CloseToTray), value);
        }
    }

    // Close-to-tray only has meaning when a tray exists. Windowed sessions have no tray, so closing
    // always exits there; hide the option rather than show a control that does nothing.
    public bool ShowCloseToTrayOption => App.RunInBackground;

    // Devices hidden from the dashboard and calendar, surfaced here so they can be unhidden.
    public ObservableCollection<Device> HiddenDevices { get; } = new();

    public bool HasHiddenDevices => HiddenDevices.Count > 0;

    public ICommand UnhideDeviceCommand { get; }

    public IReadOnlyList<RetentionOption> RetentionChoices => RetentionOptions.All;

    public RetentionOption SelectedRetentionOption
    {
        get => _selectedRetentionOption;
        set
        {
            if (_selectedRetentionOption == value || value is null)
                return;

            _selectedRetentionOption = value;
            OnPropertyChanged();

            if (!_suppressAutoSave)
                SaveSettings(nameof(AppUserSettings.ActivityRetentionMonths), value.Months);
        }
    }

    public string CurrentVersionDisplay => $"Version: v{_updateService.CurrentVersion}";

    public string UpdateActionButtonText =>
        _isUpdateAvailable && !string.IsNullOrWhiteSpace(_latestUpdateVersion)
            ? $"Update to v{_latestUpdateVersion}"
            : "Check for Updates";

    public ICommand UpdateActionCommand { get; }

    private async Task RunUpdateActionAsync()
    {
        if (_isCheckingUpdates)
            return;

        if (_isUpdateAvailable && !string.IsNullOrWhiteSpace(_latestUpdateVersion))
        {
            _updateService.InstallUpdate();
            return;
        }

        try
        {
            _isCheckingUpdates = true;
            AsyncRelayCommand.RaiseCanExecuteChanged();
            await _updateService.CheckForUpdatesAsync();
            SyncUpdateStateFromService();

            if (!_isUpdateAvailable)
                ToastMessage = "No new updates available.";
        }
        catch (Exception ex)
        {
            ToastMessage = "Update check failed. Check logs for details.";
            Log.Error(ex, "Manual update check failed");
        }
        finally
        {
            _isCheckingUpdates = false;
            AsyncRelayCommand.RaiseCanExecuteChanged();
        }
    }

    private void OnUpdateStatusChanged(UpdateService.UpdateAvailableEventArgs args)
    {
        void Apply()
        {
            _isUpdateAvailable = args.Available;
            _latestUpdateVersion = args.LatestVersion;
            OnPropertyChanged(nameof(UpdateActionButtonText));
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            Apply();
            return;
        }

        dispatcher.BeginInvoke(new Action(Apply));
    }

    private void SyncUpdateStateFromService()
    {
        _isUpdateAvailable = _updateService.UpdateAvailable;
        _latestUpdateVersion = _updateService.LatestVersion;
        OnPropertyChanged(nameof(UpdateActionButtonText));
    }

    private void LoadSettings()
    {
        _suppressAutoSave = true;
        try
        {
            var settings = _appSettingsService.GetSettings();
            LaunchOnLogin = settings.LaunchOnLogin;
            AutoInstallUpdates = settings.AutoInstallUpdates;
            CloseToTray = settings.CloseToTray;
            SelectedRetentionOption = RetentionOptions.FromMonths(settings.ActivityRetentionMonths);

            // Reflect the actual registration state so the UI matches the machine state.
            if (!_startupRegistrationService.IsEnabled() && LaunchOnLogin)
                LaunchOnLogin = false;

            ToastMessage = string.Empty;
        }
        finally
        {
            _suppressAutoSave = false;
        }
    }

    private void SaveSettings(string changedSetting, object changedValue)
    {
        try
        {
            // Read-modify-write so fields not edited on this page are preserved.
            var settings = _appSettingsService.GetSettings();
            settings.LaunchOnLogin = LaunchOnLogin;
            settings.AutoInstallUpdates = AutoInstallUpdates;
            settings.CloseToTray = CloseToTray;
            settings.ActivityRetentionMonths = SelectedRetentionOption.Months;

            _appSettingsService.SaveSettings(settings);

            if (settings.LaunchOnLogin)
                _startupRegistrationService.Enable();
            else
                _startupRegistrationService.Disable();

            ToastMessage = "Settings saved.";
            Log.Debug("Setting updated: {Setting}={Value}", changedSetting, changedValue);
        }
        catch (Exception ex)
        {
            ToastMessage = "Failed to save settings. Check logs for details.";
            Log.Error(ex, "Failed to save settings");
        }
    }

    private void OnSettingsChanged(AppUserSettings settings)
    {
        _suppressAutoSave = true;
        try
        {
            LaunchOnLogin = settings.LaunchOnLogin;
            AutoInstallUpdates = settings.AutoInstallUpdates;
            CloseToTray = settings.CloseToTray;
            SelectedRetentionOption = RetentionOptions.FromMonths(settings.ActivityRetentionMonths);
        }
        finally
        {
            _suppressAutoSave = false;
        }
    }

    private void Device_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Device.IsHiddenFromDisplay))
            RunOnUiThread(RebuildHiddenDevices);
    }

    private void DeviceList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (Device device in e.NewItems)
                device.PropertyChanged += Device_PropertyChanged;

        if (e.OldItems != null)
            foreach (Device device in e.OldItems)
                device.PropertyChanged -= Device_PropertyChanged;

        RunOnUiThread(RebuildHiddenDevices);
    }

    private void RebuildHiddenDevices()
    {
        HiddenDevices.Clear();
        foreach (var device in _usbMonitorService.DeviceList.Where(d => d.IsHiddenFromDisplay))
            HiddenDevices.Add(device);

        OnPropertyChanged(nameof(HasHiddenDevices));
    }

    private void ExecuteUnhideDevice(object? parameter)
    {
        if (parameter is not Device device)
            return;

        // Only flip the shared in-memory Device after the DB write succeeds; that same instance
        // feeds the dashboard and calendar, so the change propagates there too.
        if (_dataService.SetDeviceHiddenFromDisplay(device.DeviceId, false))
            device.IsHiddenFromDisplay = false;
    }

    // The device list is mutated from UsbMonitorService background callbacks, so marshal onto the
    // UI thread before touching the bound HiddenDevices collection.
    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    public override void Dispose()
    {
        base.Dispose();
        _appSettingsService.SettingsChanged -= OnSettingsChanged;
        _updateService.UpdateStatusChanged -= OnUpdateStatusChanged;

        foreach (var device in _usbMonitorService.DeviceList)
            device.PropertyChanged -= Device_PropertyChanged;
        _usbMonitorService.DeviceList.CollectionChanged -= DeviceList_CollectionChanged;
    }
}
