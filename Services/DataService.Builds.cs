using System.Collections.Immutable;
using KeyPulse.Data;
using KeyPulse.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace KeyPulse.Services;

public partial class DataService
{
    internal ImmutableArray<DeviceBuildDaySnapshot> ReadDeviceBuildDays()
    {
        using var context = _factory.CreateDbContext();
        return context
            .DeviceBuildDays.AsNoTracking()
            .OrderBy(x => x.Day)
            .AsEnumerable()
            .Select(DeviceBuildJson.Snapshot)
            .ToImmutableArray();
    }

    internal BuildResult InitializeDeviceBuildDay(string deviceId, DateOnly today) =>
        WriteBuildDay(context =>
        {
            RequireConnectedKeyboard(context, deviceId);
            if (context.DeviceBuildDays.Any(x => x.DeviceId == deviceId && x.Day == today))
                return false;
            var previous = context
                .DeviceBuildDays.Where(x => x.DeviceId == deviceId && x.Day < today)
                .OrderByDescending(x => x.Day)
                .FirstOrDefault();
            var current = previous == null ? null : DeviceBuildJson.Snapshot(previous).Current;
            context.DeviceBuildDays.Add(
                new()
                {
                    DeviceId = deviceId,
                    Day = today,
                    BuildsJson = DeviceBuildJson.Write(current == null ? [] : [new(Guid.NewGuid(), current.Parts)]),
                }
            );
            return true;
        });

    internal BuildResult SaveDeviceBuild(
        string deviceId,
        DateOnly day,
        DeviceBuildDaySnapshot? captured,
        Guid? editBuildKey,
        IReadOnlyList<Guid> selected,
        DateOnly today
    ) =>
        WriteBuildDay(context =>
        {
            if (selected.Any(x => x == Guid.Empty) || selected.Distinct().Count() != selected.Count)
                throw new ArgumentException("Choose each valid part once.");
            if (editBuildKey == null)
            {
                if (day != today)
                    throw new ArgumentException("The day changed. Reload to add a build for today.");
                RequireConnectedKeyboard(context, deviceId);
            }
            var row = context.DeviceBuildDays.SingleOrDefault(x => x.DeviceId == deviceId && x.Day == day);
            CheckBuildRevision(row, captured);
            var builds = row == null ? new List<DeviceBuildSnapshot>() : DeviceBuildJson.Snapshot(row).Builds.ToList();
            var index = editBuildKey.HasValue ? builds.FindIndex(x => x.BuildKey == editBuildKey) : builds.Count - 1;
            if (editBuildKey.HasValue && index < 0)
                throw new DbUpdateConcurrencyException();
            var original = index >= 0 ? builds[index] : null;
            var retained = original?.Parts.ToDictionary(x => x.ItemKey) ?? [];
            if (selected.ToHashSet().SetEquals(retained.Keys))
                return false;
            var parts = selected
                .Select(key =>
                {
                    if (retained.TryGetValue(key, out var saved))
                        return saved;
                    var owned = context.CollectionItems.SingleOrDefault(x => x.ItemKey == key);
                    if (
                        owned == null
                        || owned.PartKind is not (PartKinds.Switch or PartKinds.Keycap)
                        || string.IsNullOrWhiteSpace(owned.Name)
                    )
                        throw new ArgumentException("Choose owned switches or keycaps, or retain the saved parts.");
                    return new BuildPartSnapshot(key, owned.PartKind, owned.Name, owned.Variant);
                })
                .ToImmutableArray();
            if (editBuildKey.HasValue)
                builds[index] = new(editBuildKey.Value, parts);
            else
                builds.Add(new(Guid.NewGuid(), parts));
            if (row == null)
            {
                row = new() { DeviceId = deviceId, Day = day };
                context.DeviceBuildDays.Add(row);
            }
            else
                row.Revision = checked(row.Revision + 1);
            row.BuildsJson = DeviceBuildJson.Write(builds);
            return true;
        });

    internal BuildResult DeleteDeviceBuild(DeviceBuildDaySnapshot captured, Guid buildKey) =>
        WriteBuildDay(context =>
        {
            var row = context.DeviceBuildDays.SingleOrDefault(x =>
                x.DeviceId == captured.DeviceId && x.Day == captured.Day
            );
            CheckBuildRevision(row, captured);
            var builds = DeviceBuildJson.Snapshot(row!).Builds.ToList();
            if (builds.RemoveAll(x => x.BuildKey == buildKey) != 1)
                throw new DbUpdateConcurrencyException();
            row!.BuildsJson = DeviceBuildJson.Write(builds);
            row.Revision = checked(row.Revision + 1);
            return true;
        });

    private static void RequireConnectedKeyboard(ApplicationDbContext context, string deviceId)
    {
        if (
            context.Devices.SingleOrDefault(x => x.DeviceId == deviceId)
            is not { DeviceType: DeviceTypes.Keyboard, IsConnected: true }
        )
            throw new ArgumentException("Connect this keyboard before adding a build.");
    }

    private static void CheckBuildRevision(DeviceBuildDay? row, DeviceBuildDaySnapshot? captured)
    {
        if (row?.DayKey != captured?.DayKey || row?.Revision != captured?.Revision)
            throw new DbUpdateConcurrencyException();
    }

    private BuildResult WriteBuildDay(Func<ApplicationDbContext, bool> change)
    {
        lock (CollectionGate)
        {
            try
            {
                using var context = _factory.CreateDbContext();
                using var transaction = context.Database.BeginTransaction();
                if (!change(context))
                    return new(BuildResultStatus.Success);
                context.SaveChanges();
                transaction.Commit();
                return new(BuildResultStatus.Success, Changed: true);
            }
            catch (DbUpdateConcurrencyException)
            {
                return BuildResult.Conflict();
            }
            catch (ArgumentException ex)
            {
                return new(BuildResultStatus.ValidationError, ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not read or save daily keyboard builds");
                return new(BuildResultStatus.StorageFailure, "Could not read or save builds. Retry or check logs.");
            }
        }
    }
}
