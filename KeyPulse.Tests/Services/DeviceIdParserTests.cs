using KeyPulse.Services;

namespace KeyPulse.Tests.Services;

/// <summary>
/// Pure string-parsing logic extracted from the WMI / Raw Input services. The surrounding capture code
/// is Windows-bound, but these parsers are deterministic and central to device identification.
/// </summary>
public class DeviceIdParserTests
{
    // RawInputService.ParseDeviceId: HID device path -> canonical USB id (uppercased), or null.

    [Theory]
    [InlineData(@"\\?\HID#VID_046D&PID_C548&MI_00#7&abc#{guid}", "USB\\VID_046D&PID_C548")]
    [InlineData(@"\\?\HID#vid_046d&pid_c548", "USB\\VID_046D&PID_C548")] // lowercased input uppercased
    [InlineData("VID_1234&PID_5678", "USB\\VID_1234&PID_5678")]
    public void ParseDeviceId_ValidPath_ReturnsCanonicalUppercasedId(string path, string expected) =>
        RawInputService.ParseDeviceId(path).ShouldBe(expected);

    [Theory]
    [InlineData("no identifiers here")]
    [InlineData("VID_046D only, no pid")] // missing PID
    [InlineData("PID_C548 only, no vid")] // missing VID
    [InlineData("VID_&PID_C548")] // empty VID value
    public void ParseDeviceId_MissingOrEmptyVidPid_ReturnsNull(string path) =>
        RawInputService.ParseDeviceId(path).ShouldBeNull();

    // UsbMonitorService.ExtractValueFromDeviceId: pulls the token after an identifier up to & or \.

    [Theory]
    [InlineData(@"USB\VID_046D&PID_C548&MI_00", "VID_", "046D")]
    [InlineData(@"USB\VID_046D&PID_C548&MI_00", "PID_", "C548")]
    [InlineData(@"USB\VID_046D", "VID_", "046D")] // terminates at end of string
    [InlineData("vid_046d&x", "VID_", "046d")] // case-insensitive identifier match, value verbatim
    public void ExtractValueFromDeviceId_ReturnsTokenAfterIdentifier(string id, string identifier, string expected) =>
        UsbMonitorService.ExtractValueFromDeviceId(id, identifier).ShouldBe(expected);

    [Theory]
    [InlineData(null, "VID_")]
    [InlineData("", "VID_")]
    [InlineData("USB\\PID_C548", "VID_")] // identifier absent
    [InlineData("USB\\VID_046D", "")] // empty identifier
    public void ExtractValueFromDeviceId_NoMatch_ReturnsEmpty(string? id, string identifier) =>
        UsbMonitorService.ExtractValueFromDeviceId(id, identifier).ShouldBe("");

    // UsbMonitorService.IsUnknownDeviceName

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("Unknown Device", true)]
    [InlineData("unknown device", true)] // case-insensitive
    [InlineData("Logitech MX Master 3", false)]
    public void IsUnknownDeviceName(string? name, bool expected) =>
        UsbMonitorService.IsUnknownDeviceName(name).ShouldBe(expected);
}
