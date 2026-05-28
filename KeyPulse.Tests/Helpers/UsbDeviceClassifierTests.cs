using KeyPulse.Helpers;
using KeyPulse.Models;

namespace KeyPulse.Tests.Helpers;

/// <summary>
/// Covers the pure signal-pattern resolver. (GetInterfaceSignal is WMI-bound and not unit-tested.)
/// </summary>
public class UsbDeviceClassifierTests
{
    [Theory]
    [InlineData(1, 1, DeviceTypes.Mouse)] // documented: 1 keyboard + 1 mouse signal => Mouse
    [InlineData(2, 1, DeviceTypes.Keyboard)] // documented: 2 keyboard + 1 mouse signal => Keyboard
    [InlineData(0, 0, DeviceTypes.Other)]
    [InlineData(1, 0, DeviceTypes.Other)]
    [InlineData(2, 2, DeviceTypes.Other)]
    [InlineData(3, 1, DeviceTypes.Other)]
    [InlineData(-1, -1, DeviceTypes.Other)]
    [InlineData(int.MaxValue, int.MaxValue, DeviceTypes.Other)]
    public void ResolveDeviceType_MapsSignalPatterns(int keyboardSignals, int mouseSignals, DeviceTypes expected) =>
        UsbDeviceClassifier.ResolveDeviceType(keyboardSignals, mouseSignals).ShouldBe(expected);
}
