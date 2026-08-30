using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using KeyPulse.Configuration;
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
    private readonly IDatabaseCredentialStore _databaseCredentialStore;
    private bool _launchOnLogin;
    private bool _autoInstallUpdates;
    private bool _closeToTray;
    private RetentionOption _selectedRetentionOption = RetentionOptions.All[0];
    private bool _isCheckingUpdates;
    private bool _isUpdateAvailable;
    private string? _latestUpdateVersion;
    private bool _suppressAutoSave;
    private DatabaseProvider _activeDatabaseProvider;
    private DatabaseProvider _selectedDatabaseProvider;
    private string _postgreSqlHost = "localhost";
    private int _postgreSqlPort = 5432;
    private string _postgreSqlDatabase = "";
    private string _postgreSqlUsername = "";
    private string _postgreSqlPassword = "";
    private PostgreSqlSslMode _postgreSqlSslMode = PostgreSqlSslMode.Prefer;
    private PostgreSqlConnectionSettings _loadedPostgreSql = new();

    public SettingsViewModel(
        AppSettingsService appSettingsService,
        StartupRegistrationService startupRegistrationService,
        UpdateService updateService,
        UsbMonitorService usbMonitorService,
        DataService dataService,
        IDatabaseCredentialStore databaseCredentialStore
    )
    {
        _appSettingsService = appSettingsService;
        _startupRegistrationService = startupRegistrationService;
        _updateService = updateService;
        _usbMonitorService = usbMonitorService;
        _dataService = dataService;
        _databaseCredentialStore = databaseCredentialStore;

        UpdateActionCommand = new AsyncRelayCommand(_ => RunUpdateActionAsync(), _ => !_isCheckingUpdates);
        UnhideDeviceCommand = new RelayCommand(ExecuteUnhideDevice, parameter => parameter is Device);
        TestDatabaseConnectionCommand = new AsyncRelayCommand(_ => TestDatabaseConnectionAsync());
        ApplyDatabaseCommand = new AsyncRelayCommand(_ => ApplyDatabaseAsync());

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

    public IReadOnlyList<PostgreSqlSslMode> PostgreSqlSslModeChoices { get; } = Enum.GetValues<PostgreSqlSslMode>();
    public ICommand TestDatabaseConnectionCommand { get; }
    public ICommand ApplyDatabaseCommand { get; }

    public DatabaseProvider SelectedDatabaseProvider
    {
        get => _selectedDatabaseProvider;
        set
        {
            if (_selectedDatabaseProvider == value)
                return;
            _selectedDatabaseProvider = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowPostgreSqlSettings));
            OnPropertyChanged(nameof(UseSqliteStorage));
            OnPropertyChanged(nameof(UsePostgreSqlStorage));
            OnPropertyChanged(nameof(IsStorageProviderChanged));
        }
    }

    public bool UseSqliteStorage
    {
        get => SelectedDatabaseProvider == DatabaseProvider.Sqlite;
        set
        {
            if (value)
                SelectedDatabaseProvider = DatabaseProvider.Sqlite;
        }
    }

    public bool UsePostgreSqlStorage
    {
        get => SelectedDatabaseProvider == DatabaseProvider.PostgreSql;
        set
        {
            if (value)
                SelectedDatabaseProvider = DatabaseProvider.PostgreSql;
        }
    }

    public bool IsStorageProviderChanged => SelectedDatabaseProvider != _activeDatabaseProvider;

    public bool ShowPostgreSqlSettings => SelectedDatabaseProvider == DatabaseProvider.PostgreSql;

    public string PostgreSqlHost
    {
        get => _postgreSqlHost;
        set => SetDatabaseField(ref _postgreSqlHost, value);
    }

    public int PostgreSqlPort
    {
        get => _postgreSqlPort;
        set => SetDatabaseField(ref _postgreSqlPort, value);
    }

    public string PostgreSqlDatabase
    {
        get => _postgreSqlDatabase;
        set => SetDatabaseField(ref _postgreSqlDatabase, value);
    }

    public string PostgreSqlUsername
    {
        get => _postgreSqlUsername;
        set => SetDatabaseField(ref _postgreSqlUsername, value);
    }

    public string PostgreSqlPassword
    {
        get => _postgreSqlPassword;
        set
        {
            if (_postgreSqlPassword == value)
                return;
            _postgreSqlPassword = value;
        }
    }

    public PostgreSqlSslMode PostgreSqlSslMode
    {
        get => _postgreSqlSslMode;
        set => SetDatabaseField(ref _postgreSqlSslMode, value);
    }

    private void SetDatabaseField<T>(
        ref T field,
        T value,
        [System.Runtime.CompilerServices.CallerMemberName] string? name = null
    )
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        OnPropertyChanged(name);
    }

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
            LoadDatabaseSettings(settings);

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

    private void LoadDatabaseSettings(AppUserSettings settings)
    {
        _activeDatabaseProvider = settings.DatabaseProvider;
        SelectedDatabaseProvider = settings.PendingDatabaseProvider ?? settings.DatabaseProvider;
        _loadedPostgreSql = settings.PostgreSql.Copy();
        PostgreSqlHost = settings.PostgreSql.Host;
        PostgreSqlPort = settings.PostgreSql.Port;
        PostgreSqlDatabase = settings.PostgreSql.Database;
        PostgreSqlUsername = settings.PostgreSql.Username;
        PostgreSqlSslMode = settings.PostgreSql.SslMode;
        PostgreSqlPassword = _databaseCredentialStore.ReadPostgreSqlPassword() ?? string.Empty;
        OnPropertyChanged(nameof(IsStorageProviderChanged));
    }

    public void CancelDatabaseChanges()
    {
        var settings = _appSettingsService.GetSettings();
        if (settings.PendingDatabaseProvider.HasValue)
        {
            settings.PendingDatabaseProvider = null;
            settings.PendingDatabaseImport = false;
            settings.PendingDatabaseSwitchId = null;
            _appSettingsService.SaveSettings(settings);
        }
        else
        {
            LoadDatabaseSettings(settings);
        }
    }

    private PostgreSqlConnectionSettings ReadPostgreSqlSettings() =>
        new()
        {
            Host = PostgreSqlHost.Trim(),
            Port = PostgreSqlPort,
            Database = PostgreSqlDatabase.Trim(),
            Username = PostgreSqlUsername.Trim(),
            SslMode = PostgreSqlSslMode,
        };

    private string ReadPostgreSqlPassword() =>
        string.IsNullOrEmpty(PostgreSqlPassword)
            ? throw new InvalidOperationException("Enter the PostgreSQL password")
            : PostgreSqlPassword;

    private async Task TestDatabaseConnectionAsync()
    {
        try
        {
            await DatabaseConfigurationService.TestPostgreSqlAsync(ReadPostgreSqlSettings(), ReadPostgreSqlPassword());
            ToastMessage = "Database connection successful.";
        }
        catch (Exception ex)
        {
            ToastMessage = $"Connection failed: {ex.Message}";
            Log.Debug(ex, "PostgreSQL connection test failed");
        }
    }

    private async Task ApplyDatabaseAsync()
    {
        try
        {
            var settings = _appSettingsService.GetSettings();
            if (SelectedDatabaseProvider == DatabaseProvider.PostgreSql)
            {
                var postgreSql = ReadPostgreSqlSettings();
                var password = ReadPostgreSqlPassword();
                await DatabaseConfigurationService.TestPostgreSqlAsync(postgreSql, password);

                if (_activeDatabaseProvider == DatabaseProvider.PostgreSql)
                {
                    var targetChanged =
                        postgreSql.Port != _loadedPostgreSql.Port
                        || !string.Equals(postgreSql.Host, _loadedPostgreSql.Host, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(
                            postgreSql.Database,
                            _loadedPostgreSql.Database,
                            StringComparison.OrdinalIgnoreCase
                        );
                    if (targetChanged)
                        throw new InvalidOperationException(
                            "Switch to SQLite before selecting a different PostgreSQL database"
                        );
                }

                _databaseCredentialStore.WritePostgreSqlPassword(password);
                settings.PostgreSql = postgreSql;
                if (_activeDatabaseProvider == DatabaseProvider.Sqlite)
                {
                    settings.PendingDatabaseProvider = DatabaseProvider.PostgreSql;
                    settings.PendingDatabaseImport = DatabaseConfigurationService.HasSqliteHistory();
                    settings.PendingDatabaseSwitchId = Guid.NewGuid().ToString("N");
                }
            }
            else if (_activeDatabaseProvider == DatabaseProvider.PostgreSql)
            {
                var answer = MessageBox.Show(
                    "The local SQLite backup is older than the active PostgreSQL database. Switch after restart anyway?",
                    AppConstants.App.DefaultName,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );
                if (answer != MessageBoxResult.Yes)
                    return;
                settings.PendingDatabaseProvider = DatabaseProvider.Sqlite;
                settings.PendingDatabaseImport = false;
                settings.PendingDatabaseSwitchId = null;
            }

            _appSettingsService.SaveSettings(settings);
            ToastMessage = "Database change saved. Restart KeyPulse to apply it.";
        }
        catch (Exception ex)
        {
            ToastMessage = $"Database change failed: {ex.Message}";
            Log.Warning(ex, "Database setting could not be saved");
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
            LoadDatabaseSettings(settings);
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
