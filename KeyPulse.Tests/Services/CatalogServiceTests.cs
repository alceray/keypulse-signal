using System.IO;
using System.Text;
using System.Text.Json;
using KeyPulse.Helpers;
using KeyPulse.Models;
using KeyPulse.Services;

namespace KeyPulse.Tests.Services;

public class CatalogServiceTests
{
    internal const string SwitchJson =
        """{"schemaVersion":1,"catalogVersion":2,"totalCount":1,"entries":[{"id":"sample","name":"Sample","switchType":"linear","switchFamily":"MX","variants":["62g","67g"]}]}""";
    internal const string EmptyJson = """{"schemaVersion":1,"catalogVersion":1,"totalCount":0,"entries":[]}""";

    internal static Stream StreamOf(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

    internal static CatalogService Catalog(string json = SwitchJson) =>
        new(name => StreamOf(name.Contains("switches") ? json : EmptyJson));

    [Fact]
    public async Task EmbeddedCatalogs_LoadIndependentlyAndMemoize()
    {
        var service = new CatalogService();
        service.GetState(PartKinds.Switch).Status.ShouldBe(CatalogLoadStatus.NotLoaded);
        foreach (var kind in new[] { PartKinds.Switch, PartKinds.Keycap })
        {
            var task = service.LoadAsync(kind);
            service.LoadAsync(kind).ShouldBeSameAs(task);
            var state = await task;
            state.Error.ShouldBeNull();
            state.Status.ShouldBe(CatalogLoadStatus.Loaded);
            state.Entries.Length.ShouldBeGreaterThan(100);
            state.ById.Count.ShouldBe(state.Entries.Length);
            state.SearchText.Count.ShouldBe(state.Entries.Length);
            service.LoadAsync(kind, true).ShouldBeSameAs(task);
        }
    }

    [Fact]
    public async Task Failure_IsNotEmptySuccess_AndOnlyFailedKindRetries()
    {
        var fail = true;
        var reads = 0;
        var service = new CatalogService(name =>
        {
            Interlocked.Increment(ref reads);
            if (name.Contains("switches") && fail)
                throw new IOException("missing");
            return StreamOf(EmptyJson);
        });
        var failed = service.LoadAsync(PartKinds.Switch);
        (await failed).Status.ShouldBe(CatalogLoadStatus.Failed);
        (await service.LoadAsync(PartKinds.Keycap)).Status.ShouldBe(CatalogLoadStatus.Loaded);
        service.LoadAsync(PartKinds.Switch).ShouldBeSameAs(failed);
        fail = false;
        (await service.LoadAsync(PartKinds.Switch, true)).Status.ShouldBe(CatalogLoadStatus.Loaded);
        reads.ShouldBe(3);
    }

    [Theory]
    [InlineData("\"switchType\":\"linear\",", "")]
    [InlineData("\"switchFamily\":\"MX\",", "")]
    [InlineData("\"linear\"", "\"unknown\"")]
    [InlineData("\"linear\"", "\"\"")]
    [InlineData("\"MX\"", "\"MX/Unknown\"")]
    [InlineData("\"variants\":[\"62g\",\"67g\"]", "\"isLowProfile\":false")]
    [InlineData("[\"62g\",\"67g\"]", "[]")]
    [InlineData("[\"62g\",\"67g\"]", "[\"62g\",\"62G\"]")]
    [InlineData("[\"62g\",\"67g\"]", "[\" 62g\"]")]
    [InlineData("[\"62g\",\"67g\"]", "[{}]")]
    [InlineData("\"totalCount\":1", "\"totalCount\":2")]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":2")]
    [InlineData("\"id\":\"sample\"", "\"id\":\"sample\",\"id\":\"duplicate\"")]
    public async Task InvalidCatalogs_FailWithoutEscaping(string before, string after)
    {
        var state = await Catalog(SwitchJson.Replace(before, after)).LoadAsync(PartKinds.Switch);
        state.Status.ShouldBe(CatalogLoadStatus.Failed);
        state.Entries.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("linear", "MX")]
    [InlineData("tactile", "Alps")]
    [InlineData("clicky", "Choc")]
    [InlineData("linear/clicky", "EC/HE")]
    [InlineData("latching", "Legacy")]
    public async Task SupportedSwitchMetadata_NormalizesOmission(string type, string family)
    {
        var state = await Catalog(SwitchJson.Replace("linear", type).Replace("MX", family)).LoadAsync(PartKinds.Switch);
        state.Status.ShouldBe(CatalogLoadStatus.Loaded);
        var details = (SwitchPartDetails)state.Entries.Single().Description.Details;
        details.IsLowProfile.ShouldBe(false);
        details.Variants.ShouldBe(new[] { "62g", "67g" });
    }

    [Theory]
    [InlineData("WoB", "White on Black")]
    [InlineData("White on Black", "WoB")]
    [InlineData("BoW", "Black on White")]
    [InlineData("Black on White", "BoW")]
    public void Search_ExpandsColorwaysBothWays(string text, string query)
    {
        var search = new CatalogSearch([]);
        search.CreateFilter(query)(search.Prepare(text)).ShouldBeTrue();
        search.CreateFilter("WoB")(search.Prepare("WOBkey")).ShouldBeFalse();
    }

    [Fact]
    public void Search_PreservesCompanyCaseRules_AndSearchesAllWordsNotesVariants()
    {
        var search = new CatalogService().Search;
        search.Prepare("DK").ShouldNotBe(search.Prepare("dk"));
        var description = new PartDescription(
            "Café WoB",
            "Factory",
            null,
            null,
            new KeycapPartDetails
            {
                Profile = "Cherry",
                Material = "ABS",
                Variants = ["Base", "ISO Kit"],
            }
        );
        var text = search.Build(description, "gift from Alex");
        search.CreateFilter("cafe white ISO alex ABS")(text).ShouldBeTrue();
        search.CreateFilter("cafe missing")(text).ShouldBeFalse();
        search.CreateFilter("")(text).ShouldBeTrue();
    }

    [Fact]
    public void PreparedFilters_CanBeReusedWithoutSharingQueriesOrLosingAliasCase()
    {
        var search = new CatalogService().Search;
        var upper = search.CreateFilter("DK WoB");
        var lower = search.CreateFilter("dk WoB");
        var rows = new[] { "DK White on Black", "dk WoB", "DK Violet", "DK WOBkey" }.Select(search.Prepare).ToArray();

        rows.Where(upper).ShouldBe(new[] { rows[0] });
        rows.Where(lower).ShouldBe(new[] { rows[1] });
        rows.Where(upper).ShouldBe(new[] { rows[0] });
        rows.Where(search.CreateFilter(" \t\n")).Count().ShouldBe(rows.Length);
    }

    [Theory]
    [InlineData("Key Kobo", "KEYKOBO")]
    [InlineData("Milkyway Keys", "MilkywayKeys")]
    [InlineData("Keyboard Science", "KeyboardScience")]
    [InlineData("KeyGeek", "keygeek")]
    [InlineData("G.Rule", "grule")]
    [InlineData("SP-Star", "SP Star")]
    [InlineData("Glove.Studio", "Glove Studio")]
    [InlineData("Wingtree / Lichicx", "Wingtree Lichicx")]
    [InlineData("Key\tKobo", "Key Kobo")]
    [InlineData("Caf\u00e9 Keys", "cafekeys")]
    public void Search_IgnoresCaseSpacingAndPunctuationWithoutExplicitAliases(string first, string second)
    {
        var search = new CatalogSearch([]);
        search.CreateFilter(first)(search.Prepare(second)).ShouldBeTrue();
        search.CreateFilter(second)(search.Prepare(first)).ShouldBeTrue();
    }

    [Fact]
    public void Search_KeepsQueryWordsIndependentAndExpandsRealAliasesBeforeCompacting()
    {
        var search = new CatalogService().Search;
        var description = new PartDescription(
            "Red Caf\u00e9",
            "KeyKobo",
            null,
            null,
            new KeycapPartDetails { Profile = "Cherry", Variants = ["ISO Kit"] }
        );
        var row = search.Build(description, "gift");
        search.CreateFilter("gift KKB cafe red ISOKit")(row).ShouldBeTrue();
        search.CreateFilter("cafe blue")(row).ShouldBeFalse();
        search.CreateFilter("Key Kobo")(row).ShouldBeTrue();
        search.CreateFilter("MilkywayKeys")(search.Prepare("Milkyway Keys")).ShouldBeTrue();
        search.CreateFilter("Milkyway Keys")(search.Prepare("MilkywayKeys")).ShouldBeTrue();
        search.CreateFilter("Dangkeeb")(search.Prepare("Dangkeebs")).ShouldBeTrue();
        search.CreateFilter("Dangkeeb")(search.Prepare("DK")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"profile\":\"Cherry\"}")]
    [InlineData("{\"switchType\":null,\"switchType\":\"linear\"}")]
    [InlineData("{\"variants\":null}")]
    [InlineData("{\"variants\":[\"a\",\"A\"]}")]
    public void UnreadableDetails_AreIsolatedAndOriginalBytesRemain(string json)
    {
        var row = new CollectionItem
        {
            PartKind = PartKinds.Switch,
            QuantityUnit = "Piece",
            DetailsJson = json,
        };
        var snapshot = PartDetailsCodec.Snapshot(row);
        snapshot.Details.ShouldBeNull();
        snapshot.DetailsError.ShouldNotBeNull();
        row.DetailsJson.ShouldBe(json);
    }

    [Fact]
    public void TypedDetails_RoundTripAndEnforceKindVersionUnit()
    {
        var details = new SwitchPartDetails { SwitchFamily = "EC/HE", Variants = ["45g", "55g"] };
        var json = PartDetailsCodec.Serialize(PartKinds.Switch, details, false);
        var parsed = PartDetailsCodec.Parse(PartKinds.Switch, 1, "Piece", json, false);
        PartDetailsCodec.Serialize(PartKinds.Switch, parsed, false).ShouldBe(json);
        Should.Throw<ArgumentException>(() => PartDetailsCodec.Parse(PartKinds.Switch, 2, "Piece", json, false));
        Should.Throw<ArgumentException>(() => PartDetailsCodec.Parse(PartKinds.Switch, 1, "Kit", json, false));
        Should.Throw<JsonException>(() => PartDetailsCodec.Parse(PartKinds.Keycap, 1, "Kit", json, false));
        Should.Throw<ArgumentException>(
            () => PartDetailsCodec.Serialize(PartKinds.Switch, new KeycapPartDetails(), false)
        );
        (
            (SwitchPartDetails)PartDetailsCodec.Parse(PartKinds.Switch, 1, "Piece", "{}", false)
        ).IsLowProfile.ShouldBeNull();
    }
}
