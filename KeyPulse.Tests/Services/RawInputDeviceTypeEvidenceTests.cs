using KeyPulse.Data;
using KeyPulse.Models;
using KeyPulse.Services;
using KeyPulse.Tests.Infrastructure;

namespace KeyPulse.Tests.Services;

public sealed class RawInputDeviceTypeEvidenceTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly AppTimerService _timer = new();
    private readonly DailyStatsService _dailyStats;
    private readonly DataService _dataService;
    private readonly RawInputService _sut;

    public RawInputDeviceTypeEvidenceTests()
    {
        _dailyStats = new DailyStatsService(_db.Factory, _timer);
        _dataService = new DataService(_db.Factory, _dailyStats);
        _sut = new RawInputService(_dataService);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _dailyStats.Dispose();
        _timer.Dispose();
        _db.Dispose();
    }

    private void SeedDevice(string id, DeviceTypes type)
    {
        using var ctx = _db.CreateContext();
        ctx.Devices.Add(new Device { DeviceId = id, DeviceName = id, DeviceType = type });
        ctx.SaveChanges();
    }

    [Fact]
    public void OpposingPackets_EmitOnceAtThreshold()
    {
        SeedDevice("D1", DeviceTypes.Keyboard);
        var suggestions = new List<(string DeviceId, DeviceTypes Type)>();
        _sut.DeviceTypeMismatchSuggested += (id, type) => suggestions.Add((id, type));

        for (var i = 0; i < RawInputService.DeviceTypeMismatchPacketThreshold - 1; i++)
            _sut.ObserveDeviceType("D1", DeviceTypes.Mouse);

        suggestions.ShouldBeEmpty();

        _sut.ObserveDeviceType("D1", DeviceTypes.Mouse);
        _sut.ObserveDeviceType("D1", DeviceTypes.Mouse);

        suggestions.ShouldBe([("D1", DeviceTypes.Mouse)]);
    }

    [Fact]
    public void MatchingPacket_ResetsOpposingCount()
    {
        SeedDevice("D1", DeviceTypes.Keyboard);
        var suggestions = new List<DeviceTypes>();
        _sut.DeviceTypeMismatchSuggested += (_, type) => suggestions.Add(type);

        for (var i = 0; i < RawInputService.DeviceTypeMismatchPacketThreshold - 1; i++)
            _sut.ObserveDeviceType("D1", DeviceTypes.Mouse);

        _sut.ObserveDeviceType("D1", DeviceTypes.Keyboard);

        for (var i = 0; i < RawInputService.DeviceTypeMismatchPacketThreshold - 1; i++)
            _sut.ObserveDeviceType("D1", DeviceTypes.Mouse);

        suggestions.ShouldBeEmpty();

        _sut.ObserveDeviceType("D1", DeviceTypes.Mouse);
        suggestions.ShouldBe([DeviceTypes.Mouse]);
    }

    [Theory]
    [InlineData(DeviceTypes.Unknown)]
    [InlineData(DeviceTypes.Other)]
    public void UnclassifiedTypes_DoNotSuggest(DeviceTypes assignedType)
    {
        SeedDevice("D1", assignedType);
        var suggestionCount = 0;
        _sut.DeviceTypeMismatchSuggested += (_, _) => suggestionCount++;

        for (var i = 0; i < RawInputService.DeviceTypeMismatchPacketThreshold; i++)
            _sut.ObserveDeviceType("D1", DeviceTypes.Mouse);

        suggestionCount.ShouldBe(0);
    }

    [Fact]
    public void ResetEvidence_UsesNewAssignedType()
    {
        SeedDevice("D1", DeviceTypes.Keyboard);
        var suggestionCount = 0;
        _sut.DeviceTypeMismatchSuggested += (_, _) => suggestionCount++;

        _sut.ResetDeviceTypeEvidence("D1", DeviceTypes.Mouse);
        for (var i = 0; i < RawInputService.DeviceTypeMismatchPacketThreshold; i++)
            _sut.ObserveDeviceType("D1", DeviceTypes.Mouse);

        suggestionCount.ShouldBe(0);
    }
}
