using System.Collections.Immutable;
using KeyPulse.Models;
using Serilog;

namespace KeyPulse.Services;

public sealed class DeviceBuildService(DataService data)
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _gate = new(1);
    private ImmutableArray<DeviceBuildDaySnapshot> _days = [];
    private bool _loaded,
        _accepting = true;
    private string? _loadError;
    private readonly List<Task> _pending = [];
    public event Action<string, DateOnly>? Changed;
    public string? LoadError
    {
        get
        {
            lock (_sync)
                return _loadError;
        }
    }

    public DeviceBuildDaySnapshot? GetDay(string deviceId, DateOnly day)
    {
        lock (_sync)
            return _days.FirstOrDefault(x => x.DeviceId == deviceId && x.Day == day);
    }

    public DeviceBuildSnapshot? GetCurrent(string deviceId, DateOnly day) => GetDay(deviceId, day)?.Current;

    public async Task<bool> LoadAsync(bool retry = false)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_sync)
                if (_loaded && !retry)
                    return true;
            return await Task.Run(Read).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool Read()
    {
        try
        {
            var days = data.ReadDeviceBuildDays();
            lock (_sync)
            {
                _days = days;
                _loaded = true;
                _loadError = null;
            }
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not load daily keyboard builds");
            lock (_sync)
            {
                _loaded = false;
                _loadError = "Could not read builds. Retry or check logs.";
            }
            return false;
        }
    }

    public Task<BuildResult> InitializeDayAsync(string deviceId, DateOnly day) =>
        Queue(
            deviceId,
            day,
            () =>
                day != DateOnly.FromDateTime(DateTime.Now)
                    ? new(BuildResultStatus.ValidationError, "The day changed. Try again.")
                    : data.InitializeDeviceBuildDay(deviceId, day)
        );

    public Task<BuildResult> SaveAsync(
        string deviceId,
        DateOnly day,
        DeviceBuildDaySnapshot? captured,
        Guid? editBuildKey,
        IReadOnlyList<Guid> selected
    )
    {
        var keys = selected.ToArray();
        return Queue(
            deviceId,
            day,
            () => data.SaveDeviceBuild(deviceId, day, captured, editBuildKey, keys, DateOnly.FromDateTime(DateTime.Now))
        );
    }

    public Task<BuildResult> DeleteAsync(DeviceBuildDaySnapshot captured, Guid buildKey) =>
        Queue(captured.DeviceId, captured.Day, () => data.DeleteDeviceBuild(captured, buildKey));

    private Task<BuildResult> Queue(string deviceId, DateOnly day, Func<BuildResult> change)
    {
        lock (_sync)
        {
            if (!_accepting)
                return Task.FromResult(
                    new BuildResult(BuildResultStatus.StorageFailure, "The application is closing.")
                );
            var task = WriteAsync(deviceId, day, change);
            _pending.RemoveAll(x => x.IsCompleted);
            _pending.Add(task);
            return task;
        }
    }

    private async Task<BuildResult> WriteAsync(string deviceId, DateOnly day, Func<BuildResult> change)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        BuildResult result;
        try
        {
            result = await Task.Run(() =>
                {
                    if (!Read())
                        return new BuildResult(BuildResultStatus.StorageFailure, LoadError);
                    var saved = change();
                    if (saved.Changed)
                        Read();
                    return saved;
                })
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
        if (result.Changed)
            Changed?.Invoke(deviceId, day);
        return result;
    }

    public Task StopAndDrainAsync()
    {
        lock (_sync)
        {
            _accepting = false;
            return Task.WhenAll(_pending);
        }
    }
}
