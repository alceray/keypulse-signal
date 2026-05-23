using KeyPulse.Models;
using OxyPlot;

namespace KeyPulse.ViewModels.Dashboard;

/// <summary>
/// Assigns per-device chart colors by connection-time rank.
/// Palettes follow proper color wheel order (0° to 360°) with muted, professional tones.
/// </summary>
internal sealed class DashboardDeviceColorPalette
{
    public const int MaxColoredDevicesPerType = 12;
    public const double OthersShareThreshold = 0.01;

    public static readonly OxyColor OthersColor = OxyColor.FromRgb(150, 156, 166);

    /// <summary>
    /// Cool palette for keyboards - follows color wheel order from cyan (180°) to magenta (300°)
    /// Muted/slightly desaturated for professional appearance
    /// </summary>
    private static readonly OxyColor[] CoolPalette =
    [
        // Keyboard — cool: teal → blue → indigo → violet (dark/light pairs)
        OxyColor.FromRgb(61, 158, 150), // teal dark
        OxyColor.FromRgb(168, 221, 217), // teal light
        OxyColor.FromRgb(46, 111, 191), // blue dark
        OxyColor.FromRgb(139, 188, 240), // blue light
        OxyColor.FromRgb(74, 63, 168), // indigo dark
        OxyColor.FromRgb(157, 151, 224), // indigo light
        OxyColor.FromRgb(122, 45, 158), // violet dark
        OxyColor.FromRgb(199, 142, 224), // violet light
        OxyColor.FromRgb(29, 122, 110), // deep teal dark
        OxyColor.FromRgb(106, 191, 184), // deep teal light
        OxyColor.FromRgb(26, 77, 153), // deep blue dark
        OxyColor.FromRgb(102, 153, 217), // deep blue light
    ];

    /// <summary>
    /// Warm palette for mice - follows color wheel order from red (0°) to yellow (60°)
    /// Muted/slightly desaturated for professional appearance
    /// </summary>
    private static readonly OxyColor[] WarmPalette =
    [
        // Mouse — warm: crimson → orange → gold → olive (dark/light pairs)
        OxyColor.FromRgb(192, 52, 42), // crimson dark
        OxyColor.FromRgb(240, 148, 142), // crimson light
        OxyColor.FromRgb(196, 94, 16), // orange dark
        OxyColor.FromRgb(240, 169, 106), // orange light
        OxyColor.FromRgb(179, 138, 0), // gold dark
        OxyColor.FromRgb(240, 208, 96), // gold light
        OxyColor.FromRgb(107, 122, 0), // olive dark
        OxyColor.FromRgb(184, 204, 85), // olive light
        OxyColor.FromRgb(160, 34, 14), // deep crimson dark
        OxyColor.FromRgb(232, 128, 110), // deep crimson light
        OxyColor.FromRgb(158, 82, 0), // amber dark
        OxyColor.FromRgb(224, 160, 80), // amber light
    ];

    public IReadOnlyDictionary<string, OxyColor> GetColorsForDevices(
        IReadOnlyCollection<Device> devices,
        IReadOnlyDictionary<string, double> connectionMinutesByDevice
    )
    {
        var colorsByDevice = new Dictionary<string, OxyColor>(StringComparer.OrdinalIgnoreCase);
        var deviceList = devices.ToList();

        AssignColorsForType(deviceList, connectionMinutesByDevice, DeviceTypes.Keyboard, colorsByDevice);
        AssignColorsForType(deviceList, connectionMinutesByDevice, DeviceTypes.Mouse, colorsByDevice);

        return colorsByDevice;
    }

    private static void AssignColorsForType(
        IReadOnlyCollection<Device> devices,
        IReadOnlyDictionary<string, double> connectionMinutesByDevice,
        DeviceTypes deviceType,
        IDictionary<string, OxyColor> colorsByDevice
    )
    {
        var ranked = devices
            .Where(d => d.DeviceType == deviceType)
            .Select(d =>
            {
                connectionMinutesByDevice.TryGetValue(d.DeviceId, out var connectionMinutes);
                return new { Device = d, ConnectionMinutes = connectionMinutes };
            })
            .OrderByDescending(d => d.ConnectionMinutes)
            .ThenBy(d => d.Device.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Device.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = ranked.Sum(d => d.ConnectionMinutes);
        var colorIndex = 0;

        for (var i = 0; i < ranked.Count; i++)
        {
            var item = ranked[i];
            var share = total > 0 ? item.ConnectionMinutes / total : 0;
            var isColored = item.ConnectionMinutes > 0 && i < MaxColoredDevicesPerType && share > OthersShareThreshold;

            colorsByDevice[item.Device.DeviceId] = isColored
                ? GetColorFromPalette(deviceType, colorIndex++)
                : OthersColor;
        }
    }

    private static OxyColor GetColorFromPalette(DeviceTypes deviceType, int index)
    {
        return deviceType switch
        {
            DeviceTypes.Mouse => WarmPalette[index % WarmPalette.Length],
            DeviceTypes.Keyboard => CoolPalette[index % CoolPalette.Length],
            _ => OthersColor,
        };
    }
}
