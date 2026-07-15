using System.Collections.Concurrent;
using KeyPulse.Data;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.Tests.Infrastructure;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace KeyPulse.Tests.Services;

/// <summary>
/// DataService runs Database.Migrate() + DatabaseMigrations.RunAll() in its constructor. Against the
/// EnsureCreated fixture the migrate is a no-op (migrations are attributed to the base context) and
/// RunAll operates on the already-created tables. Crash recovery reads a heartbeat file that is absent
/// in tests (HeartbeatFile.Read() -> null), so crash time falls back to the orphaned session start.
/// </summary>
public class DataServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly AppTimerService _timer = new();
    private readonly DailyStatsService _dailyStats;
    private readonly DataService _sut;

    public DataServiceTests()
    {
        _dailyStats = new DailyStatsService(_db.Factory, _timer);
        _sut = new DataService(_db.Factory, _dailyStats);
    }

    public void Dispose()
    {
        _dailyStats.Dispose();
        _timer.Dispose();
        _db.Dispose();
    }

    private static DateTime At(int hour, int minute, int second = 0) =>
        new(2026, 5, 20, hour, minute, second, DateTimeKind.Local);

    private void Seed(Action<ApplicationDbContext> seed)
    {
        using var ctx = _db.CreateContext();
        seed(ctx);
        ctx.SaveChanges();
    }

    /// <summary>
    /// Redirects the static Serilog logger to an in-memory sink for the duration of a test so the
    /// otherwise-swallowed-and-logged save failure becomes observable. Only the device-snapshot save
    /// failure template is captured, so logging from test classes running in parallel cannot produce
    /// false positives.
    /// </summary>
    private sealed class ErrorLogCapture : IDisposable
    {
        private const string DeviceSaveFailureTemplate = "Failed to save device snapshot for {DeviceId}";

        private readonly ILogger _previous;
        public ConcurrentQueue<string> Messages { get; } = new();

        private ErrorLogCapture()
        {
            _previous = Log.Logger;
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Error()
                .WriteTo.Sink(new QueueSink(this))
                .CreateLogger();
        }

        public static ErrorLogCapture Begin() => new();

        public void Dispose()
        {
            (Log.Logger as IDisposable)?.Dispose();
            Log.Logger = _previous;
        }

        private sealed class QueueSink(ErrorLogCapture owner) : ILogEventSink
        {
            public void Emit(LogEvent logEvent)
            {
                if (logEvent.MessageTemplate.Text == DeviceSaveFailureTemplate)
                    owner.Messages.Enqueue(logEvent.RenderMessage());
            }
        }
    }

    // ── SaveDevice ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveDevice_ConcurrentFirstConnectOfSameDevice_NoUniqueViolation()
    {
        // Reproduces the production race: several threads save the same brand-new device at once. Each
        // opens its own connection, so without the lock both read "not present" and then race to INSERT
        // the same primary key, failing the loser with "UNIQUE constraint failed: Devices.DeviceId"
        // (swallowed and logged). The lock serializes them so every save commits and one row results.
        using var errors = ErrorLogCapture.Begin();

        const int workers = 16;
        using var barrier = new Barrier(workers);
        var tasks = Enumerable
            .Range(0, workers)
            .Select(i =>
                Task.Run(() =>
                {
                    barrier.SignalAndWait(); // release all writers simultaneously to widen the race window
                    _sut.SaveDevice(
                        new Device
                        {
                            DeviceId = "RACE",
                            DeviceName = $"name-{i}",
                            TotalInputCount = i,
                        }
                    );
                })
            )
            .ToArray();
        await Task.WhenAll(tasks);

        errors.Messages.ShouldBeEmpty(); // every concurrent save committed; none collided on the key
        _sut.GetAllDevices().Count(d => d.DeviceId == "RACE").ShouldBe(1);
    }

    // ── SaveDeviceEvent ─────────────────────────────────────────────────────────

    [Fact]
    public void SaveDeviceEvent_Inserts()
    {
        _sut.SaveDeviceEvent(
            new DeviceEvent
            {
                DeviceId = "D1",
                EventType = EventTypes.Connected,
                EventTime = At(9, 0),
            }
        );

        _sut.GetAllDeviceEvents().Count.ShouldBe(1);
    }

    [Fact]
    public void SaveDeviceEvent_Duplicate_SwallowedByParameterlessOverload()
    {
        DeviceEvent Make() =>
            new()
            {
                DeviceId = "D1",
                EventType = EventTypes.Connected,
                EventTime = At(9, 0),
            };

        _sut.SaveDeviceEvent(Make());
        Should.NotThrow(() => _sut.SaveDeviceEvent(Make())); // unique-index violation caught & logged

        _sut.GetAllDeviceEvents().Count.ShouldBe(1);
    }

    // ── SaveActivitySnapshots ───────────────────────────────────────────────────

    [Fact]
    public void SaveActivitySnapshots_UnknownDevice_Skipped()
    {
        _sut.SaveActivitySnapshots(
            [
                new ActivitySnapshot
                {
                    DeviceId = "GHOST",
                    Minute = At(9, 5),
                    Keystrokes = 5,
                },
            ]
        );

        _sut.GetActivitySnapshots().ShouldBeEmpty();
    }

    [Fact]
    public void SaveActivitySnapshots_New_InsertsAndCountsTotalInput()
    {
        Seed(ctx => ctx.Devices.Add(new Device { DeviceId = "D1", DeviceName = "kb" }));

        _sut.SaveActivitySnapshots(
            [
                new ActivitySnapshot
                {
                    DeviceId = "D1",
                    Minute = At(9, 5),
                    Keystrokes = 10,
                    MouseClicks = 2,
                    MouseMovementSeconds = 3,
                    ActiveSeconds = 7,
                },
            ]
        );

        var snap = _sut.GetActivitySnapshots().ShouldHaveSingleItem();
        snap.Keystrokes.ShouldBe(10);
        snap.ActiveSeconds.ShouldBe((byte)7);
        _sut.GetDevice("D1")!.TotalInputCount.ShouldBe(15); // 10 + 2 + 3, active seconds excluded
    }

    [Fact]
    public void SaveActivitySnapshots_MergesExisting_AddsKeysMaxesMovement()
    {
        Seed(ctx => ctx.Devices.Add(new Device { DeviceId = "D1", DeviceName = "kb" }));

        _sut.SaveActivitySnapshots(
            [
                new ActivitySnapshot
                {
                    DeviceId = "D1",
                    Minute = At(9, 5),
                    Keystrokes = 10,
                    MouseMovementSeconds = 5,
                    ActiveSeconds = 6,
                },
            ]
        );
        _sut.SaveActivitySnapshots(
            [
                new ActivitySnapshot
                {
                    DeviceId = "D1",
                    Minute = At(9, 5),
                    Keystrokes = 4,
                    MouseMovementSeconds = 3,
                    ActiveSeconds = 9,
                },
            ]
        );

        var snap = _sut.GetActivitySnapshots().ShouldHaveSingleItem();
        snap.Keystrokes.ShouldBe(14); // additive
        snap.MouseMovementSeconds.ShouldBe((byte)5); // Max(5, 3)
        snap.ActiveSeconds.ShouldBe((byte)9); // Max(6, 9)
    }

    [Fact]
    public void SaveActivitySnapshots_InBatchDuplicate_MergesInsteadOfLosingBatch()
    {
        Seed(ctx => ctx.Devices.Add(new Device { DeviceId = "D1", DeviceName = "kb" }));

        _sut.SaveActivitySnapshots(
            [
                new ActivitySnapshot
                {
                    DeviceId = "D1",
                    Minute = At(9, 5),
                    Keystrokes = 1,
                },
                new ActivitySnapshot
                {
                    DeviceId = "D1",
                    Minute = At(9, 5),
                    Keystrokes = 2,
                },
            ]
        );

        // The two same-minute snapshots merge into one row rather than colliding and dropping the batch.
        _sut.GetActivitySnapshots().ShouldHaveSingleItem().Keystrokes.ShouldBe(3);
        _sut.GetDevice("D1")!.TotalInputCount.ShouldBe(3);
    }

    // ── RecoverFromCrash ────────────────────────────────────────────────────────

    [Fact]
    public void RecoverFromCrash_UnbalancedDevice_WritesCloseAndAppEnded()
    {
        Seed(ctx =>
        {
            ctx.Devices.Add(new Device { DeviceId = "D1", DeviceName = "kb" });
            ctx.DeviceEvents.Add(new DeviceEvent { EventType = EventTypes.AppStarted, EventTime = At(9, 0) });
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "D1",
                    EventType = EventTypes.Connected,
                    EventTime = At(9, 1),
                }
            );
        });

        _sut.RecoverFromCrash();

        var events = _sut.GetAllDeviceEvents();
        events.ShouldContain(e => e.DeviceId == "D1" && e.EventType == EventTypes.ConnectionEnded);
        events.ShouldContain(e => e.EventType == EventTypes.AppEnded);
    }

    [Fact]
    public void RecoverFromCrash_CleanShutdown_NoBackfill()
    {
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(new DeviceEvent { EventType = EventTypes.AppStarted, EventTime = At(9, 0) });
            ctx.DeviceEvents.Add(new DeviceEvent { EventType = EventTypes.AppEnded, EventTime = At(10, 0) });
        });

        _sut.RecoverFromCrash();

        _sut.GetAllDeviceEvents().Count.ShouldBe(2); // last app event was AppEnded => clean
    }

    // ── RebuildDeviceSnapshots ──────────────────────────────────────────────────

    [Fact]
    public void RebuildDeviceSnapshots_RecomputesConnectionSeconds_AndClearsSession()
    {
        Seed(ctx =>
        {
            ctx.Devices.Add(
                new Device
                {
                    DeviceId = "D1",
                    DeviceName = "kb",
                    SessionStartedAt = At(9, 0),
                }
            );
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "D1",
                    EventType = EventTypes.Connected,
                    EventTime = At(9, 0),
                }
            );
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "D1",
                    EventType = EventTypes.Disconnected,
                    EventTime = At(9, 1),
                }
            );
        });

        _sut.RebuildDeviceSnapshots();

        var device = _sut.GetDevice("D1")!;
        device.SessionStartedAt.ShouldBeNull();
        device.TotalConnectionSeconds.ShouldBe(60);
    }

    // ── GetDashboardEvents (pre-range seeding) ──────────────────────────────────

    [Fact]
    public void GetDashboardEvents_SeedsStillOpenDevice_ButNotClosedOrPostClose()
    {
        var from = At(12, 0);
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "OPEN",
                    EventType = EventTypes.Connected,
                    EventTime = At(8, 0),
                }
            );
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "CLOSED",
                    EventType = EventTypes.Connected,
                    EventTime = At(8, 0),
                }
            );
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "CLOSED",
                    EventType = EventTypes.Disconnected,
                    EventTime = At(9, 0),
                }
            );
            ctx.DeviceEvents.Add(new DeviceEvent { EventType = EventTypes.AppStarted, EventTime = At(7, 0) });
        });

        var result = _sut.GetDashboardEvents(from, At(14, 0));

        result.DeviceEvents.ShouldContain(e => e.DeviceId == "OPEN"); // open at `from` => seeded
        result.DeviceEvents.ShouldNotContain(e => e.DeviceId == "CLOSED"); // closed before `from`
        result.AppLifecycleEvents.ShouldContain(e => e.EventType == EventTypes.AppStarted); // app running => seeded
    }

    [Fact]
    public void GetDashboardEvents_NullFrom_NoSeeds()
    {
        Seed(ctx =>
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "D1",
                    EventType = EventTypes.Connected,
                    EventTime = At(9, 0),
                }
            )
        );

        _sut.GetDashboardEvents(null, At(23, 0)).DeviceEvents.Count.ShouldBe(1);
    }

    // ── DevicesWithConnectionEndedInLastSession (computed once at startup) ───────

    [Fact]
    public void DevicesWithConnectionEndedInLastSession_IncludesDevicesClosedInLastSession()
    {
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(new DeviceEvent { EventType = EventTypes.AppStarted, EventTime = At(9, 0) });
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "D1",
                    EventType = EventTypes.ConnectionEnded,
                    EventTime = At(9, 30),
                }
            );
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "M1",
                    EventType = EventTypes.ConnectionEnded,
                    EventTime = At(9, 40),
                }
            );
            ctx.DeviceEvents.Add(new DeviceEvent { EventType = EventTypes.AppEnded, EventTime = At(10, 0) });
        });

        var closed = _sut.DevicesWithConnectionEndedInLastSession();
        closed.ShouldContain("D1");
        closed.ShouldContain("M1");
    }

    [Fact]
    public void DevicesWithConnectionEndedInLastSession_ExcludesDevicesNotClosed()
    {
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(new DeviceEvent { EventType = EventTypes.AppStarted, EventTime = At(9, 0) });
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "OTHER",
                    EventType = EventTypes.ConnectionEnded,
                    EventTime = At(9, 30),
                }
            );
            ctx.DeviceEvents.Add(new DeviceEvent { EventType = EventTypes.AppEnded, EventTime = At(10, 0) });
        });

        var closed = _sut.DevicesWithConnectionEndedInLastSession();
        closed.ShouldContain("OTHER");
        closed.ShouldNotContain("D1");
    }

    [Fact]
    public void DevicesWithConnectionEndedInLastSession_NoCompletedSession_ReturnsEmpty()
    {
        Seed(ctx => ctx.DeviceEvents.Add(new DeviceEvent { EventType = EventTypes.AppStarted, EventTime = At(9, 0) }));

        _sut.DevicesWithConnectionEndedInLastSession().ShouldBeEmpty();
    }

    [Fact]
    public void DevicesWithConnectionEndedInLastSession_OnlyConsidersTheMostRecentSession()
    {
        Seed(ctx =>
        {
            // An earlier completed session that closed D1.
            ctx.DeviceEvents.Add(new DeviceEvent { EventType = EventTypes.AppStarted, EventTime = At(8, 0) });
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "D1",
                    EventType = EventTypes.ConnectionEnded,
                    EventTime = At(8, 30),
                }
            );
            ctx.DeviceEvents.Add(new DeviceEvent { EventType = EventTypes.AppEnded, EventTime = At(8, 45) });
            // The most recent completed session closed only M1.
            ctx.DeviceEvents.Add(new DeviceEvent { EventType = EventTypes.AppStarted, EventTime = At(9, 0) });
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "M1",
                    EventType = EventTypes.ConnectionEnded,
                    EventTime = At(9, 30),
                }
            );
            ctx.DeviceEvents.Add(new DeviceEvent { EventType = EventTypes.AppEnded, EventTime = At(10, 0) });
        });

        var closed = _sut.DevicesWithConnectionEndedInLastSession();
        closed.ShouldContain("M1");
        closed.ShouldNotContain("D1"); // closed in the earlier session, not the last
    }

    // ── GetLastDeviceEvent ──────────────────────────────────────────────────────

    [Fact]
    public void GetLastDeviceEvent_EmptyDb_ReturnsNull() => _sut.GetLastDeviceEvent().ShouldBeNull();

    [Fact]
    public void GetLastDeviceEvent_FiltersByDeviceOrReturnsLatestOverall()
    {
        Seed(ctx =>
        {
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "D1",
                    EventType = EventTypes.Connected,
                    EventTime = At(9, 0),
                }
            );
            ctx.DeviceEvents.Add(
                new DeviceEvent
                {
                    DeviceId = "D2",
                    EventType = EventTypes.Connected,
                    EventTime = At(9, 1),
                }
            );
        });

        _sut.GetLastDeviceEvent("D1")!.DeviceId.ShouldBe("D1");
        _sut.GetLastDeviceEvent()!.DeviceId.ShouldBe("D2"); // latest overall
    }

    // ── GetDashboardDevices / visibility ────────────────────────────────────────

    [Fact]
    public void GetDashboardDevices_ExcludesHidden()
    {
        Seed(ctx =>
        {
            ctx.Devices.Add(
                new Device
                {
                    DeviceId = "VISIBLE",
                    DeviceName = "a",
                    DeviceType = DeviceTypes.Keyboard,
                }
            );
            ctx.Devices.Add(
                new Device
                {
                    DeviceId = "HIDDEN",
                    DeviceName = "b",
                    DeviceType = DeviceTypes.Keyboard,
                    IsHiddenFromDisplay = true,
                }
            );
        });

        var result = _sut.GetDashboardDevices();
        result.Devices.ShouldContain(d => d.DeviceId == "VISIBLE");
        result.Devices.ShouldNotContain(d => d.DeviceId == "HIDDEN");
    }

    [Fact]
    public void SetDeviceHiddenFromDisplay_MissingDevice_ReturnsFalse() =>
        _sut.SetDeviceHiddenFromDisplay("GHOST", true).ShouldBeFalse();

    [Fact]
    public void SetDeviceHiddenFromDisplay_Existing_Updates()
    {
        Seed(ctx => ctx.Devices.Add(new Device { DeviceId = "D1", DeviceName = "a" }));

        _sut.SetDeviceHiddenFromDisplay("D1", true).ShouldBeTrue();
        _sut.GetDevice("D1")!.IsHiddenFromDisplay.ShouldBeTrue();
    }

    [Fact]
    public void SetDeviceType_MissingDevice_ReturnsFalse() =>
        _sut.SetDeviceType("GHOST", DeviceTypes.Mouse).ShouldBeFalse();

    [Fact]
    public void SetDeviceType_Existing_Updates()
    {
        Seed(ctx =>
            ctx.Devices.Add(
                new Device { DeviceId = "D1", DeviceName = "device", DeviceType = DeviceTypes.Keyboard }
            )
        );

        _sut.SetDeviceType("D1", DeviceTypes.Mouse).ShouldBeTrue();

        _sut.GetDevice("D1")!.DeviceType.ShouldBe(DeviceTypes.Mouse);
    }

    // ── GetActivitySnapshots filtering ──────────────────────────────────────────

    [Fact]
    public void GetActivitySnapshots_FiltersByDeviceAndRange()
    {
        Seed(ctx =>
        {
            ctx.ActivitySnapshots.Add(new ActivitySnapshot { DeviceId = "D1", Minute = At(9, 0) });
            ctx.ActivitySnapshots.Add(new ActivitySnapshot { DeviceId = "D1", Minute = At(10, 0) });
            ctx.ActivitySnapshots.Add(new ActivitySnapshot { DeviceId = "D2", Minute = At(9, 0) });
        });

        _sut.GetActivitySnapshots("D1").Count.ShouldBe(2);
        _sut.GetActivitySnapshots(from: At(9, 30), to: At(10, 30)).Count.ShouldBe(1); // only the 10:00 rows
        _sut.GetActivitySnapshots().Count.ShouldBe(3); // no filter
    }
}
