using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using KeyPulse.Helpers;
using KeyPulse.Models;
using Serilog;

namespace KeyPulse.Services;

public enum CatalogLoadStatus
{
    NotLoaded,
    Loading,
    Loaded,
    Failed,
}

public sealed record CatalogState(
    CatalogLoadStatus Status,
    int Version,
    ImmutableArray<CatalogEntry> Entries,
    ImmutableDictionary<string, CatalogEntry> ById,
    ImmutableDictionary<string, string> SearchText,
    string? Error = null
)
{
    internal static CatalogState Empty(CatalogLoadStatus status, string? error = null) =>
        new(
            status,
            0,
            [],
            ImmutableDictionary<string, CatalogEntry>.Empty,
            ImmutableDictionary<string, string>.Empty,
            error
        );
}

public sealed class CatalogService
{
    private readonly object _sync = new();
    private readonly Func<string, Stream> _open;
    private readonly Lazy<CatalogSearch> _search;
    private readonly Dictionary<string, Task<CatalogState>> _loads = [];
    private readonly Dictionary<string, CatalogState> _states = [];

    public CatalogService()
        : this(OpenResource, () => CatalogSearch.FromResource()) { }

    internal CatalogService(Func<string, Stream> open, Func<CatalogSearch>? search = null)
    {
        _open = open;
        _search = new(search ?? (() => new CatalogSearch([])));
    }

    public CatalogSearch Search => _search.Value;

    internal static Stream OpenResource(string name) =>
        typeof(CatalogService).Assembly.GetManifestResourceStream(name)
        ?? throw new IOException("The bundled catalog resource is missing.");

    public CatalogState GetState(string kind)
    {
        _ = PartKinds.Unit(kind);
        lock (_sync)
            return _states.GetValueOrDefault(kind) ?? CatalogState.Empty(CatalogLoadStatus.NotLoaded);
    }

    public Task<CatalogState> LoadAsync(string kind, bool retry = false)
    {
        _ = PartKinds.Unit(kind);
        lock (_sync)
        {
            if (
                _loads.TryGetValue(kind, out var existing)
                && !(retry && GetState(kind).Status == CatalogLoadStatus.Failed)
            )
                return existing;
            _states[kind] = CatalogState.Empty(CatalogLoadStatus.Loading);
            return _loads[kind] = Task.Run(() =>
            {
                CatalogState state;
                try
                {
                    using var stream = _open(
                        kind == PartKinds.Switch ? "KeyPulse.Catalogs.switches.json" : "KeyPulse.Catalogs.keycaps.json"
                    );
                    state = Parse(kind, stream, Search);
                    Log.Information(
                        "Loaded {PartKind} catalog: {EntryCount} entries, version {CatalogVersion}",
                        kind,
                        state.Entries.Length,
                        state.Version
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "The {PartKind} catalog could not be loaded", kind);
                    state = CatalogState.Empty(
                        CatalogLoadStatus.Failed,
                        "The catalog could not be loaded. Check logs for details."
                    );
                }
                lock (_sync)
                    _states[kind] = state;
                return state;
            });
        }
    }

    internal static CatalogState Parse(string kind, Stream stream, CatalogSearch search)
    {
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        CheckFields(root, ["schemaVersion", "catalogVersion", "totalCount", "entries"]);
        if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("catalogVersion").GetInt32() < 1)
            throw new ArgumentException("Unsupported catalog version.");
        var rows = ImmutableArray.CreateBuilder<CatalogEntry>();
        var byId = ImmutableDictionary.CreateBuilder<string, CatalogEntry>(StringComparer.Ordinal);
        var searchText = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var entry in root.GetProperty("entries").EnumerateArray())
        {
            CheckFields(
                entry,
                kind == PartKinds.Switch
                    ?
                    [
                        "id",
                        "name",
                        "manufacturer",
                        "brand",
                        "designer",
                        "variants",
                        "switchType",
                        "switchFamily",
                        "isLowProfile",
                    ]
                    : ["id", "name", "manufacturer", "brand", "designer", "variants", "profile", "material"]
            );
            var id = Text(entry, "id", true)!;
            if (!Regex.IsMatch(id, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant))
                throw new ArgumentException("Invalid catalog id.");
            var variants = ImmutableArray<string>.Empty;
            if (entry.TryGetProperty("variants", out var labels))
            {
                variants = labels.EnumerateArray().Select(x => x.GetString()!).ToImmutableArray();
                if (variants.IsEmpty)
                    throw new ArgumentException("Empty catalog variant list.");
            }
            PartDetails details;
            if (kind == PartKinds.Switch)
            {
                var low = entry.TryGetProperty("isLowProfile", out var flag);
                if (low && (flag.ValueKind != JsonValueKind.True))
                    throw new ArgumentException("Invalid low-profile flag.");
                details = new SwitchPartDetails
                {
                    SwitchType = Text(entry, "switchType", true),
                    SwitchFamily = Text(entry, "switchFamily", true),
                    IsLowProfile = low,
                    Variants = variants,
                };
            }
            else
                details = new KeycapPartDetails
                {
                    Profile = Text(entry, "profile"),
                    Material = Text(entry, "material"),
                    Variants = variants,
                };
            PartDetailsCodec.Validate(kind, details, true);
            var description = new PartDescription(
                Text(entry, "name", true)!,
                Text(entry, "manufacturer"),
                Text(entry, "brand"),
                Text(entry, "designer"),
                details
            );
            CatalogEntry item =
                kind == PartKinds.Switch
                    ? new SwitchCatalogEntry(id, description)
                    : new KeycapCatalogEntry(id, description);
            byId.Add(id, item);
            rows.Add(item);
            searchText.Add(id, search.Build(description));
        }
        if (root.GetProperty("totalCount").GetInt32() != rows.Count)
            throw new ArgumentException("Catalog count mismatch.");
        return new(
            CatalogLoadStatus.Loaded,
            root.GetProperty("catalogVersion").GetInt32(),
            rows.ToImmutable(),
            byId.ToImmutable(),
            searchText.ToImmutable()
        );
    }

    private static void CheckFields(JsonElement element, string[] fields)
    {
        PartDetailsCodec.ValidateObject(element);
        foreach (var property in element.EnumerateObject())
            if (!fields.Contains(property.Name, StringComparer.Ordinal))
                throw new ArgumentException("Unexpected catalog field.");
    }

    private static string? Text(JsonElement element, string name, bool required = false)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            if (required)
                throw new ArgumentException("Missing catalog field: " + name);
            return null;
        }
        var text = value.GetString();
        PartDetailsCodec.ValidateText(text, true);
        return text;
    }
}
