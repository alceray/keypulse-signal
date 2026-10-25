using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using KeyPulse.Models;

namespace KeyPulse.Helpers;

public static class PartDetailsCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private static readonly HashSet<string> Types = ["linear", "tactile", "clicky", "linear/clicky", "latching"];
    private static readonly Regex Family = new(
        "^(MX|Alps|Choc|HE|EC|Optical|Legacy|Other)(/(MX|Alps|Choc|HE|EC|Optical|Legacy|Other))*$",
        RegexOptions.CultureInvariant
    );

    public static void Validate(string kind, PartDetails details, bool catalog)
    {
        _ = PartKinds.Unit(kind);
        if (kind == PartKinds.Switch && details is SwitchPartDetails s)
        {
            if ((catalog || s.SwitchType != null) && (s.SwitchType == null || !Types.Contains(s.SwitchType)))
                throw new ArgumentException("Choose a supported switch type.");
            if ((catalog || s.SwitchFamily != null) && (s.SwitchFamily == null || !Family.IsMatch(s.SwitchFamily)))
                throw new ArgumentException("Choose a supported switch family.");
            if (catalog && s.IsLowProfile == null)
                throw new ArgumentException("Catalog low-profile state is required.");
        }
        else if (kind == PartKinds.Keycap && details is KeycapPartDetails k)
        {
            ValidateText(k.Profile);
            ValidateText(k.Material);
        }
        else
            throw new ArgumentException("Details do not match the part category.");
        if (details.Variants.IsDefault)
            throw new ArgumentException("Invalid variant list.");
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in details.Variants)
        {
            ValidateText(label, required: true);
            if (!labels.Add(label))
                throw new ArgumentException("Variant labels must be unique.");
        }
    }

    public static void ValidateText(string? value, bool required = false)
    {
        if (value == null && !required)
            return;
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
            throw new ArgumentException("Text values must be nonempty and trimmed.");
    }

    public static string Serialize(string kind, PartDetails details, bool catalog)
    {
        Validate(kind, details, catalog);
        return JsonSerializer.Serialize(details, details.GetType(), Options);
    }

    public static PartDetails Parse(string kind, int version, string unit, string json, bool catalog)
    {
        if (version != 1)
            throw new ArgumentException("This details version requires a compatible application.");
        if (unit != PartKinds.Unit(kind))
            throw new ArgumentException("Unexpected quantity unit.");
        using var document = JsonDocument.Parse(json);
        ValidateObject(document.RootElement);
        PartDetails details =
            kind == PartKinds.Switch
                ? document.RootElement.Deserialize<SwitchPartDetails>(Options)!
                : document.RootElement.Deserialize<KeycapPartDetails>(Options)!;
        Validate(kind, details, catalog);
        return details;
    }

    internal static void ValidateObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Expected a JSON object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!names.Add(property.Name))
                throw new ArgumentException("Duplicate JSON property.");
    }

    public static CollectionSnapshot Snapshot(CollectionItem row)
    {
        PartDetails? details = null;
        string? error = null;
        try
        {
            details = Parse(
                row.PartKind,
                row.DetailsSchemaVersion,
                row.QuantityUnit,
                row.DetailsJson,
                row.CatalogEntryId != null
            );
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException or InvalidOperationException)
        {
            error = "Details unavailable: " + ex.Message;
        }
        return new(
            row.ItemKey,
            row.PartKind,
            row.CatalogEntryId,
            row.Name,
            row.Manufacturer,
            row.Brand,
            row.Designer,
            row.QuantityUnit,
            row.Revision,
            row.Quantity,
            row.Notes,
            row.AddedAt,
            details,
            error
        );
    }
}
