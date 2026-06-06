using KeyPulse.Models;
using KeyPulse.ViewModels.Calendar;

namespace KeyPulse.Tests.ViewModels;

/// <summary>
/// Pure mapping from daily-stat rows + device metadata to calendar tiles and detail rows.
/// Hidden-device filtering is the query's job and is not repeated here.
/// </summary>
public class CalendarSummaryBuilderTests
{
    private static readonly DateOnly Day = new(2026, 5, 20);

    // ── BuildMonthSummaries ─────────────────────────────────────────────────────

    [Fact]
    public void BuildMonthSummaries_LeapFebruary_Returns29Entries() =>
        CalendarSummaryBuilder.BuildMonthSummaries(2024, 2, [], NoDevices()).Count.ShouldBe(29);

    [Fact]
    public void BuildMonthSummaries_EmptyMonth_AllDaysPresentWithNoData()
    {
        var summaries = CalendarSummaryBuilder.BuildMonthSummaries(2026, 5, [], NoDevices());

        summaries.Count.ShouldBe(31);
        summaries.ShouldAllBe(s => !s.HasData);
    }

    [Fact]
    public void BuildMonthSummaries_TileDevices_ResolveMetadataAndOrderKeyboardsFirst()
    {
        var rows = new[] { Stat("MOUSE", Day), Stat("KB", Day), Stat("OTHER", Day) };
        var devices = Devices(
            Device("MOUSE", "Mouse A", DeviceTypes.Mouse),
            Device("KB", "Keyboard A", DeviceTypes.Keyboard),
            Device("OTHER", "Gadget", DeviceTypes.Other)
        );

        var summaries = CalendarSummaryBuilder.BuildMonthSummaries(2026, 5, rows, devices);

        var tile = summaries.Single(s => s.Day == Day);
        tile.HasData.ShouldBeTrue();
        tile.Devices.Select(d => d.DeviceName).ShouldBe(["Keyboard A", "Mouse A", "Gadget"]);
    }

    // ── BuildDayDetails ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildDayDetails_MapsStatsAndResolvesMetadata()
    {
        var row = Stat("KB", Day);
        row.SessionCount = 2;
        row.ConnectionSeconds = 600;
        row.Keystrokes = 42;
        row.ActiveMinutes = 7;

        var detail = CalendarSummaryBuilder
            .BuildDayDetails([row], Devices(Device("KB", "My Keyboard", DeviceTypes.Keyboard)))
            .ShouldHaveSingleItem();

        detail.DeviceName.ShouldBe("My Keyboard");
        detail.DeviceType.ShouldBe(DeviceTypes.Keyboard);
        detail.SessionCount.ShouldBe(2);
        detail.ConnectionSeconds.ShouldBe(600);
        detail.Keystrokes.ShouldBe(42);
        detail.ActiveMinutes.ShouldBe(7);
        detail.HourlyInputBars.Count.ShouldBe(24);
    }

    [Fact]
    public void BuildDayDetails_NoDeviceRow_FallsBackToDeviceId()
    {
        var detail = CalendarSummaryBuilder.BuildDayDetails([Stat("ORPHAN", Day)], NoDevices()).ShouldHaveSingleItem();

        detail.DeviceName.ShouldBe("ORPHAN"); // no device metadata => id fallback
        detail.DeviceType.ShouldBe(DeviceTypes.Unknown);
    }

    [Fact]
    public void BuildDayDetails_OrdersByConnectionSecondsDescending()
    {
        var shortSession = Stat("A", Day);
        shortSession.ConnectionSeconds = 10;
        var longSession = Stat("B", Day);
        longSession.ConnectionSeconds = 999;

        var details = CalendarSummaryBuilder.BuildDayDetails([shortSession, longSession], NoDevices());

        details.Select(d => d.DeviceId).ShouldBe(["B", "A"]);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private static DailyDeviceStat Stat(string deviceId, DateOnly day) =>
        new()
        {
            Day = day,
            DeviceId = deviceId,
            UpdatedAt = DateTime.UtcNow,
        };

    private static Device Device(string deviceId, string name, DeviceTypes type) =>
        new()
        {
            DeviceId = deviceId,
            DeviceName = name,
            DeviceType = type,
        };

    private static IReadOnlyDictionary<string, Device> NoDevices() => new Dictionary<string, Device>();

    private static IReadOnlyDictionary<string, Device> Devices(params Device[] devices) =>
        devices.ToDictionary(d => d.DeviceId);
}
