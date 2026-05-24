using System.Windows.Media;
using KeyPulse.Helpers;
using KeyPulse.Models;
using OxyPlot;
using OxyPlot.Series;

namespace KeyPulse.ViewModels.Dashboard;

/// <summary>
/// Builds dashboard pie charts and related tracker behavior.
/// </summary>
internal static class DashboardPieChartBuilder
{
    private const string TRACKER_FORMAT =
        "{Label}\n"
        + "{StatusLine}"
        + "Connected Time: {ConnectedTime}\n"
        + "Percentage: {Percentage}\n"
        + "{LastSeenOrConnectedLine}"
        + "{GroupedDevices}";

    /// <summary>
    /// Creates a connection-time-share pie model for a device category (keyboard or mouse).
    /// </summary>
    public static PlotModel BuildConnectionTimePiePlot(
        string title,
        IEnumerable<Device> devices,
        IReadOnlyDictionary<string, double> connectionMinutesByDevice,
        IReadOnlyDictionary<string, OxyColor> colorsByDevice
    )
    {
        var model = new PlotModel { Title = title };

        // Build all candidate slices (one per device with non-zero connection time),
        // sorted deterministically so equal values don't visibly flip between refreshes.
        var slices = devices
            .Select(d => CreateDeviceSlice(d, connectionMinutesByDevice, colorsByDevice))
            .Where(s => s.Value > 0)
            .OrderByDescending(s => s.Value)
            .ThenBy(s => s.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (slices.Count == 0)
        {
            model.Series.Add(BuildEmptyPieSeries());
            return model;
        }

        var total = slices.Sum(s => s.Value);
        foreach (var slice in slices)
            slice.Percentage = total > 0 ? $"{slice.Value / total:P1}" : "N/A";

        // Split visible vs. "Others"-grouped slices using the palette thresholds.
        var visibleSlices = new List<DashboardPieSlice>();
        var otherSlices = new List<DashboardPieSlice>();
        for (var i = 0; i < slices.Count; i++)
        {
            var slice = slices[i];
            var share = total > 0 ? slice.Value / total : 0;
            var isVisible =
                i < DashboardDeviceColorPalette.MaxColoredDevicesPerType
                && share > DashboardDeviceColorPalette.OthersShareThreshold;

            (isVisible ? visibleSlices : otherSlices).Add(slice);
        }

        if (otherSlices.Count > 0)
            visibleSlices.Add(CreateOthersSlice(otherSlices, total));

        var series = new PieSeries
        {
            Diameter = 0.95,
            StrokeThickness = 1,
            AngleSpan = 360,
            StartAngle = 0,
            TrackerFormatString = TRACKER_FORMAT,
        };
        foreach (var slice in visibleSlices)
            series.Slices.Add(slice);

        model.Series.Add(series);
        return model;
    }

    private static DashboardPieSlice CreateDeviceSlice(
        Device device,
        IReadOnlyDictionary<string, double> connectionMinutesByDevice,
        IReadOnlyDictionary<string, OxyColor> colorsByDevice
    )
    {
        connectionMinutesByDevice.TryGetValue(device.DeviceId, out var connectionMinutes);
        var color = colorsByDevice.TryGetValue(device.DeviceId, out var c) ? c : OxyColors.Automatic;

        return new DashboardPieSlice(device.DeviceName, connectionMinutes)
        {
            DeviceId = device.DeviceId,
            Fill = color,
            ConnectedTime = TimeFormatter.FormatDuration(TimeSpan.FromMinutes(connectionMinutes)),
            IsConnected = device.IsConnected,
            LastSeenOrConnectedLabel = device.IsConnected ? "Last connected" : "Last seen",
            LastSeenOrConnectedValue = device.IsConnected ? device.LastConnectedRelative : device.LastSeenRelative,
        };
    }

    private static DashboardPieSlice CreateOthersSlice(IReadOnlyList<DashboardPieSlice> otherSlices, double total)
    {
        var otherValue = otherSlices.Sum(s => s.Value);
        var groupedDevices = "Grouped Devices:\n" + string.Join("\n", otherSlices.Select(s => $"- {s.Label}"));

        return new DashboardPieSlice($"Others ({otherSlices.Count})", otherValue)
        {
            DeviceId = "",
            Fill = DashboardDeviceColorPalette.OthersColor,
            ConnectedTime = TimeFormatter.FormatDuration(TimeSpan.FromMinutes(otherValue)),
            Percentage = total > 0 ? $"{otherValue / total:P1}" : "N/A",
            GroupedDevices = groupedDevices,
            IsOthers = true,
        };
    }

    private static PieSeries BuildEmptyPieSeries() =>
        new()
        {
            Diameter = 0.95,
            StrokeThickness = 1,
            AngleSpan = 360,
            StartAngle = 0,
            TrackerFormatString = "No connected time data yet.",
            Slices = { new PieSlice("No data", 1) },
        };

    /// <summary>
    /// Creates an interaction controller that shows trackers on hover.
    /// </summary>
    public static IPlotController BuildPieHoverController()
    {
        var controller = new PlotController();
        controller.UnbindAll();
        controller.Bind(new OxyMouseEnterGesture(), PlotCommands.HoverTrack);
        controller.Bind(new OxyMouseDownGesture(OxyMouseButton.Left), PlotCommands.HoverTrack);
        return controller;
    }

    /// <summary>
    /// Connects tracker hit events to the dashboard hover preview state.
    /// </summary>
    public static void AttachTrackerPreview(PlotModel model, Action<DashboardPieSlice> onSliceHovered)
    {
#pragma warning disable CS0618
        model.TrackerChanged += (_, e) =>
#pragma warning restore CS0618
        {
            if (e.HitResult?.Item is DashboardPieSlice slice)
                onSliceHovered(slice);
        };
    }
}

/// <summary>
/// Tracker payload attached to each pie slice for rich hover text and the bound hover preview.
/// </summary>
internal sealed class DashboardPieSlice(string label, double value) : PieSlice(label, value)
{
    public string DeviceId { get; init; } = "";
    public string ConnectedTime { get; init; } = "N/A";
    public string Percentage { get; set; } = "N/A";
    public bool IsConnected { get; init; }
    public string LastSeenOrConnectedLabel { get; init; } = "";
    public string LastSeenOrConnectedValue { get; init; } = "";
    public string GroupedDevices { get; init; } = "";
    public bool IsOthers { get; init; }

    public string StatusTag =>
        IsOthers ? ""
        : IsConnected ? "Connected"
        : "Disconnected";

    // Tracker template placeholders. Kept as derived strings so the OxyPlot tracker popup
    // can hide the whole line for "Others" slices and disconnected metadata.
    public string StatusLine => IsOthers ? "" : $"Status: {StatusTag}\n";
    public string LastSeenOrConnectedLine =>
        IsOthers || string.IsNullOrEmpty(LastSeenOrConnectedLabel)
            ? ""
            : $"{LastSeenOrConnectedLabel}: {LastSeenOrConnectedValue}\n";
}

/// <summary>
/// UI-bound hover state displayed beneath the dashboard pie charts.
/// </summary>
internal sealed class DashboardHoverPreview : ObservableObject
{
    private string _deviceName = "Hover a slice to inspect device metadata.";
    private string _statusTag = "Unknown";
    private Brush _statusBrush = Brushes.Gray;
    private string _connectedTime = "Connected Time: N/A";
    private string _percentage = "Percentage: N/A";
    private string _lastSeenOrConnected = "Last seen: N/A";
    private string _groupedDevices = "";

    public string DeviceName
    {
        get => _deviceName;
        private set
        {
            _deviceName = value;
            OnPropertyChanged();
        }
    }

    public string StatusTag
    {
        get => _statusTag;
        private set
        {
            _statusTag = value;
            OnPropertyChanged();
        }
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set
        {
            _statusBrush = value;
            OnPropertyChanged();
        }
    }

    public string ConnectedTime
    {
        get => _connectedTime;
        private set
        {
            _connectedTime = value;
            OnPropertyChanged();
        }
    }

    public string Percentage
    {
        get => _percentage;
        private set
        {
            _percentage = value;
            OnPropertyChanged();
        }
    }

    public string LastSeenOrConnected
    {
        get => _lastSeenOrConnected;
        private set
        {
            _lastSeenOrConnected = value;
            OnPropertyChanged();
        }
    }

    public string GroupedDevices
    {
        get => _groupedDevices;
        private set
        {
            _groupedDevices = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Copies values from the hovered pie slice into the preview state.
    /// </summary>
    public void UpdateFromSlice(DashboardPieSlice slice)
    {
        DeviceName = slice.Label;
        Percentage = $"Percentage: {slice.Percentage}";
        GroupedDevices = slice.GroupedDevices;

        if (slice.IsOthers)
        {
            StatusTag = "";
            StatusBrush = Brushes.Gray;
            ConnectedTime = $"Connected Time: {slice.ConnectedTime}";
            LastSeenOrConnected = "";
            return;
        }

        StatusTag = slice.StatusTag;
        StatusBrush = slice.IsConnected ? Brushes.ForestGreen : Brushes.IndianRed;
        ConnectedTime = $"Connected Time: {slice.ConnectedTime}";
        LastSeenOrConnected = $"{slice.LastSeenOrConnectedLabel}: {slice.LastSeenOrConnectedValue}";
    }
}
