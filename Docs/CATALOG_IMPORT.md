# Switch and keycap catalog imports

The catalogs are offline JSON data maintained through a manual import and review process. This tooling does not change the app, database, device selections, installer, or release workflow.

## Files

| Path | Purpose |
|---|---|
| `Assets/Catalogs/switches.json` | Accepted switch names and optional metadata |
| `Assets/Catalogs/keycaps.json` | Accepted keycap set and add-on names with optional metadata |
| `Scripts/Catalogs/sources.json` | Fetch dates, Matrix commit, stable upstream identities, canonical IDs, source links, last observed fields, and collection variant notes |
| `Scripts/Catalogs/overrides.json` | Reviewed duplicate decisions, metadata corrections, and exclusions |
| `Scripts/Catalogs/aliases.json` | Company spellings, abbreviations, profiles, and the inference rules below |
| `Scripts/Catalogs/Aliases.ps1` | Applies those rules to display fields and duplicate comparisons while preserving IDs and source observations |
| `Scripts/Import-Catalogs.ps1` | Fetch, replay, validate, and promote entry point |
| `artifacts/catalog-import/<run>/` | Ignored source snapshots, candidate files, and review report. Keep the latest complete run for replay and `-Reuse`. |

Requires Windows PowerShell 5.1 or later with permission to run local scripts. No modules, packages, database, or API credentials are needed. Only `Fetch` uses the network.

## Catalog fields

Both catalogs contain `schemaVersion`, `catalogVersion`, `totalCount`, and an `entries` array. The importer calculates `totalCount` from the entries, and validation rejects a missing or mismatched count. Each entry requires a stable `id` and `name`.

| Optional field | Catalog | Meaning |
|---|---|---|
| `manufacturer` | Both | Explicitly identified or reliably inferred manufacturer |
| `brand` | Both | Selling or commissioning brand. Recorded even when it matches the manufacturer, so a missing brand always means the seller is unknown |
| `designer` | Both | Credited designer or collaboration |
| `switchType` | Switches | `linear`, `tactile`, or `clicky` |
| `profile` | Keycaps | Keycap profile |
| `material` | Keycaps | Keycap plastic |

- Unknown values are omitted. A store's own name is never recorded as a manufacturer.
- Metadata comes from explicit source labels and the reviewed inference rules below. Comparisons and marketing prose are never read for specifications.
- Names keep meaningful revisions, rounds, and switch weights and colors. Add-on kits for a named set have their own entries, and kit options inside a parent product stay attached to it.
- Keycap names show only their Latin form. Switch names and designer credits are kept as written.
- Accepted IDs stay fixed through name changes. Alternate names stay traceable in source observations and reviewed bindings. The only exception was a one-time cleanup on 2026-09-11 that renamed 57 IDs doubled by translated names, made before anything consumed catalog IDs.
- Catalog versions increase only when accepted entry content changes. Fetch timestamps and formatting never change a version.
- JSON files use two-space indentation and write non-ASCII characters literally.

## Update step by step

Run commands from the repository root, with a new run directory for each live fetch.

1. Fetch all sixteen sources and generate staged candidates:

   ```powershell
   .\Scripts\Import-Catalogs.ps1 -Action Fetch -Run artifacts/catalog-import/2026-10-01
   ```

2. Read `<run>/report.json` and the files under `<run>/candidate/`. The report lists additions, changes, explicit removals, possible duplicates, metadata conflicts, missing listings, rejected items, and omitted source values. Duplicates and conflicts return a failure exit status until resolved. Check collection `notes` in the candidate source mappings before merging similarly named specimens.
3. Resolve product decisions in `overrides.json` or shared naming rules in `aliases.json`, then replay the same cached inputs:

   ```powershell
   .\Scripts\Import-Catalogs.ps1 -Action Replay -Run artifacts/catalog-import/2026-10-01
   ```

4. Run the offline tests and inspect the candidate diff. `git diff --no-index` exits with code 1 when differences exist.

   ```powershell
   .\Scripts\Catalogs\Tests\Test-Catalogs.ps1
   git diff --no-index -- Assets/Catalogs artifacts/catalog-import/2026-10-01/candidate/Assets/Catalogs
   ```

5. Promote the reviewed candidate and validate the accepted files:

   ```powershell
   .\Scripts\Import-Catalogs.ps1 -Action Promote -Run artifacts/catalog-import/2026-10-01
   .\Scripts\Import-Catalogs.ps1 -Action Validate
   ```

6. Commit both catalogs, `sources.json`, `overrides.json`, and `aliases.json` together. The tool never commits, tags, or publishes. To roll back, revert that commit as a unit.

`Fetch` writes `fetch.json` only after every source completes. `Replay` verifies cached file hashes and pagination offline. A failed fetch stays inspectable but cannot be replayed, so retry with a new run directory. A snapshot missing any registered source cannot be replayed either, so adding an adapter needs a new fetch.

When only an adapter was added, seed the new run from the latest one instead of downloading everything again:

```powershell
.\Scripts\Import-Catalogs.ps1 -Action Fetch -Run artifacts/catalog-import/2026-10-01 -Reuse artifacts/catalog-import/shopify-2026-09-10
```

`-Reuse` verifies every checksum in the older run, copies those files, verifies them again, and downloads only the sources the older run lacks. Each carried source keeps its original `fetchedAt` and records a `reusedFrom` folder name. Sources no longer registered are dropped and named. `-Reuse` still refuses to write into an existing run directory.

`Promote` requires a clean report and matching input, candidate, and accepted-file hashes. Any edit to overrides, aliases, or accepted files needs a new replay, and a failed replay invalidates its earlier promotion receipt. Ordinary write failures roll back files already replaced. Replacing the three files is not a filesystem-wide transaction, so a process killed during promotion may need all three restored from Git and a replay. Keep this operation serialized.

The catalog scripts must stay pure ASCII. Windows PowerShell 5.1 reads a script without a byte order mark in the ANSI code page, where a dash's bytes become curly quotes that can break parsing or silently break a regex. Write such characters as `\u` escapes or build them with `[char]`. A test fails if a non-ASCII character appears.

## Alias rules

- `canonical` is the full company spelling used in manufacturer, brand, and designer fields. `aliases` match without regard to case. `exactAliases` match only the listed case, so `dk` means Divinikey, `DK` means Dangkeebs, and an unlisted `Dk` is left alone.
- `namePrefix` selects an abbreviated prefix for product names, and `namePrefixes` can set one per catalog. KeebsForAll uses `KFA` for switches and `kfaPBT` for keycaps. Without a preference, names use the canonical spelling.
- Product names use established abbreviations such as KKB, ePBT, MW, KBS, GOM, NK, SWG, WS, RAMA, PrimeKB, RK, DMK, JCS, C3, TUT, and VVD. Company fields keep the full spelling, and comparisons use the full identity.
- Rules standardize company fields, whole company names in designer credits, leading company names in titles, and explicit `x` collaborations. Model words, weights, revisions, rounds, collection numbers, and designer punctuation stay intact. Company credits drop trademark signs.
- Milkyway Keys, TutKeys, and XMI are canonical for their lines. `Milkway Keys`, `21KB`, and `Xiami` resolve to them. TX and Typeplus stay separate. Glove, Glove.Studio, and Glove Studio share one spelling, as do KFA, kfaPBT, and KeebsForAll.
- Durock and JWICK are separate brands with JWK as their OEM. The legacy `Durock/JWK` label records JWK as manufacturer without assigning a brand.
- Alias matches never merge products automatically, and a new listing matched through an alias still needs a reviewed binding. Original spellings stay in `sources.json`, and source identity digests ignore editable aliases.
- Alias edits need `Replay` before `Promote`, because the candidate receipt checks the alias file hash.

## Inferred fields

These rules fill a missing field and nothing else. They run once on the merged entry after every source has contributed, so any source value wins and a `null` entry override still removes what they add. Each was checked against the catalog for counterexamples or confirmed by the maintainer.

Keycaps:

- A name beginning with a recognized shape supplies that profile. The shapes are Cherry, CYL, MTNU, SA, DSA, DSS, DCS, KAT, KAM, KSA, XDA, MDA, MT3, OEM, HSA, and CRP-X, and CYL resolves to Cherry. A leading shape word names the profile, not the seller, so a Cherry-profile set is never recorded as sold by Cherry.
- A shape made by one company names it. DCS and DSS name Signature Plastics, KAT and KAM name Keyreative, MTNU names GMK, HSA names JTK, and CRP-X names Hammerworks. MTNU and CRP-X are PBT, and HSA is ABS. SA and DSA name nobody, because other factories make them too.
- A company declares what its sets share through `keycapDefaults`. `manufacturer` is `true` when it makes what it sells, or names the company that does. `profile` and `material` give its usual shape and plastic, and `unlessProfile` names a line that differs.
- GMK sets are Cherry and ABS, except MTNU sets, which record MTNU and PBT. KeyKobo sets are ABS. JTK sets are Cherry and ABS. XMI sets are Cherry and PBT. CRP sets are Cherry and PBT, made by Hammerworks.
- CreateKeebs, Domikey, EnjoyPBT, Gateron, GoMaster, Keyboard Science, Keyreative, Milkyway Keys, PBTfans, SoulCat, Swagkeys, TutKeys, Vividkey, and Wuque Studio make what they sell. NovelKeys and RAMA Works only sell, so they declare nothing. `PBTfans Thermal` keeps a reviewed omission of its disputed maker.

Switches:

- Thirteen factories carry `switchManufacturer`: Aflion, Cherry, Gateron, Grain Gold, Haimu, Jerrzi, JWK, Kailh, Keygeek, Outemu, SP-Star, Wingtree, and Yusya. A switch named for one of them records it as manufacturer. Brands that outsource, such as DareU, LEOBOG, Skyloong, Royal Kludge, and Feker, are left out.
- A single type word in a title or an explicit type label supplies the type. `Silent` and `Hall effect` establish nothing, because silent switches split 101 linear to 41 tactile.

Both catalogs:

- The company a name begins with is the selling brand. When it also makes the product, both fields record it, so a Domikey set shows Domikey twice.
- A declared profile or material must already be recognized, and a declared manufacturer must be canonical, or the alias map fails to load.

## Listing rules

The adapters apply these rules so a new fetch stays clean without repeating a review. Listings no rule can recognize get a reviewed exclusion.

Rejected:

- Prototypes, samples, trial sets, and open-box display units. `proto` matches only as a whole word, so Protozoa is unaffected.
- Aftermarket switch work, meaning broken-in, hand-lubed, modded, spring-swapped, and lubed-and-filmed listings. Factory-lubed and pre-lubed switches ship that way and stay.
- Accessories and assorted packs such as testers, pullers, deskmats, and stabilizers, plus configurator placeholders.
- Listings of several products, meaning mega listings, kit collections that gather one kind of kit from many sets, and leftovers from several sets.
- Artisans. A keycap listing is an artisan when its title says so, when its title or vendor names Salvun, or when its title ends in a single metal, machined, or brass keycap. HIBI also sells full sets, so its name alone rejects nothing. Keygem's `Artisan` tag is ignored because it marks ordinary sets too.
- Switches and faceplates listed in a keycap feed.

Renamed instead of rejected:

- Dry, lubed, unlubed, factory-lubed, and pre-lubed wording is dropped from a switch name, so both purchase options land on one entry. NovelKeys' Dry line keeps the word, because it is the model there.
- A trailing `B-Stock` is dropped, because B-stock units are the product with cosmetic flaws.
- Keycap titles take the catalog's spelling. `GMK X (CYL)` and a leading `CYL X` become `GMK CYL X`, and a trailing `Bundle` is dropped. A dash after a known company separates brand from set, while any other dash is part of the name, as in `GMK Beloved - KA2017 Revival`.
- Keycap names drop Chinese, Japanese, or Korean text wherever a Latin form remains, along with a round the translation repeats. A Latin gloss in brackets becomes the name, as in `DMK In Former Days`.

Order matters in `Convert-CatalogProduct`. Modification wording is read before lube wording is removed, and the artisan check reads the raw title because cleanup drops the singular keycap that marks one. The switch rules run again on composed variant names, because stores put that wording in option labels.

Each Shopify store fills `vendor` differently. SwitchOddities, UniKeys, Divinikey, and KBDfans read it as the selling brand. Daily Clack files each set under its maker, so its vendor supplies the manufacturer when that is a known maker. NovelKeys, CannonKeys, Omnitype, Keygem, Dangkeebs, and Swagkeys hold the store's name, stock status, or a mix, so theirs is ignored.

## Matching and duplicates

Matching only decides which new listings are flagged as possible duplicates. It never merges, and it never touches source identities, so existing bindings do not move.

- Case, punctuation, accents, trademark signs, and translated text are ignored. `&`, `and`, and a slash are the same joiner, and `GMK CYL X` matches `GMK X`.
- R2, V2, V2.0, a bare 2, and 2.0 all mean round 2, and R3.1 matches V3.1. R1 and V1 match the unnumbered name, because first rounds are usually unnumbered. Point versions such as V3.1 and V3.2 stay apart, and only digits 2 to 9 count as a bare round, so `GMK Extended 2048` is not one.
- The merge compares fields case-sensitively and treats spellings of one value as agreement, keeping the accepted spelling. A genuine difference is reported as a conflict.

A match is only a lead. Confirm a keycap duplicate with a shared group buy date or geekhack thread between KeycapLendar and Matrix, a shared designer, or a retailer page created when that round went on sale. A page created long before its round proves nothing, because some vendors reuse one page for every round. For switches an unnumbered name is not reliably the first version, so pairs such as `Akko Pink` and `Akko Pink V1` stay separate until reviewed.

## Review decisions

The override document has three maps:

```json
{
  "schemaVersion": 1,
  "bindings": {
    "kbdfans:12345": "gmk-example-r2"
  },
  "entries": {
    "keycaps/gmk-example-r2": {
      "name": "GMK Example R2",
      "designer": "Credited Designer"
    }
  },
  "excluded": {
    "divinikey:67890": "Non-keycap accessory"
  }
}
```

- `bindings` maps an upstream identity to a canonical ID. Confirm matching rounds and variants before merging, and assign distinct IDs when similar names differ. Moving every binding away from an entry retires it, and the report records the removal. Move or remove any overrides for the retired ID.
- `entries` pins reviewed metadata. Set a field to `null` to omit a value that cannot be established. Never change `id` here. When merged listings disagree on a name or credit, pin the accepted value.
- `excluded` rejects a source listing with a reason. Its entry is removed only when no other binding remains. Remove any entry override for that ID too. Stock availability is never a reason.
- Keep ambiguous variants separate. A collection-number suffix can distinguish specimens, but it is an import label rather than an official revision.
- A listing that disappears, or a new automatic filter, never deletes accepted history. Existing metadata stays when a source stops supplying it. A drop below half a source's prior bindings, for sources with at least 20, aborts the import for investigation.
- Unrecognized multi-option switch listings stop for review rather than discarding variants. Add an adapter rule with a fixture test when a source introduces a new option format.

## Sources

Shopify links start at page 1. Increment `page` until an empty `products` array returns.

| Source | Coverage | Format |
|---|---|---|
| [SwitchOddities JSON](https://switchoddities.com/collections/switch-samples/products.json?limit=250&page=1) | Switch sample listings, including some mouse switches | Shopify product JSON |
| [ThereminGoat collection XLSX](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) | Broad switch collection with historical models and variants | Public workbook with name, type, manufacturer, and variant-note cells |
| [ThereminGoat switch scores CSV](https://raw.githubusercontent.com/ThereminGoat/switch-scores/refs/heads/master/1-Composite%20Overall%20Total%20Score%20Sheet.csv) | Scored switches with manufacturer and type columns | Six stacked ranking tables. Only the first is read. |
| [UniKeys JSON](https://unikeyboards.com/collections/keyboard-switches/products.json?limit=250&page=1) | Named switches with manufacturer, designer, and type labels | Shopify product JSON. Weights and modifications are kept, pack sizes removed. |
| [Matrix JSON file index](https://api.github.com/repos/matrixzj/matrixzj.github.io/contents/docs/gmk-keycaps?ref=c3b59ab9c059c5976b5ecc0789f6df97960614c6) | Historical GMK sets and add-ons | GitHub directory listing of per-set Markdown documents at the imported commit |
| [Divinikey JSON](https://divinikey.com/collections/keycap-sets/products.json?limit=250&page=1) | Keycap sets and add-ons | Shopify product JSON |
| [KBDfans JSON](https://kbdfans.com/collections/keycaps/products.json?limit=250&page=1) | Keycap sets and add-ons | Shopify product JSON |
| [DCS Wiki data bundle](https://dcs.wiki/_next/static/chunks/911-9f506cad5fbc6f41.js) | DCS sets and add-on kits | JSON array inside a JavaScript bundle, rediscovered from the catalog page on each fetch |
| [KeycapLendar Firestore JSON](https://firestore.googleapis.com/v1/projects/keycaplendar/databases/%28default%29/documents/keysets?pageSize=300) | Past, current, and upcoming sets and add-ons | Public collection. Follow `nextPageToken` until absent. |
| [NovelKeys JSON](https://novelkeys.com/collections/keycaps/products.json?limit=250&page=1) | GMK, KAM, and SA sets | Shopify product JSON |
| [CannonKeys JSON](https://cannonkeys.com/collections/keycaps/products.json?limit=250&page=1) | GMK, NicePBT, and PBS sets | Shopify product JSON |
| [Daily Clack JSON](https://dailyclack.com/collections/keycaps/products.json?limit=250&page=1) | Sets filed under their maker | Shopify product JSON |
| [Omnitype JSON](https://omnitype.com/collections/keycaps/products.json?limit=250&page=1) | GMK sets and bundles | Shopify product JSON |
| [Keygem JSON](https://keygem.com/collections/keycaps/products.json?limit=250&page=1) | KAT, MW, PBTfans, and studio sets | Shopify product JSON |
| [Dangkeebs JSON](https://dangkeebs.com/collections/keycaps/products.json?limit=250&page=1) | KAM, Keyboard Science, and Qtuo Studio sets | Shopify product JSON |
| [Swagkeys JSON](https://swagkeys.com/collections/keycaps/products.json?limit=250&page=1) | SW and CRP sets | Shopify product JSON |

Coverage is not exhaustive. Retailers remove discontinued products, Matrix's GMK index ends at 2024, and DCS Wiki omits some private runs. KeycapLendar includes interest checks, so a listing does not prove a product shipped. Canceled KeycapLendar sets are excluded by review. Blank metadata is intentional.

### Adapter notes

- KeycapLendar's document ID is the stable identity. Names combine its category and colorway. Its `profile` field mixes makers and shapes, so only recognized shapes and explicit material suffixes are read. Fetch and replay verify the whole token chain, typed fields, and hashes. The adapter reads the public collection behind the website. The supported API needs a key from the maintainer, and the importer never attempts authenticated access.
- DCS Wiki has no JSON endpoint. Each fetch finds the current script URLs on the [catalog page](https://dcs.wiki/keycaps), locates one `JSON.parse` array, and decodes it without running JavaScript. Replay checks that the bundle belongs to the cached page. Cherry and Gorton legend styles are not profiles.
- ThereminGoat's workbook is read with .NET ZIP and XML support, without Excel. Formulas in import cells, external entities, and changed schemas stop the import. Source identities use collection numbers, and the repeated number 3215 is split with a name digest.
- The score sheet repeats its composite table five more times by switch type, so only the first table is read, and it ends at `AVERAGE OF ALL`. Ranks change between publications, so identities are a digest of the name. Repeated rows that agree collapse with a warning, and rows that disagree stop for review. `Silent Linear` and `Silent Tactile` map to linear and tactile.
- UniKeys packaging-only options share one identity, while weight and modification options keep their own upstream variant IDs.
- The workbook and score sheet omit and report unknown and question-marked manufacturers.

### Candidate sources

Checked on 2026-09-09. None is an adapter yet.

| Source | Data | Remaining work |
|---|---|---|
| [Cherry XTRFY](https://cherryxtrfy.com/keyboard-switches) | JSON split across hidden page inputs for 89 switches | Decode the inputs without running page scripts. Do not assign Cherry to the whole collection. |
| [Gateron store JSON](https://www.gateron.co/products.json?limit=250&page=1) | 35 Shopify products | Some specification tables exist only on product pages |
| [Milktooth](https://milktooth.com/) | Embedded page data with switch brands and types | Homepage arrays are partial, and brand must stay separate from manufacturer |
| [KeebFinder](https://keeb-finder.com/switches) | Embedded page data for 48 switches | Verify pagination and follow specification links |
| [KBD.news](https://kbd.news/switch/) | HTML table of 500 switches | Useful for type cross-checks, no JSON feed |
| [Keygeek](https://www.keygeek.cn/products_16/) | Manufacturer listing | No feed. UniKeys already carries much of it. |

A 2026-09-09 comparison of name matches found no source redundant. XMI has only one entry, because KeycapLendar lists one XMI set and no other active source carries the line. XMI sells through Chinese platforms without reachable feeds, and keycapsets.com loads its data client side, so closing that gap needs a new feed or a manual list.

### Source notices

- [Matrix's license](https://github.com/matrixzj/matrixzj.github.io/blob/master/LICENSE.txt) is CC BY-NC-ND 4.0, not an unrestricted redistribution license. The import extracts factual names and credits, not pictures or prose. Preserve attribution and assess permission before distributing an adapted compilation.
- [Divinikey's terms](https://divinikey.com/policies/terms-of-service) and [KBDfans' terms](https://kbdfans.com/policies/terms-of-service) restrict crawling. A reachable endpoint does not grant bulk access or redistribution. Check [SwitchOddities' terms](https://switchoddities.com/policies/terms-of-service) and the other stores' terms for the intended use too.
- [DCS Wiki's notice](https://dcs.wiki/about) describes a noncommercial archive with no data-reuse license. Preserve its attribution.
- Raw responses stay local under ignored `artifacts/`. Catalog data is not relicensed under the application's code license, and fetching by end users is out of scope.

## History

The switch catalog holds 5,411 entries at version 15, and the keycap catalog 2,658 at version 20. Switch coverage is 69 percent manufacturer, 43 percent brand, and 82 percent type. Keycap coverage is 71 percent manufacturer, 58 percent brand, 71 percent profile, and 55 percent material.

**Sources.** Early imports used SwitchOddities, Matrix, Divinikey, KBDfans, and DCS Wiki. ThereminGoat's workbook and UniKeys expanded switches, KeycapLendar expanded keycaps, the score sheet followed, and seven retailer keycap feeds made sixteen adapters on 2026-09-10.

**Removed.** Every removal has a reviewed exclusion, so it survives later imports.

- 122 prototypes that no retailer ever listed, 95 samples, and 23 aftermarket modifications.
- 82 canceled keycap sets, 15 switches whose names carried question marks about their identity, and one emoji-named GMK entry.
- Cherry listings with no pin count where a pin-specific entry exists, including `Cherry Brown`, and nameplate specimens that duplicated a plain entry. MX1A listings are Hyperglide.
- 36 artisans and 23 other listings that are not keycap sets, such as kit collections, leftover sales, faceplates, a keyboard, and single novelty keys.

**Merged.**

- 24 spelling-error pairs revealed by alias standardization and the score sheet.
- 91 lube-marked switch listings into 60 clean products. Three switches known only as modified specimens kept one clean entry.
- 65 same-release keycap pairs. Most were a KeycapLendar first round and Matrix's `R1`, a version or year standing for a round as in `GMK Dracula V2.0` and `R2`, or a Matrix title worded differently. Twelve retailer listings moved to the round that was on sale when their page opened.

**Kept apart on purpose.**

- Olivia and Olivia++, because plus signs are meaningful.
- `Cherry Blossom` by JWK and `Cherry MX Blossom` by Cherry.
- KTT Phalaenopsis, Skyloong Chocolate Rose, and other specimens without enough variant detail.
- Retailer pages reused across rounds, such as `PBTfans Spark Light R2` and `PBTfans X-ray R3`.
- Switch pairs where only one side is numbered V1.

**Notable metadata decisions.**

- WS Aurora keeps Haimu, Keyspensory Haze uses Aflion, and Tecsee's own pages set Purple Panda as tactile and Carrot as linear.
- XCJZ Jerrzi Lotus Stem, two Akko models, UniKeys' `MDD` labels, and `PBTfans Thermal` keep no manufacturer, because their sources disagree or only speculate.
- `Gateron Nightingale` keeps no type, because its two specimens disagree.
- `HMX Snow Crash (Overlubed Batch)` stays, because an over-lubed factory batch is a production note.

**Open for review.**

- `GMK Frost Witch r2` credits Adam from KeycapLendar while Matrix credits Krelbit.
- `PBTfans Purpolch` and `PBTfans Purpolch R4` may be one release, but KeycapLendar links a different product.
- Switch pairs such as `Akko Pink` and `Akko Pink V1` need evidence before merging.
