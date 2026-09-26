# Catalog review notes

Updated: 2026-09-26

Cleanup decisions grouped by method. Exact names and options live in the catalogs; original observations and exclusion reasons live in the import metadata.

## Names and metadata

- Normalize spelling, aliases, spacing, and round/version capitalization; match IDs and import bindings to accepted names. Prefer GMK CYL only when the product line is documented.
- Move selectable releases, kits, weights, and component options out of names. Remove redundant profile text, sales copy, measurement notes, and incidental housing annotations. Keep Cherry in the profile field and meaningful mechanism/model codes in names.
- Replace collector descriptions with verified commercial identities. A color match alone is insufficient; check construction and mechanism before renaming or merging.
- Fill manufacturer, brand, designer, material, profile, and mechanism from product evidence. Keep credits scoped to the actual contribution; do not infer factories from retailers or assign one manufacturer/material across conflicting editions.

## Merges and variants

- Keep the existing schema with an optional flat variants list. Merge aliases and child kits only into an established parent; retain independent extensions, incompatible products, and distinct mechanisms separately.
- Combine related attributes into observed configurations, such as V2 / 62g. Never generate combinations or earlier switch versions from a later number alone. Omit singleton selectors and shared properties such as Linear.
- Preserve meaningful housing, material, weight, and construction choices. Remove collector numbering, mold revisions, defects, replacement parts, LED/diffuser options, and through-hole/SMD/OG labels as independent selectors.
- Remove pin counts from names/IDs. Keep them in variants only when every option has known pin information and both 3-pin and 5-pin choices occur.
- Where both forces are given, keep only bottom-out weight without a force label; label actuation-only measurements explicitly. Distance labels omit the word Travel.
- A merge does not establish commercial availability for every historical specimen. Do not convert missing housing/version information into an assumed Standard, Clear, or V1 option.

## Identity boundaries and exceptions

- **Keycap numbering:** CRP rounds remain separate, with only that round's verified kits; standalone projects stay separate when their round is unknown. C64 rounds retain BUGER.WORK credit. Sculpt rows, SA-R3 profiles, KBParadise V60/V80 compatibility, and historical DCS project titles are not release ranges. CRP's early kit coverage remains incomplete.
- **Switch mechanisms:** retain distinctions such as Jellyfish X/Y, Huano White's different mechanisms, KTT Macaron Blue/Orange retail versus ChinaJoy mechanisms, and mechanical/optical/magnetic families. User-modified Clickiez modes are not factory variants.
- **Gateron models:** preserve KS codes in names/IDs and keep KS-3/8/9, G Pro generations, legacy, low-profile, and optical compatibility families separate. Merge confirmed aliases within those boundaries; KS-22 does not imply invented V1/V2 options or compatibility with KS-15. [Gateron FAQ](https://www.gateron.co/pages/faq).
- **Manufacturer changes:** separate Giant's Gateron origin, JWK V2-V4, and Tecsee/Panghu V5-V6. Do not transfer versions across factories. Keep earlier KTT Hyacinth distinct from HMX Hyacinth and older MZ Z1 distinct from Keygeek MZ Z1. Mixed-manufacturer parents retain no unsupported shared factory. [Giant history](https://www.theremingoat.com/blog/emt-v2-switch-review).
- **Collector labels:** Alpaca mold changes are not official V1/V2 releases; arbitrary Mahjong indices and seller-invented decimal revisions remain provenance only. [PrimeKB clarification](https://www.primekb.com/products/alpaca-linears).
- **User-curated choices:** retain numbered Gypsophila entries with housing colors in parentheses; PrimeKB T1's red 62g/red 65g/grey 67g choices; plain colors for Sea Glass and Keybay W1. Keep JWICK, Durock, and PrimeKB T1 identities separate.

## Removals and evidence

- Exclude requested open-slot/special specimens, counterfeit entries, unverified factory-sample aliases, custom frankenswitch recipes, and unresolved identities without safe merge targets. Removal for insufficient evidence does not prove a product never existed or was exclusively a sample.
- Preserve identifiable manufactured derivatives and historical products. Lubing/filming, quotation marks, or unusual names alone are not grounds for removal.
- Require manufacturer releases, bulk retail, or production-keyboard evidence for commercial identity. SwitchOddities and other single-switch sellers can describe specimens; collector lists and mirrors do not independently establish bulk availability.
- Existing specimen-only choices, including some ChinaJoy and dustproof records, remain provisional where retained. Exclusion reasons identify explicit removals; do not infer a blanket purge from these rules.

## Unresolved research

- **Keycap identities:** GMK Child Kits has no established parent; GMK International Kit/TIK, CC ZeRo/CannonCaps ZeRo, WRK Cartridge/Work Louder Wrk., and Meow Meow/Meow Waon need alias or brand verification.
- **Keycap specifications:** conflicting profiles remain for Akko Shiny Kitten and Steam Engine Cyrillic. Work Louder and URSA materials vary by edition; TRIFL's final material and Doys PC Blanks' own specifications remain unverified.
- **Sparse historical metadata:** Berry Trackday, NCC Baja, PFF WoB, TK Chalk, HiPro EC BoW, Renso, and Topre Commander Monotone/Two Tone still need reliable details. Do not borrow accessory or related-product specifications.
- **Switch identities:** Kaiche/Kaicheng Blue and Unionwell Extreme Magnetic Ice Obsidian/Light Blue lack confirmed alias relationships. DareU Low Profile Red, Jixian White's RGB bottom, and TTC Red's housing/version association remain unresolved.
- **Switch numbering:** KS-22 Low Pro Banana conflicts with documented optical usage; historical KS-1 Silent Clear/Yellow attribution remains uncertain. Do not assign KS-27/33 to unnumbered Gateron Low Profile colors, or invent versions for unversioned Healio and early KTT Wine Red weights.
- **Retained uncertainty:** Gypsophila's manufacturer/bulk provenance, some Morandi Macaron HE and dustproof specimen claims, and KTT Bamboo Gleam's identity remain unverified. Removing an annotation does not resolve those questions.

## Import persistence

- Accepted data lives in Assets/Catalogs/. Keep entry overrides and source bindings synchronized with every rename, merge, metadata change, and variant correction.
- Keep original observations, URLs, and collector notes in sources.json. Use overrides.json exclusions to prevent removed records returning; preserve their reasons rather than creating separate archives.
- Review new source observations before changing accepted choices. The catalog is not a complete inventory of every historical variant; Git history retains the detailed cleanup trail.
