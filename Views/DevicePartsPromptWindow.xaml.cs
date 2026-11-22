using System.Windows;
using System.Windows.Interop;
using KeyPulse.Models;

namespace KeyPulse.Views;

public partial class DevicePartsPromptWindow : Window
{
    public event Action? ChangeRequested;
    private readonly Device _device;

    public DevicePartsPromptWindow(Device device, DeviceBuildSnapshot build)
    {
        InitializeComponent();
        _device = device;
        UpdateBuild(build);
        SourceInitialized += (_, _) =>
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(PreventActivation);
        ContentRendered += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 16;
            Top = area.Bottom - ActualHeight - 16;
        };
    }

    private static string Labels(DeviceBuildSnapshot build, string kind)
    {
        var parts = build.Parts.Where(x => x.PartKind == kind).Select(x => x.Label).ToArray();
        return parts.Length == 0 ? "None" : string.Join(", ", parts);
    }

    public void UpdateBuild(DeviceBuildSnapshot build) =>
        DataContext = new
        {
            Title = $"{_device.DeviceName} reconnected",
            Switches = "Switches: " + Labels(build, PartKinds.Switch),
            Keycaps = "Keycaps: " + Labels(build, PartKinds.Keycap),
        };

    private static IntPtr PreventActivation(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // WM_MOUSEACTIVATE / MA_NOACTIVATE: mouse clicks work without moving keyboard focus.
        if (message == 0x0021)
        {
            handled = true;
            return new IntPtr(3);
        }
        return IntPtr.Zero;
    }

    private void SameClick(object sender, RoutedEventArgs e) => Close();

    private void ChangeClick(object sender, RoutedEventArgs e)
    {
        ChangeRequested?.Invoke();
        Close();
    }
}
