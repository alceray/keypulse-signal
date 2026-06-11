using System.Drawing;
using System.IO;
using System.Windows.Forms;
using KeyPulse.Configuration;
using KeyPulse.Helpers;
using MahApps.Metro.IconPacks;
using Serilog;

namespace KeyPulse.Services;

public sealed class TrayIconService(UpdateService updateService, RawInputService rawInputService) : IDisposable
{
    private NotifyIcon? _trayIcon;
    private ToolStripMenuItem? _updateMenuItem;
    private ToolStripMenuItem? _pauseMenuItem;
    private Bitmap? _pauseImage;
    private Bitmap? _resumeImage;
    private bool _disposed;

    private const string PauseMenuText = "Pause input tracking";
    private const string ResumeMenuText = "Resume input tracking";

    // Rendered at 2x the 16px menu slot so it stays crisp when the menu scales the image up for DPI.
    private const int PauseIconSizePx = 32;
    private static readonly System.Windows.Media.Color PauseIconColor = System.Windows.Media.Color.FromRgb(
        0x33,
        0x33,
        0x33
    );

    public void Initialize(Action showMainWindow, Action shutdown)
    {
        if (_trayIcon != null)
            return;

        var trayIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppConstants.App.TrayIconRelativePath);
        _trayIcon = new NotifyIcon
        {
            Icon = new Icon(trayIconPath),
            Visible = true,
            Text = AppConstants.App.DefaultName,
            ContextMenuStrip = new ContextMenuStrip { ShowItemToolTips = true },
        };

        _updateMenuItem = new ToolStripMenuItem("No updates available") { Enabled = false };
        _updateMenuItem.Click += (_, _) => updateService.InstallUpdate();
        _trayIcon.ContextMenuStrip.Items.Add(_updateMenuItem);

        LoadPauseIcons();
        _pauseMenuItem = new ToolStripMenuItem(PauseMenuText)
        {
            ToolTipText = "Resumes automatically the next time the app starts.",
        };
        _pauseMenuItem.Click += (_, _) => rawInputService.TogglePause();
        _trayIcon.ContextMenuStrip.Items.Add(_pauseMenuItem);

        _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => shutdown());
        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
                showMainWindow();
        };

        updateService.UpdateStatusChanged += OnUpdateStatusChanged;
        UpdateUpdateMenuItem(updateService.UpdateAvailable, updateService.LatestVersion);

        rawInputService.PauseStateChanged += ApplyPauseState;
        ApplyPauseState(rawInputService.IsPaused);

        Log.Information("Tray icon initialized");
    }

    private void OnUpdateStatusChanged(UpdateService.UpdateAvailableEventArgs args)
    {
        UpdateUpdateMenuItem(args.Available, args.LatestVersion);
    }

    private void UpdateUpdateMenuItem(bool available, string? latestVersion)
    {
        if (_updateMenuItem == null)
            return;

        void Apply()
        {
            if (available)
            {
                _updateMenuItem.Text = $"Install update (v{latestVersion})";
                _updateMenuItem.Enabled = true;
            }
            else
            {
                _updateMenuItem.Text = "No updates available";
                _updateMenuItem.Enabled = false;
            }
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (!ShutdownDispose.IsDispatcherUsable(dispatcher))
            return;

        if (dispatcher!.CheckAccess())
        {
            Apply();
            return;
        }

        dispatcher.BeginInvoke(new Action(Apply));
    }

    // Pause shows while tracking (click pauses); play shows while paused (click resumes).
    private void LoadPauseIcons()
    {
        try
        {
            _pauseImage = PhosphorIconRenderer.Render(PackIconPhosphorIconsKind.Pause, PauseIconSizePx, PauseIconColor);
            _resumeImage = PhosphorIconRenderer.Render(PackIconPhosphorIconsKind.Play, PauseIconSizePx, PauseIconColor);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to render tray pause icons; the menu item will show text only");
        }
    }

    private void ApplyPauseState(bool paused)
    {
        void Apply()
        {
            if (_pauseMenuItem != null)
            {
                _pauseMenuItem.Text = paused ? ResumeMenuText : PauseMenuText;
                _pauseMenuItem.Image = paused ? _resumeImage : _pauseImage;
            }

            if (_trayIcon != null)
                _trayIcon.Text = paused ? $"{AppConstants.App.DefaultName} (paused)" : AppConstants.App.DefaultName;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (!ShutdownDispose.IsDispatcherUsable(dispatcher))
            return;

        if (dispatcher!.CheckAccess())
        {
            Apply();
            return;
        }

        dispatcher.BeginInvoke(new Action(Apply));
    }

    public void ShowWarning(string title, string message, int timeoutMs)
    {
        if (_trayIcon == null)
            return;

        _trayIcon.ShowBalloonTip(timeoutMs, title, message, ToolTipIcon.Warning);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            Log.Debug("Tray icon dispose skipped because it was already disposed");
            return;
        }

        _disposed = true;
        updateService.UpdateStatusChanged -= OnUpdateStatusChanged;
        rawInputService.PauseStateChanged -= ApplyPauseState;

        _pauseImage?.Dispose();
        _resumeImage?.Dispose();
        _pauseImage = null;
        _resumeImage = null;

        if (_trayIcon == null)
            return;

        _trayIcon.Dispose();
        _trayIcon = null;
        Log.Information("Tray icon disposed");
    }
}
