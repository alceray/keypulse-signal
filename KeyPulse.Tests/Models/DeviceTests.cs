using System.ComponentModel;
using KeyPulse.Models;

namespace KeyPulse.Tests.Models;

/// <summary>
/// Device derives from ObservableObject; in a headless test process notifications fire
/// synchronously, so the status logic, session math, and change-notification contract are all
/// directly testable. DateTime.Now-dependent paths use deterministic inputs (no-session / future).
/// </summary>
public class DeviceTests
{
    private static Device NewDevice(string id = "DEV1") => new() { DeviceId = id };

    // ── IsConnected / StatusText / StatusSortOrder ─────────────────────────────

    [Fact]
    public void IsConnected_ReflectsSession()
    {
        var device = NewDevice();
        device.IsConnected.ShouldBeFalse();

        device.SessionStartedAt = DateTime.Now;
        device.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public void StatusText_Disconnected_IgnoresHiddenFlag()
    {
        var device = NewDevice();
        device.IsHiddenFromDisplay = true; // no session

        device.StatusText.ShouldBe("Disconnected");
        device.StatusSortOrder.ShouldBe(2);
    }

    [Fact]
    public void StatusText_ConnectedAndHidden()
    {
        var device = NewDevice();
        device.SessionStartedAt = DateTime.Now;
        device.IsHiddenFromDisplay = true;

        device.StatusText.ShouldBe("Hidden");
        device.StatusSortOrder.ShouldBe(1);
    }

    [Fact]
    public void StatusText_ConnectedVisible()
    {
        var device = NewDevice();
        device.SessionStartedAt = DateTime.Now;

        device.StatusText.ShouldBe("Connected");
        device.StatusSortOrder.ShouldBe(0);
    }

    // ── TotalConnectionSeconds ─────────────────────────────────────────────────

    [Fact]
    public void TotalConnectionSeconds_NoSession_ReturnsStored()
    {
        var device = NewDevice();
        device.TotalConnectionSeconds = 1234;
        device.TotalConnectionSeconds.ShouldBe(1234);
    }

    [Fact]
    public void TotalConnectionSeconds_FutureSession_ClampsNegativeElapsed()
    {
        var device = NewDevice();
        device.TotalConnectionSeconds = 100;
        device.SessionStartedAt = DateTime.Now.AddHours(1); // future => negative elapsed

        device.TotalConnectionSeconds.ShouldBe(100); // Math.Max(0, negative) adds nothing
    }

    // ── SetActivityState ───────────────────────────────────────────────────────

    [Fact]
    public void SetActivityState_NotifiesOnChangeOnly()
    {
        var device = NewDevice();
        var changes = CountNotifications(
            device,
            nameof(Device.IsActive),
            () =>
            {
                device.SetActivityState(true);
                device.SetActivityState(true); // no change
                device.SetActivityState(false);
            }
        );

        changes.ShouldBe(2);
        device.IsActive.ShouldBeFalse();
    }

    // ── Change-notification cascades ───────────────────────────────────────────

    [Fact]
    public void SessionStartedAt_RaisesDependentProperties()
    {
        var device = NewDevice();
        var names = CaptureNotifications(device, () => device.SessionStartedAt = DateTime.Now);

        names.ShouldContain(nameof(Device.SessionStartedAt));
        names.ShouldContain(nameof(Device.IsConnected));
        names.ShouldContain(nameof(Device.StatusText));
        names.ShouldContain(nameof(Device.TotalConnectionSeconds));
    }

    [Fact]
    public void RefreshDynamicProperties_RaisesAllThree()
    {
        var device = NewDevice();
        var names = CaptureNotifications(device, device.RefreshDynamicProperties);

        names.ShouldContain(nameof(Device.TotalConnectionSeconds));
        names.ShouldContain(nameof(Device.LastConnectedRelative));
        names.ShouldContain(nameof(Device.LastSeenRelative));
    }

    [Fact]
    public void IsHiddenFromDisplay_RaisesStatusText()
    {
        var device = NewDevice();
        var names = CaptureNotifications(device, () => device.IsHiddenFromDisplay = true);

        names.ShouldContain(nameof(Device.IsHiddenFromDisplay));
        names.ShouldContain(nameof(Device.StatusText));
    }

    [Fact]
    public void IsHiddenFromDisplay_RaisesStatusSortOrder()
    {
        var device = NewDevice();
        device.SessionStartedAt = DateTime.Now;
        var names = CaptureNotifications(device, () => device.IsHiddenFromDisplay = true);

        names.ShouldContain(nameof(Device.StatusSortOrder));
    }

    [Fact]
    public void DeviceType_RaisesTypeAndSortNotifications()
    {
        var device = NewDevice();
        var names = CaptureNotifications(device, () => device.DeviceType = DeviceTypes.Mouse);

        names.ShouldContain(nameof(Device.DeviceType));
        names.ShouldContain(nameof(Device.TypeSortOrder));
        device.TypeSortOrder.ShouldBe(1);
    }

    // ── UpdateLastConnectedAt ──────────────────────────────────────────────────

    [Fact]
    public void UpdateLastConnectedAt_FirstConnection_AlwaysSets()
    {
        var device = NewDevice();
        var time = new DateTime(2026, 5, 20, 9, 0, 0);

        device.UpdateLastConnectedAt(time, EventTypes.ConnectionStarted, false);

        device.LastConnectedAt.ShouldBe(time);
    }

    [Fact]
    public void UpdateLastConnectedAt_ConnectedEvent_AlwaysSetsEvenWhenAlreadyKnown()
    {
        var device = NewDevice();
        device.LastConnectedAt = new DateTime(2020, 1, 1);
        var time = new DateTime(2026, 5, 20, 9, 0, 0);

        device.UpdateLastConnectedAt(time, EventTypes.Connected, true);

        device.LastConnectedAt.ShouldBe(time);
    }

    [Fact]
    public void UpdateLastConnectedAt_NonConnectionStartedEvent_NoOp()
    {
        var device = NewDevice();
        var original = new DateTime(2020, 1, 1);
        device.LastConnectedAt = original;

        device.UpdateLastConnectedAt(new DateTime(2026, 5, 20), EventTypes.Disconnected, false);

        device.LastConnectedAt.ShouldBe(original);
    }

    [Fact]
    public void UpdateLastConnectedAt_ConnectionStarted_NotEndedLastSession_Sets()
    {
        var device = NewDevice();
        device.LastConnectedAt = new DateTime(2020, 1, 1);
        var time = new DateTime(2026, 5, 20);

        device.UpdateLastConnectedAt(time, EventTypes.ConnectionStarted, false);

        device.LastConnectedAt.ShouldBe(time);
    }

    [Fact]
    public void UpdateLastConnectedAt_ConnectionStarted_EndedLastSession_NoOp()
    {
        var device = NewDevice();
        var original = new DateTime(2020, 1, 1);
        device.LastConnectedAt = original;

        device.UpdateLastConnectedAt(new DateTime(2026, 5, 20), EventTypes.ConnectionStarted, true);

        device.LastConnectedAt.ShouldBe(original);
    }

    // ── CommitSession ──────────────────────────────────────────────────────────

    [Fact]
    public void CommitSession_NoSession_NoOp()
    {
        var device = NewDevice();
        device.TotalConnectionSeconds = 50;

        device.CommitSession(DateTime.Now);

        device.TotalConnectionSeconds.ShouldBe(50);
    }

    [Fact]
    public void CommitSession_PositiveElapsed_AccumulatesAndClears()
    {
        var device = NewDevice();
        var start = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Local);
        device.SessionStartedAt = start;

        device.CommitSession(start.AddSeconds(120));

        device.IsConnected.ShouldBeFalse();
        device.TotalConnectionSeconds.ShouldBe(120);
    }

    [Fact]
    public void CommitSession_NegativeElapsed_ClearsWithoutAccumulating()
    {
        var device = NewDevice();
        var start = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Local);
        device.TotalConnectionSeconds = 10;
        device.SessionStartedAt = start;

        device.CommitSession(start.AddSeconds(-30)); // end before start

        device.IsConnected.ShouldBeFalse();
        device.TotalConnectionSeconds.ShouldBe(10);
    }

    // ── Relative-time fallbacks ────────────────────────────────────────────────

    [Fact]
    public void RelativeTimes_NullTimestamps_ReturnNA()
    {
        var device = NewDevice();
        device.LastConnectedRelative.ShouldBe("N/A");
        device.LastSeenRelative.ShouldBe("N/A");
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static List<string?> CaptureNotifications(INotifyPropertyChanged obj, Action act)
    {
        var names = new List<string?>();
        PropertyChangedEventHandler handler = (_, e) => names.Add(e.PropertyName);
        obj.PropertyChanged += handler;
        act();
        obj.PropertyChanged -= handler;
        return names;
    }

    private static int CountNotifications(INotifyPropertyChanged obj, string property, Action act) =>
        CaptureNotifications(obj, act).Count(name => name == property);
}
