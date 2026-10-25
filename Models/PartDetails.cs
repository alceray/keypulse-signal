using System.Collections.Immutable;

namespace KeyPulse.Models;

public static class PartKinds
{
    public const string Switch = "Switch";
    public const string Keycap = "Keycap";

    public static int MaxQuantity(string kind) =>
        kind switch
        {
            Switch => 10_000,
            Keycap => 100,
            _ => throw new ArgumentException("Unsupported part category."),
        };

    public static string Unit(string kind) =>
        kind switch
        {
            Switch => "Piece",
            Keycap => "Kit",
            _ => throw new ArgumentException("Unsupported part category."),
        };
}

public abstract record PartDetails
{
    public ImmutableArray<string> Variants { get; init; } = [];
}

public sealed record SwitchPartDetails : PartDetails
{
    public string? SwitchType { get; init; }
    public string? SwitchFamily { get; init; }
    public bool? IsLowProfile { get; init; }
}

public sealed record KeycapPartDetails : PartDetails
{
    public string? Profile { get; init; }
    public string? Material { get; init; }
}

public record PartDescription(string Name, string? Manufacturer, string? Brand, string? Designer, PartDetails Details);

public abstract record CatalogEntry(string Id, PartDescription Description);

public sealed record SwitchCatalogEntry(string Id, PartDescription Description) : CatalogEntry(Id, Description);

public sealed record KeycapCatalogEntry(string Id, PartDescription Description) : CatalogEntry(Id, Description);

public sealed record CollectionSnapshot(
    Guid ItemKey,
    string PartKind,
    string? CatalogEntryId,
    string Name,
    string? Manufacturer,
    string? Brand,
    string? Designer,
    string QuantityUnit,
    long Revision,
    int Quantity,
    string? Notes,
    DateTime AddedAt,
    PartDetails? Details,
    string? DetailsError
);

public enum CollectionResultStatus
{
    Success,
    ValidationError,
    Conflict,
    StorageFailure,
}

public sealed record CollectionResult(
    CollectionResultStatus Status,
    ImmutableArray<CollectionSnapshot> Items,
    string? Message = null
)
{
    public bool Succeeded => Status == CollectionResultStatus.Success;

    public static CollectionResult Failure(CollectionResultStatus status, string message) => new(status, [], message);
}
