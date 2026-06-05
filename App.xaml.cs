using System.ComponentModel;
using System.IO;
using System.Windows;
using KeyPulse.Configuration;
using KeyPulse.Data;
using KeyPulse.Helpers;
using KeyPulse.Services;
using KeyPulse.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace KeyPulse;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private UsbMonitorService? _usbMonitorService;
    private RawInputService? _rawInputService;
    private UpdateService? _updateService;
    private TrayIconService? _trayIconService;
    private static Mutex? _appMutex;
    private EventWaitHandle? _activateEvent;
    private volatile bool _isShuttingDown;
    private volatile bool _isSessionEnding;
    private string? _appName;
    private AppSettingsService? _appSettingsService;
    private StartupRegistrationService? _startupRegistrationService;
    private string? _promptedVersion;
    private static bool RunInBackground { get; set; }
    public static ServiceProvider ServiceProvider { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var startupStopwatch = System.Diagnostics.Stopwatch.StartNew();
        _appName = AppConstants.App.DefaultName;
        var instanceId = GetInstanceId(_appName);
        ConfigureLogging();
        Log.Information(AppConstants.Troubleshooting.SessionStartMarker);

        // Attempt clean shutdown on unhandled exceptions (crashes).
        // Force-kills (IDE stop, TerminateProcess) cannot be caught - RecoverFromCrash() handles those.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception");
            _rawInputService?.Dispose();
            _usbMonitorService?.Dispose();
        };
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Dispatcher unhandled exception");
            _rawInputService?.Dispose();
            _usbMonitorService?.Dispose();
        };

        _appMutex = new Mutex(true, instanceId, out var canCreateApp);
        if (!canCreateApp)
        {
            Log.Information("Secondary instance detected; signaling active instance");
            if (!SignalExistingInstance(instanceId))
            {
                Log.Warning("Failed to signal existing instance; showing already-running message");
                MessageBox.Show(
                    "The application is already running.",
                    _appName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }

            startupStopwatch.Stop();
            Log.Information("Application startup completed in {ElapsedMs}ms", startupStopwatch.ElapsedMilliseconds);
            Environment.Exit(0);
        }

        InitializeActivationSignalListener(instanceId);

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        _appSettingsService = ServiceProvider.GetRequiredService<AppSettingsService>();
        _startupRegistrationService = ServiceProvider.GetRequiredService<StartupRegistrationService>();
        _updateService = ServiceProvider.GetRequiredService<UpdateService>();
        _trayIconService = ServiceProvider.GetRequiredService<TrayIconService>();

        // Resolve startup mode with precedence: launch args > build default.
        RunInBackground = ResolveRunInBackground(e.Args);
        Log.Information("Startup mode resolved: RunInBackground={RunInBackground}", RunInBackground);

        SyncStartupRegistrationFromSettings();

        _usbMonitorService = ServiceProvider.GetRequiredService<UsbMonitorService>();

        // Run the daily-stats startup rebuild in the background so the window appears immediately.
        // First run does a one-time full historical backfill; later runs do a cheap drift-recovery pass.
        _ = Task.Run(() =>
        {
            ServiceProvider.GetRequiredService<DailyStatsService>().RunStartupRebuild();
        });

        // Show window / tray immediately so the UI appears while slow startup runs in the background.
        // First launch always shows the window, even in Release/tray mode.
        var settings = _appSettingsService.GetSettings();

        if (!RunInBackground || settings.IsFirstLaunch)
        {
            MainWindow = new MainWindow();
            MainWindow.Title = _appName;
            MainWindow.Closing += MainWindow_Closing;
            MainWindow.Show();

            // Mark first launch as done and save.
            if (settings.IsFirstLaunch)
            {
                settings.IsFirstLaunch = false;
                _appSettingsService.SaveSettings(settings);
            }
        }

        // Initialize tray if in background mode (either first launch or not).
        if (RunInBackground)
        {
            _trayIconService?.Initialize(ShowMainWindow, Shutdown);
        }

        // WMI device snapshot + watcher setup - awaited off the UI thread.
        // Failures here are logged but don't block; app continues in degraded mode.
        try
        {
            await _usbMonitorService.StartAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "USB monitoring startup failed");
            ShowStartupWarning(
                "Device monitoring failed to start completely. Some features may be unavailable. Check logs for details."
            );
        }

        _rawInputService = ServiceProvider.GetRequiredService<RawInputService>();

        // RawInputService startup handles its own exceptions internally.
        try
        {
            _rawInputService.Start();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Input tracking startup failed");
            ShowStartupWarning(
                "Activity tracking failed to start. The app will continue running but activity data may not be collected. Check logs for details."
            );
        }

        try
        {
            _updateService.UpdateStatusChanged += OnUpdateAvailable;
            _updateService.Start();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update checker startup failed");
            ShowStartupWarning(
                "Update checker failed to start. The app will continue running, and you can still try checking manually from Settings."
            );
        }

        startupStopwatch.Stop();
        Log.Information("Application startup completed in {ElapsedMs}ms", startupStopwatch.ElapsedMilliseconds);
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var shutdownStopwatch = System.Diagnostics.Stopwatch.StartNew();
        _isShuttingDown = true;
        Log.Information("Application shutdown started");
        try
        {
            if (_updateService != null)
                _updateService.UpdateStatusChanged -= OnUpdateAvailable;
            DisposeActivationSignalResources();
            DisposeMutexResources();
            DisposeServicesForExitPath();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during application shutdown");
        }
        finally
        {
            shutdownStopwatch.Stop();
            Log.Information("Application shutdown completed in {ElapsedMs}ms", shutdownStopwatch.ElapsedMilliseconds);
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    private void DisposeActivationSignalResources()
    {
        var activationEvent = _activateEvent;
        _activateEvent = null;
        ShutdownDispose.TryStep(() => activationEvent?.Dispose(), "activation event dispose");
    }

    private static void DisposeMutexResources()
    {
        ShutdownDispose.TryStep(() => _appMutex?.ReleaseMutex(), "app mutex release");
        ShutdownDispose.TryStep(() => _appMutex?.Dispose(), "app mutex dispose");
        _appMutex = null;
    }

    private void DisposeServicesForExitPath()
    {
        if (_isSessionEnding)
        {
            Log.Information(
                "Skipping service disposal during Windows session end to avoid blocking shutdown; crash recovery will reconcile state on next startup"
            );
            return;
        }

        ShutdownDispose.TryStep(ServiceProvider.Dispose, "service provider dispose");
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _isSessionEnding = true;
        Log.Information("Windows session ending detected: Reason={Reason}", e.ReasonSessionEnding);
        base.OnSessionEnding(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContextFactory<ApplicationDbContext>();
        services.AddSingleton<DailyStatsService>();
        services.AddSingleton<DataService>();
        services.AddSingleton<AppSettingsService>();
        services.AddSingleton<LogAccessService>();
        services.AddSingleton<StartupRegistrationService>();
        services.AddSingleton<UsbMonitorService>();
        services.AddSingleton<RawInputService>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<AppTimerService>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DeviceListViewModel>();
        services.AddTransient<EventLogViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<TroubleshootingViewModel>();
        services.AddTransient<CalendarViewModel>();
    }

    private static bool ResolveRunInBackground(IEnumerable<string> args)
    {
        return ShouldForceTrayFromArgs(args) || IsProductionBuild();
    }

    private static bool IsProductionBuild()
    {
#if DEBUG
        return false;
#else
        return true;
#endif
    }

    private static bool ShouldForceTrayFromArgs(IEnumerable<string> args)
    {
        return args.Any(arg =>
            string.Equals(arg, AppConstants.App.StartupArgument, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static string GetActivationEventName(string appName)
    {
        return $"{appName}{AppConstants.App.ActivationEventSuffix}";
    }

    private static string GetInstanceId(string appName)
    {
        return $"{appName}.{GetBuildModeName()}";
    }

    private static string GetBuildModeName()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static void ConfigureLogging()
    {
        try
        {
            var logDirectory = AppDataPaths.GetPath(AppConstants.Paths.LogsDirectoryName);
            Directory.CreateDirectory(logDirectory);

            var loggerConfiguration = new LoggerConfiguration().Enrich.FromLogContext().MinimumLevel.Debug();

            Log.Logger = loggerConfiguration
                .WriteTo.File(
                    Path.Combine(logDirectory, AppConstants.Paths.RollingLogFileTemplate),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: AppConstants.Paths.LogRetentionFileCountLimit,
                    shared: true
                )
                .CreateLogger();
        }
        catch
        {
            // Logging bootstrap must never block application startup.
        }
    }

    private void InitializeActivationSignalListener(string instanceId)
    {
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, GetActivationEventName(instanceId));
        _ = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, _) =>
            {
                if (_isShuttingDown || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                    return;

                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        if (!_isShuttingDown)
                            ShowMainWindow();
                    })
                );
            },
            null,
            Timeout.Infinite,
            false
        );
        Log.Debug("Activation signal listener started");
    }

    private static bool SignalExistingInstance(string instanceId)
    {
        try
        {
            using var activateEvent = EventWaitHandle.OpenExisting(GetActivationEventName(instanceId));
            return activateEvent.Set();
        }
        catch
        {
            Log.Warning("Activation signal event was not available for the current instance");
            return false;
        }
    }

    private void SyncStartupRegistrationFromSettings()
    {
        if (_appSettingsService == null || _startupRegistrationService == null)
            return;

        try
        {
            var settings = _appSettingsService.GetSettings();
            if (settings.LaunchOnLogin)
                _startupRegistrationService.Enable();
            else
                _startupRegistrationService.Disable();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to synchronize startup registration from settings");
            ShowStartupWarning("Launch on Login could not be synchronized. Check logs for details.");
        }
    }

    private void ShowStartupWarning(string message)
    {
        try
        {
            if (RunInBackground && _trayIconService != null)
                _trayIconService.ShowWarning(
                    "Startup Warning",
                    message,
                    AppConstants.App.StartupWarningBalloonTimeoutMs
                );
            else if (!RunInBackground && MainWindow != null)
                MainWindow.Dispatcher.BeginInvoke(() =>
                {
                    MessageBox.Show(message, _appName, MessageBoxButton.OK, MessageBoxImage.Warning);
                });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show startup warning");
        }
    }

    private void OnUpdateAvailable(UpdateService.UpdateAvailableEventArgs args)
    {
        if (!args.Available || string.IsNullOrWhiteSpace(args.LatestVersion))
            return;

        if (_isShuttingDown || !ShutdownDispose.IsDispatcherUsable(Dispatcher))
            return;

        var version = args.LatestVersion;
        Dispatcher.BeginInvoke(new Action(() => MaybePromptForUpdate(version)));
    }

    private async void MaybePromptForUpdate(string version)
    {
        if (_isShuttingDown || _updateService == null)
            return;

        // Prompt at most once per version per session.
        if (string.Equals(_promptedVersion, version, StringComparison.OrdinalIgnoreCase))
            return;

        // The setting gates only the proactive prompt; manual install from the tray/Settings still works when off.
        if (_appSettingsService?.GetSettings().AutoInstallUpdates != true)
            return;

        _promptedVersion = version;

        if (!PromptForUpdate(version))
            return;

        try
        {
            var installed = await _updateService.DownloadAndInstallAsync();
            if (!installed && !_isShuttingDown)
                NotifyUpdateFailed();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Automatic update failed for v{Version}", version);
            if (!_isShuttingDown)
                NotifyUpdateFailed();
        }
    }

    private bool PromptForUpdate(string version)
    {
        var message = $"KeyPulse Signal v{version} is available. Update now?";

        var main = MainWindow;
        if (main is { IsVisible: true })
            return MessageBox.Show(main, message, _appName, MessageBoxButton.YesNo, MessageBoxImage.Information)
                == MessageBoxResult.Yes;

        // Tray-only mode has no visible window, so use a transient off-screen topmost owner to guarantee
        // the prompt comes to the foreground.
        var owner = new Window
        {
            ShowInTaskbar = false,
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            Top = -2000,
            Left = -2000,
            Topmost = true,
        };

        try
        {
            owner.Show();
            return MessageBox.Show(owner, message, _appName, MessageBoxButton.YesNo, MessageBoxImage.Information)
                == MessageBoxResult.Yes;
        }
        finally
        {
            owner.Close();
        }
    }

    private void NotifyUpdateFailed()
    {
        const string message = "The update could not be installed automatically. You can retry from Settings.";
        if (RunInBackground && _trayIconService != null)
            _trayIconService.ShowWarning("Update", message, AppConstants.App.StartupWarningBalloonTimeoutMs);
        else if (MainWindow != null)
            MessageBox.Show(MainWindow, message, _appName, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ShowMainWindow()
    {
        if (MainWindow == null)
        {
            MainWindow = new MainWindow();
            MainWindow.Title = _appName;
            MainWindow.Closing += MainWindow_Closing;
        }

        if (MainWindow.WindowState == WindowState.Minimized)
            MainWindow.WindowState = WindowState.Normal;

        if (!MainWindow.IsVisible)
            MainWindow.Show();

        MainWindow.Topmost = true;
        MainWindow.Topmost = false;
        MainWindow.Activate();
        MainWindow.Focus();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (RunInBackground)
        {
            e.Cancel = true;
            MainWindow?.Hide();
        }
    }
}
