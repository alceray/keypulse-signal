using System.Collections.Immutable;
using System.Text.Json;

namespace KeyPulse.Models;

public class DeviceBuildDay
{
    public int DeviceBuildDayId { get; set; }
    public Guid DayKey { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = "";
    public DateOnly Day { get; set; }
    public long Revision { get; set; } = 1;
    public int BuildsSchemaVersion { get; set; } = 1;
    public string BuildsJson { get; set; } = "[]";
}

public sealed record BuildPartSnapshot(Guid ItemKey, string PartKind, string Name, string Variant)
{
    public string Label => string.IsNullOrEmpty(Variant) ? Name : $"{Name} [{Variant}]";
}

public sealed record DeviceBuildSnapshot(Guid BuildKey, ImmutableArray<BuildPartSnapshot> Parts)
{
    public string Summary => Parts.IsEmpty
        ? "No parts assigned"
        : string.Join(
            Environment.NewLine,
            Parts.GroupBy(x => x.PartKind)
                .OrderBy(group => group.Key == PartKinds.Switch ? 0 : 1)
                .Select(group => string.Join(", ", group.Select(x => x.Label)))
        );
}

public sealed record DeviceBuildDaySnapshot(
    Guid DayKey,
    string DeviceId,
    DateOnly Day,
    long Revision,
    ImmutableArray<DeviceBuildSnapshot> Builds
)
{
    public DeviceBuildSnapshot? Current => Builds.LastOrDefault();
}

internal static class DeviceBuildJson
{
    internal static DeviceBuildDaySnapshot Snapshot(DeviceBuildDay row)
    {
        if (row.BuildsSchemaVersion != 1)
            throw new JsonException("Unsupported daily parts history version.");
        var builds = JsonSerializer.Deserialize<ImmutableArray<DeviceBuildSnapshot>>(row.BuildsJson);
        if (
            builds.IsDefault
            || builds.Any(x =>
                x == null
                || x.BuildKey == Guid.Empty
                || x.Parts.IsDefault
                || x.Parts.Any(p =>
                    p == null
                    || p.ItemKey == Guid.Empty
                    || p.PartKind is not (PartKinds.Switch or PartKinds.Keycap)
                    || string.IsNullOrWhiteSpace(p.Name)
                    || p.Variant == null
                )
                || x.Parts.Select(p => p.ItemKey).Distinct().Count() != x.Parts.Length
            )
            || builds.Select(x => x.BuildKey).Distinct().Count() != builds.Length
        )
            throw new JsonException("Invalid daily parts history.");
        return new(row.DayKey, row.DeviceId, row.Day, row.Revision, builds);
    }

    internal static string Write(IEnumerable<DeviceBuildSnapshot> builds) =>
        JsonSerializer.Serialize(
            builds.Select(b => new
            {
                b.BuildKey,
                Parts = b
                    .Parts.OrderBy(p => p.ItemKey)
                    .Select(p => new
                    {
                        p.ItemKey,
                        p.PartKind,
                        p.Name,
                        p.Variant,
                    }),
            })
        );
}

public sealed record DeviceConnectedEvent(
    Device Device,
    DateTime SessionStartedAt,
    bool IsReconnection,
    DateTime? PreviousDisconnect
);

public enum BuildResultStatus
{
    Success,
    Conflict,
    ValidationError,
    StorageFailure,
}

public sealed record BuildResult(BuildResultStatus Status, string? Error = null, bool Changed = false)
{
    public bool Succeeded => Status == BuildResultStatus.Success;

    public static BuildResult Conflict() =>
        new(BuildResultStatus.Conflict, "This day's builds changed. Reload before saving.");
}
