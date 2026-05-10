using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using KeyPulse.Configuration;
using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.ViewModels.Dashboard;
using OxyPlot;

namespace KeyPulse.ViewModels;

/// <summary>
/// Orchestrates dashboard data refresh, chart models, and live hover metadata.
/// </summary>
public sealed class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly DataService _dataService;
    private readonly UsbMonitorService _usbMonitorService;
    private readonly AppTimerService _appTimerService;

    public ICommand RefreshCommand { get; }

    public IPlotController PieHoverController { get; }

    public IReadOnlyList<string> RangeOptions => DashboardRangeResolver.RangeOptions;

    public string SelectedRange
    {
        get => _selectedRange;
        set
        {
            if (_selectedRange == value)
                return;

            _selectedRange = value;
            OnPropertyChanged();
            Refresh();
        }
    }

    public PlotModel KeyboardConnectionTimePiePlot
    {
        get => _keyboardConnectionTimePiePlot;
        private set
        {
            _keyboardConnectionTimePiePlot = value;
            OnPropertyChanged();
        }
    }

    public PlotModel MouseConnectionTimePiePlot
    {
        get => _mouseConnectionTimePiePlot;
        private set
        {
            _mouseConnectionTimePiePlot = value;
            OnPropertyChanged();
        }
    }

    public PlotModel InputActivityPlot
    {
        get => _inputActivityPlot;
        private set
        {
            _inputActivityPlot = value;
            OnPropertyChanged();
        }
    }

    public string HoveredDeviceName => _hoverPreview.DeviceName;

    public string HoveredStatusTag => _hoverPreview.StatusTag;

    public Brush HoveredStatusBrush => _hoverPreview.StatusBrush;

    public string HoveredConnectedTimeDisplay => _hoverPreview.ConnectedTimeDisplay;

    public string HoveredShareDisplay => _hoverPreview.ShareDisplay;

    public string HoveredConnectionText => _hoverPreview.ConnectionText;

    public int ConnectedDevices
    {
        get => _connectedDevices;
        private set
        {
            _connectedDevices = value;
            OnPropertyChanged();
        }
    }

    public string ConnectedDevicesBreakdown
    {
        get => _connectedDevicesBreakdown;
        private set
        {
            _connectedDevicesBreakdown = value;
            OnPropertyChanged();
        }
    }

    public string TopKeyboardsSummary
    {
        get => _topKeyboardsSummary;
        private set
        {
            _topKeyboardsSummary = value;
            OnPropertyChanged();
        }
    }

    public string TopMiceSummary
    {
        get => _topMiceSummary;
        private set
        {
            _topMiceSummary = value;
            OnPropertyChanged();
        }
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set
        {
            _lastUpdatedText = value;
            OnPropertyChanged();
        }
    }

    public string RangeDisplayText
    {
        get => _rangeDisplayText;
        private set
        {
            _rangeDisplayText = value;
            OnPropertyChanged();
        }
    }

    private PlotModel _keyboardConnectionTimePiePlot = new();
    private PlotModel _mouseConnectionTimePiePlot = new();
    private PlotModel _inputActivityPlot = new();
    private int _connectedDevices;
    private string _connectedDevicesBreakdown = "0 keyboards, 0 mice";
    private string _topKeyboardsSummary = "1. -\n2. -\n3. -";
    private string _topMiceSummary = "1. -\n2. -\n3. -";
    private string _lastUpdatedText = "";
    private string _rangeDisplayText = "";
    private string _selectedRange = DashboardRangeResolver.DefaultRange;
    private readonly DashboardHoverPreview _hoverPreview = new();

    public DashboardViewModel(
        DataService dataService,
        UsbMonitorService usbMonitorService,
        AppTimerService appTimerService
    )
    {
        _dataService = dataService;
        _usbMonitorService = usbMonitorService;
        _appTimerService = appTimerService;
        PieHoverController = DashboardPieChartBuilder.BuildPieHoverController();
        _hoverPreview.PropertyChanged += HoverPreview_PropertyChanged;

        RefreshCommand = new RelayCommand(_ => Refresh());

        // Subscribe reactively so ConnectedDevices updates the moment a device connects,
        // even if the timer hasn't fired yet (fixes the 0-on-startup issue).
        _usbMonitorService.DeviceList.CollectionChanged += DeviceList_CollectionChanged;
        foreach (var device in _usbMonitorService.DeviceList)
            device.PropertyChanged += Device_PropertyChanged;

        _appTimerService.ThirtySecondTick += OnRefreshTick;

        Refresh();
    }

    /// <summary>
    /// Forwards hover-preview property changes to the view model surface used by XAML bindings.
    /// </summary>
    private void HoverPreview_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DashboardHoverPreview.DeviceName):
                OnPropertyChanged(nameof(HoveredDeviceName));
                break;
            case nameof(DashboardHoverPreview.StatusTag):
                OnPropertyChanged(nameof(HoveredStatusTag));
                break;
            case nameof(DashboardHoverPreview.StatusBrush):
                OnPropertyChanged(nameof(HoveredStatusBrush));
                break;
            case nameof(DashboardHoverPreview.ConnectedTimeDisplay):
                OnPropertyChanged(nameof(HoveredConnectedTimeDisplay));
                break;
            case nameof(DashboardHoverPreview.ShareDisplay):
                OnPropertyChanged(nameof(HoveredShareDisplay));
                break;
            case nameof(DashboardHoverPreview.ConnectionText):
                OnPropertyChanged(nameof(HoveredConnectionText));
                break;
        }
    }

    // Called immediately when any device's IsConnected changes.
    private void Device_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Device.IsConnected))
            Refresh();
    }

    private void DeviceList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (Device device in e.NewItems)
                device.PropertyChanged += Device_PropertyChanged;

        if (e.OldItems != null)
            foreach (Device device in e.OldItems)
                device.PropertyChanged -= Device_PropertyChanged;
    }

    private void UpdateConnectedDevicesCount()
    {
        var connectedDevices = _usbMonitorService.DeviceList.Where(d => d.IsConnected).ToList();
        ConnectedDevices = connectedDevices.Count;

        var activeKeyboards = connectedDevices.Count(d => d.DeviceType == DeviceTypes.Keyboard);
        var activeMice = connectedDevices.Count(d => d.DeviceType == DeviceTypes.Mouse);
        var keyboardWord = activeKeyboards == 1 ? "keyboard" : "keyboards";
        var mouseWord = activeMice == 1 ? "mouse" : "mice";
        ConnectedDevicesBreakdown = $"{activeKeyboards} {keyboardWord}, {activeMice} {mouseWord}";
    }

    /// <summary>
    /// Rebuilds dashboard metrics and chart models for the current range and chart settings.
    /// </summary>
    public void Refresh()
    {
        var now = DateTime.Now;
        var from = DashboardRangeResolver.ResolveRangeStart(SelectedRange, now);
        var to = now;
        RangeDisplayText = TimeFormatter.FormatDateRange(from, to);

        var snapshots = _dataService.GetActivitySnapshots(from: from, to: to).ToList();
        var dashboardEvents = _dataService.GetDashboardEvents(to);
        var events = dashboardEvents.DeviceEvents;

        var devices = _usbMonitorService.DeviceList.ToList();

        UpdateConnectedDevicesCount();
        TopKeyboardsSummary = BuildTopConnectionSecondsSummary(devices, DeviceTypes.Keyboard);
        TopMiceSummary = BuildTopConnectionSecondsSummary(devices, DeviceTypes.Mouse);

        var connectionMinutesByDevice = DashboardConnectionTimeCalculator.ComputeConnectionMinutesByDevice(
            events,
            from,
            to
        );

        var keyboardModel = DashboardPieChartBuilder.BuildConnectionTimePiePlot(
            "Keyboards",
            devices.Where(d => d.DeviceType == DeviceTypes.Keyboard),
            connectionMinutesByDevice
        );
        var mouseModel = DashboardPieChartBuilder.BuildConnectionTimePiePlot(
            "Mice",
            devices.Where(d => d.DeviceType == DeviceTypes.Mouse),
            connectionMinutesByDevice
        );

        DashboardPieChartBuilder.AttachTrackerPreview(keyboardModel, _hoverPreview.UpdateFromSlice);
        DashboardPieChartBuilder.AttachTrackerPreview(mouseModel, _hoverPreview.UpdateFromSlice);

        KeyboardConnectionTimePiePlot = keyboardModel;
        MouseConnectionTimePiePlot = mouseModel;
        InputActivityPlot = DashboardActivityChartBuilder.BuildInputActivityPlot(
            snapshots,
            dashboardEvents.AppLifecycleEvents,
            from,
            to
        );

        LastUpdatedText = $"Last updated: {now.ToString(AppConstants.Date.DateFormat)}";
    }

    private static string BuildTopConnectionSecondsSummary(IEnumerable<Device> devices, DeviceTypes type)
    {
        var ranked = devices
            .Where(d => d.DeviceType == type)
            .OrderByDescending(d => d.TotalConnectionSeconds)
            .ThenBy(d => d.DeviceName)
            .Take(3)
            .ToList();

        var lines = new List<string>(3);
        for (var i = 0; i < 3; i++)
        {
            if (i >= ranked.Count)
            {
                lines.Add($"{i + 1}. -");
                continue;
            }

            var device = ranked[i];
            lines.Add($"{i + 1}. {device.DeviceName}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Stops timers and unsubscribes listeners during application shutdown.
    /// </summary>
    public void Dispose()
    {
        _appTimerService.ThirtySecondTick -= OnRefreshTick;
        _hoverPreview.PropertyChanged -= HoverPreview_PropertyChanged;

        _usbMonitorService.DeviceList.CollectionChanged -= DeviceList_CollectionChanged;
        foreach (var device in _usbMonitorService.DeviceList)
            device.PropertyChanged -= Device_PropertyChanged;
    }

    private void OnRefreshTick(object? sender, EventArgs e) => Refresh();
}
