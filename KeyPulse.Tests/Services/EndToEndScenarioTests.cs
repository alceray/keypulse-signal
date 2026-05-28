using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.Tests.Infrastructure;

namespace KeyPulse.Tests.Services;

/// <summary>
/// Drives DataService and DailyStatsService together through a full device lifecycle and asserts the
/// downstream dashboard/calendar outputs — the closest thing to an integration test for the data layer.
/// </summary>
public class EndToEndScenarioTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly AppTimerService _timer = new();
    private readonly DailyStatsService _dailyStats;
    private readonly DataService _data;

    public EndToEndScenarioTests()
    {
        _dailyStats = new DailyStatsService(_db.Factory, _timer);
        _data = new DataService(_db.Factory, _dailyStats);
    }

    public void Dispose()
    {
        _dailyStats.Dispose();
        _timer.Dispose();
        _db.Dispose();
    }

    private static DateTime At(int hour, int minute) => new(2026, 5, 20, hour, minute, 0, DateTimeKind.Local);

    [Fact]
    public void FullLifecycle_ConnectActivityDisconnect_FlowsThroughToStatsAndCalendar()
    {
        var day = new DateOnly(2026, 5, 20);

        // App starts, a device connects, registers input, disconnects, then the app ends.
        _data.SaveDevice(
            new Device
            {
                DeviceId = "D1",
                DeviceName = "kb",
                DeviceType = DeviceTypes.Keyboard,
            }
        );
        _data.SaveDeviceEvent(new DeviceEvent { EventType = EventTypes.AppStarted, EventTime = At(8, 59) });
        _data.SaveDeviceEvent(
            new DeviceEvent
            {
                DeviceId = "D1",
                EventType = EventTypes.Connected,
                EventTime = At(9, 0),
            }
        );
        _data.SaveActivitySnapshots(
            [
                new ActivitySnapshot
                {
                    DeviceId = "D1",
                    Minute = At(9, 5),
                    Keystrokes = 42,
                },
            ]
        );
        _data.SaveDeviceEvent(
            new DeviceEvent
            {
                DeviceId = "D1",
                EventType = EventTypes.Disconnected,
                EventTime = At(10, 0),
            }
        );
        _data.SaveDeviceEvent(new DeviceEvent { EventType = EventTypes.AppEnded, EventTime = At(10, 1) });

        // Connection stats are write-through; activity is flushed by the minute projector.
        _dailyStats.ProjectClosedActivityMinutes();

        // Daily stats: connection from the closing event, keystrokes from the projected snapshot.
        var stat = _dailyStats.GetDailyDeviceStats(day, day).ShouldHaveSingleItem();
        stat.SessionCount.ShouldBe(1);
        stat.ConnectionSeconds.ShouldBe(3600); // 09:00 → 10:00
        stat.Keystrokes.ShouldBe(42);
        stat.ActiveMinutes.ShouldBe(1);

        // Calendar detail reflects the same, resolving device metadata.
        var detail = _dailyStats.GetCalendarDayDetail(day).ShouldHaveSingleItem();
        detail.DeviceName.ShouldBe("kb");
        detail.ConnectionSeconds.ShouldBe(3600);
        detail.Keystrokes.ShouldBe(42);

        // Device snapshot accumulated the input; the completed session is recoverable.
        _data.GetDevice("D1")!.TotalInputCount.ShouldBe(42);
        _data.GetEventsFromLastCompletedSession().Count.ShouldBe(2); // Connected + Disconnected
    }
}
