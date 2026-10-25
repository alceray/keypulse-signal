using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KeyPulse.Models;
using KeyPulse.Services;

namespace KeyPulse.Helpers;

public sealed record SearchAlias(string Canonical, string Alias, bool CaseSensitive);

public sealed class CatalogSearch
{
    private readonly Regex _pattern;
    private readonly Dictionary<string, string> _exact = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _regular = new(StringComparer.OrdinalIgnoreCase);

    public CatalogSearch(IEnumerable<SearchAlias> aliases)
    {
        foreach (
            var alias in aliases.Concat(
                new[]
                {
                    new SearchAlias("White on Black", "WoB", false),
                    new SearchAlias("Black on White", "BoW", false),
                }
            )
        )
            (alias.CaseSensitive ? _exact : _regular).Add(alias.Alias, alias.Canonical);
        var alternatives = _exact
            .Keys.Select(x => "(?-i:" + Regex.Escape(x) + ")")
            .Concat(_regular.Keys.OrderByDescending(x => x.Length).Select(x => "(?i:" + Regex.Escape(x) + ")"));
        _pattern = new Regex(
            @"(?<![\p{L}\p{N}_])(?:" + string.Join("|", alternatives) + @")(?![\p{L}\p{N}_])",
            RegexOptions.CultureInvariant
        );
    }

    internal static CatalogSearch FromResource()
    {
        using var stream = CatalogService.OpenResource("KeyPulse.Catalogs.search-aliases.json");
        return new(
            JsonSerializer.Deserialize<SearchAlias[]>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? throw new ArgumentException("Invalid search aliases.")
        );
    }

    public static string Normalize(string value)
    {
        var result = new StringBuilder();
        foreach (var c in value.Trim().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                result.Append(char.ToLowerInvariant(c));
        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    private string NormalizeAliases(string text) =>
        string.Concat(
            Normalize(
                    _pattern.Replace(text, m => _exact.TryGetValue(m.Value, out var value) ? value : _regular[m.Value])
                )
                .Where(c => !char.IsPunctuation(c))
        );

    public string Prepare(string text) => string.Concat(NormalizeAliases(text).Where(c => !char.IsWhiteSpace(c)));

    // Create once when search text changes, then reuse for already-prepared rows.
    public Func<string, bool> CreateFilter(string query)
    {
        var words = NormalizeAliases(query).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return preparedText =>
        {
            foreach (var word in words)
                if (!preparedText.Contains(word, StringComparison.Ordinal))
                    return false;
            return true;
        };
    }

    public string Build(PartDescription description, string? notes = null) =>
        Prepare(
            string.Join(
                " ",
                new[]
                {
                    description.Name,
                    description.Manufacturer,
                    description.Brand,
                    description.Designer,
                    notes,
                }.Concat(DetailTerms(description.Details))
            )
        );

    public string Build(CollectionSnapshot item) =>
        Prepare(
            string.Join(
                " ",
                new[] { item.Name, item.Manufacturer, item.Brand, item.Designer, item.Notes }.Concat(
                    DetailTerms(item.Details)
                )
            )
        );

    private static IEnumerable<string?> DetailTerms(PartDetails? details) =>
        details switch
        {
            SwitchPartDetails s => new[] { s.SwitchType, s.SwitchFamily }.Concat(s.Variants),
            KeycapPartDetails k => new[] { k.Profile, k.Material }.Concat(k.Variants),
            _ => [],
        };
}
