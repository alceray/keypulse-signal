# Switch and keycap catalog imports

The catalogs are offline JSON data maintained through a manual import and review process. This tooling does not change the app, database, device selections, installer, or release workflow.

## Files

| Path | Purpose |
|---|---|
| `Assets/Catalogs/switches.json` | Accepted switch names and optional metadata |
| `Assets/Catalogs/keycaps.json` | Accepted keycap set and add-on names with optional metadata |
| `Scripts/Catalogs/sources.json` | Fetch dates, Matrix commit, stable upstream identities, canonical IDs, source links, last observed fields, and collection variant notes |
| `Scripts/Catalogs/overrides.json` | Reviewed duplicate decisions, metadata corrections, and exclusions |
| `Scripts/Catalogs/keycap-enrichment.json` | Field-level source links and rationale for reviewed keycap metadata |
| `Scripts/Catalogs/keycap-release-review.json` | Release eligibility decisions, evidence links, excluded source identities, and unresolved cases |
| `Scripts/Catalogs/aliases.json` | Company spellings, abbreviations, profiles, and the inference rules below |
| `Scripts/Catalogs/Aliases.ps1` | Applies those rules to display fields and duplicate comparisons while preserving IDs and source observations |
| `Scripts/Import-Catalogs.ps1` | Fetch, replay, validate, and promote entry point |
| `artifacts/catalog-import/<run>/` | Ignored source snapshots, candidate files, and review report. Keep the latest complete run for replay and `-Reuse`. |

Requires Windows PowerShell 5.1 or later with permission to run local scripts. No modules, packages, database, or API credentials are needed. Only `Fetch` uses the network.

## Catalog fields

Both catalogs contain `schemaVersion`, `catalogVersion`, `totalCount`, and an `entries` array. The importer calculates `totalCount` from the entries, and validation rejects a missing or mismatched count. Each entry requires a stable `id` and `name`.

| Optional field | Catalog | Meaning |
|---|---|---|
| `manufacturer` | Both | Production credit. For keycaps, the community-known production name is sufficient; it need not identify the legal factory operator. Switches retain their separately attributed manufacturer |
| `brand` | Both | Product or commissioning brand, recorded even when it matches the manufacturer. This is not a retailer/vendor field |
| `designer` | Both | Credited designer or collaboration |
| `switchType` | Switches | `linear`, `tactile`, or `clicky` |
| `profile` | Keycaps | Keycap profile |
| `material` | Keycaps | Keycap material; `ABS/PBT` records both plastics, whether a blend or different keys within the set |

- Unknown values are omitted. A hosting store is not evidence of brand or manufacturer. Documented house production lines such as NicePBT, CannonCaps, and Drop can supply community manufacturer names.
- Metadata comes from explicit labels, reviewed product/project specifications, and the inference rules below. Comparisons, packaging, optional artisans, and tentative production options do not supply specifications for the set.
- Names keep meaningful revisions, rounds, and switch weights and colors. Add-on kits for a named set have their own entries, and kit options inside a parent product stay attached to it.
- Keycap releases qualify once GB or in-stock orders open. Exclude IC-only proposals, future openings, cancelled releases, and failed-MOQ attempts. Verify the specific maker, profile, round, and kit: a cancelled add-on does not disqualify a produced base set, and a later successful sale may qualify after an earlier failed attempt.
- Keycap names show only their Latin form. Switch names and designer credits are kept as written.
- Accepted IDs stay fixed through name changes. Alternate names stay traceable in source observations and reviewed bindings. The only exception was a one-time cleanup on 2026-09-11 that renamed 57 IDs doubled by translated names, made before anything consumed catalog IDs.
- Catalog versions increase only when accepted entry content changes. Fetch timestamps and formatting never change a version.
- JSON files use two-space indentation and write non-ASCII characters literally.

## Update step by step

Run commands from the repository root, with a new run directory for each live fetch.

1. Fetch all twenty-six sources and generate staged candidates:

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
.\Scripts\Import-Catalogs.ps1 -Action Fetch -Run artifacts/catalog-import/2026-10-01 -Reuse artifacts/catalog-import/stores-2026-09-12
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

- A name beginning with a recognized shape supplies that profile. The shapes are Cherry, CYL, MTNU, SA, DSA, DSS, DCS, KAT, KAM, KSA, XDA, MDA, MT3, OEM, HSA, CRP-X, OSA, LSA, ASA, MOA, MOG, Cubic, DDA, and ADA, and CYL resolves to Cherry. A leading shape word names the profile, not the seller, so a Cherry-profile set is never recorded as sold by Cherry.
- A shape made by one company names it. DCS and DSS name Signature Plastics, KAT and KAM name Keyreative, MTNU names GMK, HSA names JTK, and CRP-X names Hammerworks. MTNU and CRP-X are PBT, and HSA is ABS. SA and DSA name nobody, because other factories make them too.
- A company declares what its sets share through `keycapDefaults`. `manufacturer` is `true` when it makes what it sells, or names the company that does. `profile` and `material` give its usual shape and plastic, and `unlessProfile` names a line that differs.
- GMK sets are Cherry and ABS, except MTNU sets, which record MTNU and PBT. KeyKobo sets are ABS. JTK sets are Cherry and ABS. XMI sets are Cherry and PBT. CRP sets are Cherry and PBT, made by Hammerworks.
- CreateKeebs, Domikey, EnjoyPBT, Gateron, GoMaster, Keyboard Science, Keyreative, Milkyway Keys, PBTfans, SoulCat, Swagkeys, TutKeys, Vividkey, and Wuque Studio provide recognized production names for their lines. NovelKeys and RAMA Works have no blanket manufacturer defaults. `PBTfans Thermal` uses PBTfans as its community production name.
- CRP products use brand CRP and manufacturer Hammerworks, including Hammerworks-prefixed titles. NicePBT and CannonCaps use CannonKeys as the brand and their production-line names as manufacturer. Entry overrides preserve these distinctions; designer credits remain independent.

Switches:

- Thirteen factories carry `switchManufacturer`: Aflion, Cherry, Gateron, Grain Gold, Haimu, Jerrzi, JWK, Kailh, Keygeek, Outemu, SP-Star, Wingtree, and Yusya. A switch named for one of them records it as manufacturer. Brands that outsource, such as DareU, LEOBOG, Skyloong, Royal Kludge, and Feker, are left out.
- A single type word in a title or an explicit type label supplies the type. `Silent` and `Hall effect` establish nothing, because silent switches split 101 linear to 41 tactile.

Both catalogs:

- The recognized company a name begins with supplies its product brand unless overridden. Brand and manufacturer may match, so a Domikey set can show Domikey twice.
- A declared profile or material must already be recognized, and a declared manufacturer must be canonical, or the alias map fails to load.

## Listing rules

The adapters apply these rules so a new fetch stays clean without repeating a review. Listings no rule can recognize get a reviewed exclusion.

Rejected:

- Prototypes, samples, trial sets, and open-box display units. `proto` matches only as a whole word, so Protozoa is unaffected.
- Aftermarket switch work, meaning broken-in, hand-lubed, modded, spring-swapped, and lubed-and-filmed listings. Factory-lubed and pre-lubed switches ship that way and stay.
- Accessories and assorted packs such as testers, pullers, deskmats, and stabilizers, plus configurator placeholders.
- Listings of several products, meaning mega listings, kit collections that gather one kind of kit from many sets, and leftovers from several sets.
- Artisans. A keycap listing is an artisan when its title or product type says so, when its title or vendor names Salvun, or when its title ends in a single metal, machined, or brass keycap. HIBI also sells full sets, so its name alone rejects nothing. Keygem's `Artisan` tag is ignored because it marks ordinary sets too.
- Switches and faceplates listed in a keycap feed.
- From the stores that write specifications into titles, packs of fewer than 20 keys, a single keycap or spacebar, rubber gaming keys, and listings whose title names no set once the specifications are gone. The last kind sells several colorways under one generic title.

Renamed instead of rejected:

- Dry, lubed, unlubed, factory-lubed, and pre-lubed wording is dropped from a switch name, so both purchase options land on one entry. NovelKeys' Dry line keeps the word, because it is the model there.
- A trailing `B-Stock` is dropped, because B-stock units are the product with cosmetic flaws.
- Keycap titles take the catalog's spelling. `GMK X (CYL)` and a leading `CYL X` become `GMK CYL X`, and a trailing `Bundle` is dropped. A dash after a known company separates brand from set, while any other dash is part of the name, as in `GMK Beloved - KA2017 Revival`.
- Keycap names drop Chinese, Japanese, or Korean text wherever a Latin form remains, along with a round the translation repeats. A Latin gloss in brackets becomes the name, as in `DMK In Former Days`.
- MechanicalKeyboards, Akko EU, Epomaker, Keychron, Glorious, and LumeKeebs write the key count, profile, printing process, plastic, and words like `Keycap Set` into their titles. These leave the name, and a stated profile and plastic fill those fields. A shape word or `PC` counts only beside other specifications, so `SA Solarized` and `Glorious PC` keep them. A trailing `Base` or `Base Kit` is dropped because a base kit is the set itself, and a leading layout moves behind the name, as in `Keychron Developer ISO`.
- The single-brand stores leave their name out of most titles, so their brand is added to the name and the brand field. A `KBDfans` prefix before `PBTfans` is dropped because PBTfans is its line.
- A designer credit ends before store copy that runs on into the story behind the set, as in `Wynects and inspired by manta rays`.

Order matters in `Convert-CatalogProduct`. Modification wording is read before lube wording is removed, and the artisan check reads the raw title because cleanup drops the singular keycap that marks one. The switch rules run again on composed variant names, because stores put that wording in option labels.

Each Shopify store fills `vendor` differently. SwitchOddities, UniKeys, Divinikey, KBDfans, and MechanicalKeyboards read it as the selling brand. Daily Clack files each set under its maker, so its vendor supplies the manufacturer when that is a known maker. NovelKeys, CannonKeys, Omnitype, Keygem, Dangkeebs, and Swagkeys hold the store's name, stock status, or a mix, so theirs is ignored. Akko EU, Epomaker, Keychron, Glorious, and the LumeKeebs JKDK collection sell only their own brand, which the adapter records instead of the vendor.

## Matching and duplicates

Matching only decides which new listings are flagged as possible duplicates. It never merges, and it never touches source identities, so existing bindings do not move.

- Case, punctuation, accents, trademark signs, and translated text are ignored. `&`, `and`, and a slash are the same joiner, and `GMK CYL X` matches `GMK X`.
- WoB matches White on Black, and BoW matches Black on White, including hyphenated names and repeated abbreviations in parentheses. Display names stay unchanged; makers, profiles, releases, and kits still need review.
- R2, V2, V2.0, a bare 2, and 2.0 all mean round 2, and R3.1 matches V3.1. R1 and V1 match the unnumbered name, because first rounds are usually unnumbered. Point versions such as V3.1 and V3.2 stay apart, and only digits 2 to 9 count as a bare round, so `GMK Extended 2048` is not one.
- The merge compares fields case-sensitively and treats spellings of one value as agreement, keeping the accepted spelling. A genuine difference is reported as a conflict.

Future catalog search should apply these colorway equivalents to both queries and names so either spelling finds the same entry; the current rule runs in the importer only.

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
- `excluded` rejects a source listing with a reason. Its entry is removed only when no other binding remains. Remove any entry override for that ID too. A sold-out eligible release remains in the catalog; a release whose orders never opened does not qualify.
- Keep ambiguous variants separate. A collection-number suffix can distinguish specimens, but it is an import label rather than an official revision.
- A listing that disappears, or a new automatic filter, never deletes accepted history. Existing metadata stays when a source stops supplying it. A drop below half a source's prior bindings, for sources with at least 20, aborts the import for investigation.
- Unrecognized multi-option switch listings stop for review rather than discarding variants. Add an adapter rule with a fixture test when a source introduces a new option format.

Reviewed keycap enrichment and corrections are pinned in `overrides.json`, with source links and rationale in `keycap-enrichment.json`. These are entry-specific decisions, not new global inference rules; unresolved fields remain omitted.

Release exclusions are also pinned in `overrides.json`, with evidence in `keycap-release-review.json`. Future openings require a new review confirming that orders actually opened before lifting the exclusions; a calendar date alone does not restore them. Unavailable or unrelated links remain unresolved, and non-GMK entries without usable IC/GB details are left unchanged.

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
| [MechanicalKeyboards JSON](https://mechanicalkeyboards.com/collections/keycaps/products.json?limit=250&page=1) | Tai-Hao, Ducky, Varmilo, Traitors, KBParadise, GMK, PBTfans, and other brands | Shopify product JSON. Its product type names the profile and marks artisans. |
| [Akko EU JSON](https://akkogear.eu/collections/keycap/products.json?limit=250&page=1) | Akko sets and layout kits | Shopify product JSON |
| [Epomaker JSON](https://epomaker.com/collections/keycaps/products.json?limit=250&page=1) | Epomaker sets | Shopify product JSON |
| [Keychron JSON](https://www.keychron.com/collections/all-keycaps/products.json?limit=250&page=1) | Keychron sets | Shopify product JSON |
| [Glorious JSON](https://www.gloriousgaming.com/collections/keycaps/products.json?limit=250&page=1) | GPBT sets | Shopify product JSON |
| [LumeKeebs JKDK JSON](https://lumekeebs.com/collections/jkdk/products.json?limit=250&page=1) | JKDK sets | Shopify product JSON |
| [Osume JSON](https://osume.com/collections/all-keycaps/products.json?limit=250&page=1) | Osume sets and named novelty, accent, and extras kits | Shopify product JSON |
| [KeebsForAll JSON](https://keebsforall.com/collections/keycap-sets-for-mechanical-keyboards/products.json?limit=250&page=1) | kfaPBT, JC Studio, and third-party sets | Shopify product JSON |
| [Prototypist JSON](https://prototypist.net/collections/in-stock-keycap-sets/products.json?limit=250&page=1) | In-stock collection, including sold-out listings, newer rounds, and smaller makers | Shopify product JSON |
| [Mode Designs JSON](https://modedesigns.com/collections/keycaps/products.json?limit=250&page=1) | Mode sets | Shopify product JSON |

Coverage is not exhaustive. Retailers remove discontinued products, Matrix's GMK index ends at 2024, and DCS Wiki omits some private runs. KeycapLendar and retailer collections include interest checks; a listing or dated GB does not establish an eligible release. Reviewed IC-only, unopened, cancelled, and failed-MOQ releases are excluded across their source bindings. Blank metadata is intentional.

### Adapter notes

- KeycapLendar's document ID is the stable identity. Names combine its category and colorway. Its `profile` field mixes makers and shapes, so only recognized shapes and explicit material suffixes are read. Fetch and replay verify the whole token chain, typed fields, and hashes. The adapter reads the public collection behind the website. The supported API needs a key from the maintainer, and the importer never attempts authenticated access.
- DCS Wiki has no JSON endpoint. Each fetch finds the current script URLs on the [catalog page](https://dcs.wiki/keycaps), locates one `JSON.parse` array, and decodes it without running JavaScript. Replay checks that the bundle belongs to the cached page. Cherry and Gorton legend styles are not profiles.
- ThereminGoat's workbook is read with .NET ZIP and XML support, without Excel. Formulas in import cells, external entities, and changed schemas stop the import. Source identities use collection numbers, and the repeated number 3215 is split with a name digest.
- The score sheet repeats its composite table five more times by switch type, so only the first table is read, and it ends at `AVERAGE OF ALL`. Ranks change between publications, so identities are a digest of the name. Repeated rows that agree collapse with a warning, and rows that disagree stop for review. `Silent Linear` and `Silent Tactile` map to linear and tactile.
- UniKeys packaging-only options share one identity, while weight and modification options keep their own upstream variant IDs.
- The workbook and score sheet omit and report unknown and question-marked manufacturers.
- MechanicalKeyboards and Akko EU throttle quick repeated requests. A fetch that stops on a long retry delay succeeds when retried a few minutes later.
- Osume and Mode supply their own product brand; KeebsForAll and Prototypist vendor fields do not. Named add-ons stay separate, and kit options remain in source notes. Prototypist stock prefixes and optional deskmat wording are removed; deskmat-only and mixed-set collections are excluded.
- Osume profile and material enrichment comes from individual product pages because the feed omits technical sections. Marshmallow stays distinct from Cherry. Two Marshmallow extras kits have conflicting profile labels and retain no profile. Mode's five sets specify Cherry and an ABS/PBT blend, with no separate manufacturer established.

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

#### Additional keycap sources

Shortlist of remaining sources worth adding or reviewing. Estimates were made against keycap catalog version 23 (3,231 entries). Integrated sources are listed above. **Listings** are observed product records; **new** ranges are planning estimates after allowing for accessories, equivalent layouts, and existing entries. They are not an import audit and must not be summed across overlapping sources.

| Source | Coverage benefit | Listings → estimated new | Priority / remaining work |
|---|---|---|---|
| [MelGeek](https://www.melgeek.com/collections/keycaps) | MDA/MLG gaps and MCR/MDA credits | 9 → 3–4 | **Worth it; small.** Big Bone, Label, and Pixel Xmas are leads; Horseman, Vision, and Dawn already exist with missing credits. Pixel Xmas is Pixel-only; do not assume MX compatibility. |
| [MechKeys IDOBAO](https://mechkeys.com/collections/idobao) | IDOBAO | 22 → 8–11 | **Worth it.** 11 titles identify keycap products. Exclude keyboards/switches/pullers; vendor `mechkeysshop` is the seller. |
| [HK Gaming](https://hkgaming.com/collections/keycaps) | HK Gaming | 2; 25 colorways → 15–25 | **Worth it; curated.** One full-set product holds 25 named colorways; the other is a rubberized kit. Check identities before splitting variants. |
| [Kinetic Labs](https://kineticlabs.com/keycaps) | Kinetic Labs / PolyCaps | 41 sets; 21 own → 18–25 | **Worth it.** Focus on its own range; Keychron and other third-party sets overlap active sources. HTML extraction needed. |
| [NuPhy](https://nuphy.com/collections/keycaps?page=1) | NuPhy | 38 indexed → 25–35 | **Worth it once accessible.** Indexed count only; live HTML/feed requests returned 403/429. |
| [KPrepublic](https://kprepublic.com/collections/keycaps) | Broad remaining set coverage | 511 → 60–140 | **Worth a later pass.** Lower-confidence estimate after the MK import. Many artisans/generic titles and existing Domikey/Tai-Hao sets; 510 products use the store vendor. |

Small gaps worth filling manually:

| Source | Coverage | Estimated new | Remaining work |
|---|---|---|---|
| [Pantheonkeys Shenpo Terminal](https://pantheonkeys.com/products/shenpo-terminal-pbt-dye-sub-keycap-set) | Shenpo | 1 | Explicit manufacturer credit; no dedicated one-product adapter needed. |
| [Angry Miao Glacier](https://store.angrymiao.com/collections/angry-miao-glacier-keycap-set) | Angry Miao | 0–1 | Standard Glacier is covered by Prototypist; check whether Dark Glacier needs a separate colorway entry. |
| [qPBT Terminal designer](https://buttondown.com/MVKB/archive/qpbt-terminal-in-stock-this-week-many-other/) | qPBT / EqualPBT | 1 | Designer identifies qPBT as **EqualPBT**, not Quality PBT; revisit Daily Clack/Keygem coverage. |

**Priority:** MelGeek, IDOBAO, and Kinetic Labs for focused additions; KPrepublic for a broader later pass. Keep source/vendor separate from brand and community-known manufacturer.

### Source notices

- [Matrix's license](https://github.com/matrixzj/matrixzj.github.io/blob/master/LICENSE.txt) is CC BY-NC-ND 4.0, not an unrestricted redistribution license. The import extracts factual names and credits, not pictures or prose. Preserve attribution and assess permission before distributing an adapted compilation.
- [Divinikey's terms](https://divinikey.com/policies/terms-of-service) and [KBDfans' terms](https://kbdfans.com/policies/terms-of-service) restrict crawling. A reachable endpoint does not grant bulk access or redistribution. Check [SwitchOddities' terms](https://switchoddities.com/policies/terms-of-service) and the other stores' terms for the intended use too.
- [DCS Wiki's notice](https://dcs.wiki/about) describes a noncommercial archive with no data-reuse license. Preserve its attribution.
- Raw responses stay local under ignored `artifacts/`. Catalog data is not relicensed under the application's code license, and fetching by end users is out of scope.

## History

The switch catalog holds 5,411 entries at version 16, and the keycap catalog 2,745 at version 29. Switch coverage is 69 percent manufacturer, 43 percent brand, and 82 percent type. Keycap coverage is 71 percent manufacturer, 90 percent brand, 85 percent profile, and 80 percent material.

**Sources.** Early imports used SwitchOddities, Matrix, Divinikey, KBDfans, and DCS Wiki. ThereminGoat's workbook and UniKeys expanded switches, KeycapLendar expanded keycaps, the score sheet followed, and seven retailer keycap feeds made sixteen adapters on 2026-09-10. Six more stores on 2026-09-12 added 573 keycap entries, mostly Keychron, Tai-Hao, Akko, Ducky, PBTfans, and Glorious sets. Osume, KeebsForAll, Prototypist, and Mode bring the adapter count to 26, adding 124 sets and kits and enriching 44 entries.

**Removed.** Every removal has a reviewed exclusion, so it survives later imports.

- 122 prototypes that no retailer ever listed, 95 samples, and 23 aftermarket modifications.
- 82 canceled keycap sets, 15 switches whose names carried question marks about their identity, and one emoji-named GMK entry.
- Release review removed 604 keycap entries that never reached an opened order round: 508 IC-only or superseded proposals, 41 unopened releases, 34 cancelled or failed-MOQ releases, and 21 with no evidence either way after a 2026-09-13 follow-up confirmed 23 other entries had actually opened for orders and kept them instead.
- Cherry listings with no pin count where a pin-specific entry exists, including `Cherry Brown`, and nameplate specimens that duplicated a plain entry. MX1A listings are Hyperglide.
- 36 artisans and 23 other listings that are not keycap sets, such as kit collections, leftover sales, faceplates, a keyboard, and single novelty keys.

**Merged.**

- 24 spelling-error pairs revealed by alias standardization and the score sheet.
- 91 lube-marked switch listings into 60 clean products. Three switches known only as modified specimens kept one clean entry.
- 65 same-release keycap pairs. Most were a KeycapLendar first round and Matrix's `R1`, a version or year standing for a round as in `GMK Dracula V2.0` and `R2`, or a Matrix title worded differently. Twelve retailer listings moved to the round that was on sale when their page opened.
- 71 MechanicalKeyboards listings joined existing entries, and layout or regional listings of one set joined it. Keychron and Ducky colorways sold in several shapes stay separate and carry the shape in their names, as in `Keychron OSA Retro`.
- Five expanded WoB/BoW names joined the matching KKB, PBTfans, GMK CYL, and current GMK MTNU entries.

**Kept apart on purpose.**

- Olivia and Olivia++, because plus signs are meaningful.
- `Cherry Blossom` by JWK and `Cherry MX Blossom` by Cherry.
- KTT Phalaenopsis, Skyloong Chocolate Rose, and other specimens without enough variant detail.
- Retailer pages reused across rounds, such as `PBTfans Spark Light R2` and `PBTfans X-ray R3`.
- Switch pairs where only one side is numbered V1.
- Historical GMK White on Black with a different designer credit, and the archived MTNU WoB release, pending evidence that they match the current listings.

**Notable metadata decisions.**

- WS Aurora keeps Haimu, Keyspensory Haze uses Aflion, and Tecsee's own pages set Purple Panda as tactile and Carrot as linear.
- XCJZ Jerrzi Lotus Stem, two Akko models, UniKeys' `MDD` labels, and `PBTfans Thermal` keep no manufacturer, because their sources disagree or only speculate.
- `Gateron Nightingale` keeps no type, because its two specimens disagree.
- `HMX Snow Crash (Overlubed Batch)` stays, because an over-lubed factory batch is a production note.

**Open for review.**

- `GMK Frost Witch r2` credits Adam from KeycapLendar while Matrix credits Krelbit.
- `PBTfans Purpolch` and `PBTfans Purpolch R4` may be one release, but KeycapLendar links a different product.
- Switch pairs such as `Akko Pink` and `Akko Pink V1` need evidence before merging.
