namespace KeyPulse.Helpers;

/// <summary>
/// Tracks distinct seconds-of-day in which each device saw any input, for today's live "active time"
/// overlay. Presentation-only: nothing here is persisted, so the running second-accurate total exists
/// only while the app is up and resets at local midnight.
///
/// Seed a device from its persisted minute total so today starts at the minute-granular value and
/// ticks up in seconds from there. Not thread-safe; callers guard access with their own lock, as the
/// view-model already does for its live overlay state.
/// </summary>
public sealed class ActiveSecondsAccumulator
{
    private readonly Dictionary<string, long> _secondsByDevice = [];
    private readonly Dictionary<string, long> _lastSecondOfDayByDevice = [];
    private readonly HashSet<string> _seededDevices = [];
    private DateOnly _day;

    /// <summary>Drops all state when the local day changes. Returns true if a reset happened.</summary>
    public bool ResetIfDayChanged(DateOnly today)
    {
        if (_day == today)
            return false;

        _day = today;
        _secondsByDevice.Clear();
        _lastSecondOfDayByDevice.Clear();
        _seededDevices.Clear();
        return true;
    }

    /// <summary>
    /// Raises a device to its persisted baseline once per day. Repeat calls are ignored so a refresh
    /// can't stack on top of seconds already counted this session; the first call keeps the larger of
    /// the baseline and any seconds already recorded before the seed arrived.
    /// </summary>
    public void Seed(string deviceId, long baselineSeconds)
    {
        if (_seededDevices.Add(deviceId))
            _secondsByDevice[deviceId] = Math.Max(_secondsByDevice.GetValueOrDefault(deviceId), baselineSeconds);
    }

    /// <summary>Counts the second of <paramref name="localNow"/> as active, at most once per second.</summary>
    public void RecordActivity(string deviceId, DateTime localNow)
    {
        var secondOfDay = (long)localNow.TimeOfDay.TotalSeconds;

        // Forward-moving wall clock: comparing against the last counted second collapses the burst of
        // inputs within one second into a single active second.
        if (_lastSecondOfDayByDevice.TryGetValue(deviceId, out var last) && last == secondOfDay)
            return;

        _lastSecondOfDayByDevice[deviceId] = secondOfDay;
        _secondsByDevice[deviceId] = _secondsByDevice.GetValueOrDefault(deviceId) + 1;
    }

    public long GetActiveSeconds(string deviceId) => _secondsByDevice.GetValueOrDefault(deviceId);
}
