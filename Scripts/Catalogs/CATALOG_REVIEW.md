# Catalog review notes

Reviewed: 2026-09-22

Consolidated evidence, merge decisions, and retired source records for catalog cleanup. Counts within individual reviews describe their historical checkpoint. Original source observations remain in sources.json, except removed observations archived below.

## CRP round and kit review

CRP rounds are separate catalog products, named `CRP R1` through `CRP R7`, plus the explicitly documented `CRP R2.2` rerun. The existing entry shape is unchanged: `variants` is a flat string list of kits within that round. Hammerworks remains the manufacturer/designer metadata, not the display-name prefix.

`R0`, `R5`, and `R5A` can describe keycap sculpt rows. Keep those kit labels, including a color or other qualifier when present, without generating predecessor labels. `R1 Accent Blue` in R6 is also a kit, not evidence of a separate release. Do not add an R0 kit to a round unless it is documented there.

### Kit evidence and coverage

Counts describe distinct documented options in the accepted list, not combinations that can be assembled from them. Names are lightly standardized for readability (PAD to Numpad, 6575 to 65/75, consistent spacing/color spelling); qualifiers remain intact. These are the options recoverable from the sources below, not a guarantee of every regional or limited release.

| Catalog entry | Kits | Evidence and limitations |
| --- | ---: | --- |
| CRP R1 | 8 | [Historical participant account](https://geekhack.org/index.php?topic=106389) identifies the delivered pairs: Tulip OG / Hebrew Green, Tulip Greek / Hebrew Blue, Arabic / Cyrillic Moscow Night, APL / Japanese Pink Sakura Rain. This is retrospective evidence; the complete child-kit inventory is unverified. The [original Hammer thread](https://geekhack.org/index.php?topic=95163.0) documents the BSP-to-CRP transition, so the original BSP proposal alone is not treated as a final CRP kit list. |
| CRP R2 | 9 | Historical owner listings identify [APL, Desko, Tulip, Peacock, WKL and R5](https://www.reddit.com/r/mechmarket/comments/dnpxni), [HHKB and Window Keys](https://www.reddit.com/r/mechmarket/comments/d0jgcc), and [Numpad](https://www.reddit.com/r/mechmarket/comments/dsmuej). The original vendor inventory could not be recovered; this list is partial. The [contemporary discussion](https://geekhack.org/index.php?topic=95163.50) identifies the original R2 Drop listing. |
| CRP R2.2 | 14 | [Syrup Labs organizer's table](https://geekhack.org/index.php?topic=102992.0) lists the rerun's bases, child kits, and compatibility kits. Keep its added Arabic, 65/75, 1800 and EU options with this rerun; do not backfill them into R2. |
| CRP R3 | 13 | All options from [Daily Clack R3](https://dailyclack.com/products/hammerworks-crp-r3), including six bases and the separately offered 65/75, HHKB and numpad colors. This surviving vendor inventory may not cover every regional offering. |
| CRP R4 | 39 | All options from [protoTypist R4](https://prototypist.net/products/group-buy-crp-r4), cross-checked against [Daily Clack R4](https://dailyclack.com/products/hammerworks-crp-r4). Preserve the separately listed beige and white modifier, sculpt and accessory kits. |
| CRP R5 | 23 | All options from [protoTypist R5](https://prototypist.net/products/group-buy-crp-r5), cross-checked against [Daily Clack R5](https://dailyclack.com/products/hammerworks-crp-r5). The vendor's `(not) Irish` wording is recorded as Irish. Its R5/R5A labels denote sculpt kits. |
| CRP R6 | 37 | The 36 options from [protoTypist R6](https://prototypist.net/products/group-buy-crp-r6) and [Daily Clack R6](https://dailyclack.com/products/hammerworks-crp-r6), plus [Desko Color explicitly marked Round 6](https://mechanicalkeyboards.com/products/hammerworks-crp-desko-color-109-key-cherry-profile-dye-sub-pbt-keycap-set). The typo `Tiupe-R-White` is normalized to Tulip-R White, supported by the [indexed Drop kit listing](https://drop.com/buy/hammerworks-crp-round-6-pbt-keycap-set?defaultSelectionIds=981617). |
| CRP R7 | 28 | All options from [protoTypist R7](https://prototypist.net/products/group-buy-crp-r7), cross-checked against [Mechanical Keyboards R7](https://mechanicalkeyboards.com/products/hammerworks-crp-r7-beige-cherry-profile-dye-sub-keycap-set-group-buy) and [Divinikey R7](https://divinikey.com/products/hammerworks-crp-r7-beige-keycaps). Arabic is in the full group-buy list even though absent from the later Divinikey selector. |

Several older Drop URLs now redirect to its new landing page. Retain the evidence distinction above rather than claiming a complete vendor inventory for R1/R2. If an older round has no reliable kit evidence in a later review, omit its `variants` field instead of copying another round's kits.

### Standalone sets and source mappings

- Keep CRP Purple, CRP Colorful Fonts Beige, CRP Colorful Fonts White, CRP Desko Black, and CRP Mint & RGBY as independent entries. Their retained listings do not establish a round. The existence of a similarly named kit in a numbered round is insufficient to identify the standalone listing with that round.
- CRP C64 R1, CRP C64 R2, Mirror Image, Wind God and CRP-X Parallel Worlds remain their own projects. The Daily Clack C64 Round 2 binding points to C64 R2, not CRP R2.
- The Mechanical Keyboards ANSI English, Irish, Peacock, Runes, All Red, 40s Mods, All In Mods, Modern and Numpad pages use explicit `HW-CRP-R6...` SKUs; their source bindings belong to CRP R6. Desko Color independently says Round 6 in its description. Other listings are assigned using their explicit round names.
- Original source observations and notes are preserved. Research observations use `reviewedkeycaps` provenance with evidence URLs and dated coverage notes. Canonical names, kit lists and binding redirects are pinned in `overrides.json`; importer normalization keeps rounds separate and treats their kit labels literally.

No catalog fields, nested variants, per-variant IDs, or app database tables were added.

### C64 kits and corrected combined listing

Both C64 rounds credit `BUGER.WORK` as designer and retain Hammerworks as manufacturer. The original unnumbered C64 entry is now `CRP C64 R1` (`crp-c64-r1`). Like the main CRP rounds, C64 rounds use literal kit lists without generated release variants.

| Entry | Confirmed variants | Evidence |
| --- | --- | --- |
| CRP C64 R1 | 80s, C64 Alphas, Mac, Modifiers, Numpad, Spacebars, Vertical F | Partial historical inventory. The [original group buy](https://geekhack.org/index.php?topic=109469.0) documents C64 Alphas, Mac and 80s/modifier purchases. A [2021 owner listing](https://www.reddit.com/r/mechmarket/comments/oyw87s) confirms numpad and spacebars. The [designer's R2 announcement](https://geekhack.org/index.php?topic=117973.0) explicitly identifies Mac and Vertical F as discontinued R1 kits. Ordinary Alphas and ISO are unverified for R1 and are not copied from R2. |
| CRP C64 R2 | 40s, Alphas, C64 Alphas, ISO, Modifiers, Numpad, Spacebars, TKL | All eight keycap options in [protoTypist's inventory](https://prototypist.net/products/in-stock-hammer-x-buger-crp-c64-r2-keycaps), cross-checked against [Daily Clack](https://dailyclack.com/products/hammerworks-crp-c64-round-2). The designer confirms added 40s support and revised numpad kitting. Desk mats, cases and separate resin products are outside this kit list. |

`CRP Tulip + Peacock` described two sets, not one product. Remove the combined entry and exclude its source observation on future imports; existing round lists retain Tulip and Peacock as separate kits. The listing's round is unverified, so it is not rebound to an assumed round.

Retired observation (preserved here rather than left as a dangling binding): source `keycaplendar`, source ID `0O8WOX7MyOCS52QWfz3t`, former catalog ID `crp-tulip-plus-peacock`, observed name `CRP Tulip + Peacock`, observed designer `Hammerworks`, [source URL](https://keycaplendar.firebaseapp.com/?keysetAlias=P9jz76rjjp). Original notes: `Upstream category: CRP; icDate: 2020-06-01; gbLaunch: 2020-06-06; gbEnd: 2020-06-20; details: https://drop.com/buy/hammerworks-crp-pbt-dye-subbed-keycap-set?mode=guest_open`.

## GMK kit and alternate-name review

Canonical-name follow-up: prefer `GMK CYL` names and `gmk-cyl-...` IDs when duplicate entries or retained source observations explicitly identify the set as CYL. The corrections below describe the original plain-GMK names; those with CYL evidence now use that prefix. Preserve MTNU as a separate profile.

Keep add-on kits in the parent set's flat `variants` list. An alternate name for the same product does not create another variant. Preserve original observations in `sources.json`, redirect their bindings, and pin canonical names and lists in `overrides.json`.

| Canonical entry | Correction |
| --- | --- |
| GMK 9009 | Fold 40s Addon and Ortho & Vim into the existing entry; retain R1, R2 and R3. Source designer credits for individual add-ons remain in provenance; the parent retains its base-set designer. |
| GMK Alt Grrrrr | Merge the Addon listing as an alternate name, without inventing an Addon variant. |
| GMK Black Snail | Merge Red Cyrillic Addon and add the 15 documented options below. The base designer stays Geon; the separate add-on's Peter6109 credit remains in its source observation. |
| GMK Beloved | Remove the KA2017 Revival subtitle from name and ID. |
| GMK Classic Beige | Remove the KA1953 Revamp subtitle from name and ID. |
| GMK Hammerhead | Remove separate (2020) and CYL Small Batch entries and redirect their observations to Hammerhead. Retain R1/R2; neither the year nor the sales-batch label becomes a kit. |
| GMK Gregory | Redirect CYL GREG 2 to Gregory, whose existing variants already include R2. |

### Black Snail evidence

- [GEON's product selector](https://geon.works/products/gmk-black-snail) lists Base, Alphas, Numpad, Spacebars, Novelties, L9 Modifiers, U9 Modifiers, 9009 Accents, Cherry Accents, Ortholinear, 40s Base and Relegendable. Exclude its desk mats and aluminium artisan from the shared ABS keycap-kit entry.
- [ktechs](https://ktechs.store/products/gmk-black-snail) additionally lists Cyrillic Alphas, corroborated as Red Cyrillic Alphas by [Cafege](https://cafege.com.au/products/extras-gmk-black-snail).
- [Eloquent Clicks' vendor guide](https://eloquentclicks.com/de/blogs/mechanical-keyboard/elevate-your-keyboards-aesthetics-with-premium-keycap-sets) lists Black Snail with ESFR and NORDEUK support kits.
- [Yushakobo's original group-buy selector](https://shop.yushakobo.jp/en/products/8286) calls the novelty option Novelties & Shinethrough; use that fuller label for the same kit rather than duplicating it.

The resulting list has 15 options. This combines documented regional and later offerings; it does not imply that every vendor offered every option simultaneously. Retro Point is a separate named product and is not added merely because another vendor bundles it into the same selector.

### GMK / CYL duplicate review

On 2026-09-22, six groups were merged and 97 already-combined sets adopted their retained sources' CYL spelling. Source observations, kit lists and release labels are retained; binding redirects and entry overrides use the canonical IDs.

- Classic Beige: merge plain GMK and GMK CYL; preserve the existing designer credit. GMK MTNU Classic Beige stays separate.
- WoB: merge White on Black into GMK CYL WoB. [GMK's own product page](https://www.gmk.net/shop/en/gmk-cyl-wob-white-on-black-keycaps/fptk5007.0) explicitly equates these names.
- Olivia: combine Olivia, Olivia++ and CYL Olivia No3 as GMK CYL Olivia, retaining R1/R2/R3. [The CYL vendor page](https://oblotzky.industries/products/gmk-cyl-olivia-no3) identifies No3 as the third round.
- Vaporwave: combine CYL Vaporwave W2 and Vaporwave as GMK CYL Vaporwave with R1/R2. The [designer's R2 thread](https://geekhack.org/index.php?topic=112165.150) identifies the second run; the [vendor lists it under CYL W2](https://oblotzky.industries/products/gmk-cyl-vaporwave-w2).
- Dolch: combine Dolch and CYL Dolch R5X as GMK CYL Dolch, retaining the literal R5X variant. [The vendor](https://oblotzky.industries/products/gmk-cyl-dolch-r5x) describes the R5 bottom-row configuration. R5X is not expanded into another release number.

Accepted identities and original observations are retained in the catalogs, overrides.json, and sources.json; the temporary before/after report was removed after verification. Sets lacking a CYL duplicate or explicit retained CYL observation are not renamed solely from their manufacturer.

Kaiju Part Deux was also merged into GMK CYL Kaiju: its retained protoTypist observation explicitly names it GMK CYL Kaiju R2. Existing R1/R2/R3 variants remain intact.

## Kit and add-on catalog review

Reviewed all 90 keycap names containing whole-word kit/kits or addon/add-on/add on, plus the only matching switch (Transmit Garage Kit). Merged 65 child or duplicate entries into 51 canonical products, preserving kit labels in flat variants and redirecting source bindings and overrides. No app/schema changes.

Use the longest matching parent identity with a compatible profile. A manufacturer/profile alone is not a parent: in particular, the historical DCS round entry is not a bucket for every DCS kit. Keep osume Cherry and Marshmallow kits with their respective parents. Child designers remain in source observations; parent metadata is not overwritten by add-on authors or by material differences between releases.

### Evidence for nontrivial decisions

- [PBTfans BOW](https://pbt.fans/products/pbtfans-doubleshot-bow) distinguishes icon/simple base and text base; the existing Base Icon Kit listing is a child option. [PBTfans Resonance R2](https://pbt.fans/products/pbtfans-resonance-r2) lists 40s, numpad and international options. Other exact parent/kit matches retain their original retailer observations in sources.json.
- [CannonKeys Bakeneko](https://cannonkeys.com/products/nicepbt-bow-bakeneko-kit) explicitly identifies NicePBT BoW Bakeneko kitting. Merge it into NicePBT BoW as Bakeneko Kit.
- [DCS After School 1992](https://www.keebzncables.com/products/dcs-after-school) is a 40s-only set; merge the duplicate 40s Kit and 40s Monokit listings, keeping DSS separate.
- [GMK CYL Centinela](https://www.gmk.net/shop/en/gmk-cyl-centinela-extension-kits/gmk10096) is a standalone extension project. The [designer](https://www.reddit.com/r/MechanicalKeyboards/comments/1sbxp12/gb_gmk_centinela_extension_kit/) describes variants for multiple colorways.
- [GMK CYL Beige Addon & Extension](https://oblotzky.industries/products/gmk-cyl-beige-addon-extension) works across L9/U9 beige sets. Merge its duplicate listings; do not force it into Classic Beige.
- [osume winterglow collection](https://osume.com/collections/winterglow) includes the Eve novelty kit; preserve Eve Novelty Kit as a distinct option alongside Novelty Kit.
- [DSS Honeywell Fix Kit organizer](https://www.keebtalk.com/t/gb-dss-honeywell-40-fix-kit/23364) identifies its DSS base. That base is absent here, so the add-on stays separate from DCS/GMK Honeywell.
- [GMK Child Kits](https://novelkeys.com/products/gmk-child-kits) is a clearance listing whose accessible selector did not expose parent names; retained as unresolved rather than guessed into one set.

The audit below is exhaustive for the 90 matched keycap names at the start of this review. A keep decision can mean a standalone project or an unresolved parent, as distinguished in the reason. Transmit Garage Kit remains a named linear switch; the word Kit is not evidence of a keycap child kit.

### Decisions

| Original entry | Result | Detail |
| --- | --- | --- |
| Centinela Extension Kit | gmk-cyl-centinela-extension-kit | Duplicate listing. Duplicate listing of the same standalone product. |
| DCS 9009 Caps Lock Kit | dcs-9009 | Variant: Caps Lock Kit. Named child kit; longest matching parent name and compatible profile. |
| DCS 9009 Fix Kit | dcs-9009 | Variant: Fix Kit. Named child kit; longest matching parent name and compatible profile. |
| DCS 9009 Polish Addon | dcs-9009 | Variant: Polish Addon. Named child kit; longest matching parent name and compatible profile. |
| DCS After School 1992 40s kit | dcs-after-school-1992-40s-monokit | Duplicate listing. Duplicate listing of the same standalone product. |
| DCS Bae Addon | Keep standalone | Generic Big Ass Enter compatibility kit; no single named parent. |
| DCS Color Accent Kits | Keep standalone | Standalone accent collection spanning DCS sets. |
| DCS Flex Kits | Keep standalone | Standalone community compatibility kits; not a child of the historical DCS round entry. |
| DCS LAE Addon (Little Enter) | Keep standalone | Generic Little Enter compatibility add-on; no single named parent. |
| DCS Row 4 Accent Kits | Keep standalone | Standalone sculpt-row accent collection; Row 4 is not a set name or release. |
| DCS Wyse Moogle Kit GH | Keep standalone | Historical Geekhack adaptation kit for Wyse; no accepted Wyse base entry. Do not assume identical kitting to the KBDMania run. |
| DCS Wyse Moogle Kit KBDMania | Keep standalone | Historical KBDMania adaptation kit for Wyse; no accepted Wyse base entry. Do not assume identical kitting to the Geekhack run. |
| DCX Keycap Accent Kits | Keep standalone | Generic DCX accents; no single named parent. |
| DSS Honeywell 40% Fix Kit | Keep standalone | Confirmed child of DSS Honeywell, but that base is absent. Keep reachable; do not merge into DCS or GMK Honeywell. |
| GMK 2Pack Add-on | Keep standalone | Standalone named add-on project; no accepted 2Pack parent. |
| GMK Beige Addon Extension | gmk-cyl-beige-addon-extension | Duplicate listing. Duplicate listing of the same standalone product. |
| GMK Beige/WoB uwu Macro Addon | Keep standalone | Cross-set Beige/WoB accessory, not exclusively Classic Beige or WoB. |
| GMK Black Mod Addon | Keep standalone | Standalone Black Modi modifier project; black alone does not establish a WoB parent. |
| GMK Centinela Extension Kit | Keep standalone | Retain as standalone GMK CYL Centinela Extension Kit; merge the duplicate unprefixed listing. Designer explicitly describes a multi-colorway standalone add-on. |
| GMK Child Kits | Keep standalone | Unresolved clearance listing: vendor selector did not expose the individual set names. Preserve pending evidence; do not bind it to a guessed parent. |
| GMK CYL BAE Addons | Keep standalone | Generic Big Ass Enter accessories spanning multiple sets. |
| GMK CYL Beige Addon | Keep standalone | Retain as GMK CYL Beige Addon & Extension and merge its plain-GMK duplicate. Vendor states compatibility with any L9/U9 beige set. |
| GMK Dualshot Accent Kit | gmk-cyl-dualshot | Variant: Accent Kit. Named child kit; longest matching parent name and compatible profile. |
| GMK Gegenschlag Add-on | gmk-cyl-gegenschlag | Variant: Add-on. Named child kit; longest matching parent name and compatible profile. |
| GMK Greek Beige Add-on | Keep standalone | Generic Greek beige compatibility kit; no uniquely established parent set. |
| GMK International Kit Addon | Keep standalone | No uniquely established parent. Possible TIK duplicate, but retained Matrix page has an unrelated Phantom outbound link; insufficient evidence to merge. |
| GMK Metropolis NorDeUK Addon | gmk-cyl-metropolis | Variant: NorDeUK Addon. Named child kit; longest matching parent name and compatible profile. |
| GMK Mictlan NorDeUK Addon | gmk-mictlan | Variant: NorDeUK Addon. Named child kit; longest matching parent name and compatible profile. |
| GMK Monokai Material NorDeUK Addon | gmk-cyl-monokai-material | Variant: NorDeUK Addon. Named child kit; longest matching parent name and compatible profile. |
| GMK MR Sleeves Addon | gmk-cyl-mr-sleeves | Duplicate listing. Duplicate listing of the same standalone product. |
| GMK N9 Ortholinear Add-on | Keep standalone | Generic N9-color ortholinear compatibility kit; no single parent. |
| GMK RGBYK Addon | gmk-rgbyk | Duplicate listing. Duplicate listing of the same standalone product. |
| GMK Swiss Addon | Keep standalone | Generic language compatibility kit; no single parent. |
| GMK TIK Addon | Keep standalone | Standalone The International Kit project spanning several colorways, not a child of one named set. |
| GMK WoB & BoW Hangul Addons | gmk-wob-bow-hangul | Duplicate listing. Duplicate listing of the same standalone product. |
| GMK WoBBoW NORDEUK Add-On | Keep standalone | Cross-set WoB/BoW regional kit; do not assign solely to WoB or BoW. |
| JCS Mint Extension Kit | Keep standalone | Standalone JC Studio accent kit; no matching Mint base in the accepted catalog. |
| NicePBT Accent Kits | Keep standalone | Generic accent collection; no single parent. |
| NicePBT Bakeneko Kit | nicepbt-bow | Variant: Bakeneko Kit. Verified named kit of the parent set. |
| osume bloom novelty kit | osume-bloom | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume café extras kit | osume-cafe | Variant: Extras Kit. Named child kit; longest matching parent name and compatible profile. |
| osume café marshmallow extras kit | osume-cafe-marshmallow | Variant: Extras Kit. Named child kit; longest matching parent name and compatible profile. |
| osume café marshmallow novelty kit | osume-cafe-marshmallow | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume café novelty kit | osume-cafe | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume café rose extras kit | osume-cafe-rose | Variant: Extras Kit. Named child kit; longest matching parent name and compatible profile. |
| osume café rose marshmallow extras kit | osume-cafe-rose-marshmallow | Variant: Extras Kit. Named child kit; longest matching parent name and compatible profile. |
| osume café rose marshmallow novelty kit | osume-cafe-rose-marshmallow | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume café rose novelty kit | osume-cafe-rose | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume dalgona marshmallow novelty kit | osume-dalgona-marshmallow | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume dalgona novelty kit | osume-dalgona | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume dusk novelty kit | osume-dusk | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume horoscope novelty kit | osume-horoscope | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume kanagawa novelty kit | osume-kanagawa | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume konbini blue novelty kit | osume-konbini-blue | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume konbini green novelty kit | osume-konbini-green | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume lilac dreams marshmallow novelty kit | osume-lilac-dreams-marshmallow | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume lilac dreams novelty kit | osume-lilac-dreams | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume little ghost novelty kit | osume-little-ghost | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume matcha marshmallow novelty kit | osume-matcha-marshmallow | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume matcha novelty kit | osume-matcha | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume mocha marshmallow novelty kit | osume-mocha-marshmallow | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume mocha novelty kit | osume-mocha | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume momo neko marshmallow novelty kit | osume-momo-neko-marshmallow | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume mori novelty kit | osume-mori | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume night market novelty kit | osume-night-market | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume rainy day novelty kit | osume-rainy-day | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume sakura novelty kit | osume-sakura | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume strawberry milk marshmallow novelty kit | osume-strawberry-milk-marshmallow | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume strawberry milk novelty kit | osume-strawberry-milk | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume tsukimi novelty kit | osume-tsukimi | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume winterglow eve novelty kit | osume-winterglow | Variant: Eve Novelty Kit. Verified named kit of the parent set. |
| osume winterglow novelty kit | osume-winterglow | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume year of the horse novelty kit | osume-year-of-the-horse | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume year of the snake novelty kit | Keep standalone | Matching base absent from the accepted catalog; preserve the named kit without inventing a base entry. |
| osume zen marshmallow novelty kit | osume-zen-marshmallow | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| osume zen novelty kit | osume-zen | Variant: Novelty Kit. Named child kit; longest matching parent name and compatible profile. |
| PBTfans BOW Base Icon Kit | pbtfans-bow | Variant: Base Icon Kit. Named child kit; longest matching parent name and compatible profile. |
| PBTfans Kabuki-Cho Alphas Kit | pbtfans-kabuki-cho | Variant: Alphas Kit. Named child kit; longest matching parent name and compatible profile. |
| PBTfans Kabuki-Cho Icon Numpad Kit | pbtfans-kabuki-cho | Variant: Icon Numpad Kit. Named child kit; longest matching parent name and compatible profile. |
| PBTfans Klein Blue International Kit Translucent | pbtfans-klein-blue | Variant: International Kit Translucent. Named child kit; longest matching parent name and compatible profile. |
| PBTfans Klein Blue Numpad Kit Translucent | pbtfans-klein-blue | Variant: Numpad Kit Translucent. Named child kit; longest matching parent name and compatible profile. |
| PBTfans Resonance 40s Kit | pbtfans-resonance | Variant: 40s Kit. Named child kit; longest matching parent name and compatible profile. |
| PBTfans Resonance International Kit | pbtfans-resonance | Variant: International Kit. Named child kit; longest matching parent name and compatible profile. |
| PBTfans Resonance Numpad Kit | pbtfans-resonance | Variant: Numpad Kit. Named child kit; longest matching parent name and compatible profile. |
| PBTfans Retro Dark Lights RGBY Kit | pbtfans-retro-dark-lights | Variant: RGBY Kit. Named child kit; longest matching parent name and compatible profile. |
| PBTfans Retro Dark Lights RGBY Text Kit | pbtfans-retro-dark-lights | Variant: RGBY Text Kit. Named child kit; longest matching parent name and compatible profile. |
| PBTfans Twist 40s Kit | pbtfans-twist | Variant: 40s Kit. Named child kit; longest matching parent name and compatible profile. |
| PBTfans Twist International Kit | pbtfans-twist | Variant: International Kit. Named child kit; longest matching parent name and compatible profile. |
| PBTfans Twist Numpad Kit | pbtfans-twist | Variant: Numpad Kit. Named child kit; longest matching parent name and compatible profile. |
| PBTfans WOB Base Icon Kit | pbtfans-wob | Variant: Base Icon Kit. Named child kit; longest matching parent name and compatible profile. |

## Outemu Sky review

Merge the collector labels Outemu Sky Clear/Clear and Clear/ICE into `Outemu Sky` (`outemu-sky`). Preserve all 12 original ThereminGoat observations as provenance; the slash-separated labels are not independently verified product names. Outemu Sky Silent remains separate.

The accepted variants are `62g`, `68g`, `75g`, `80g`, `V2.1`, and `V2.2`. The [original seller's 2018 listing](https://www.reddit.com/r/mechmarket/comments/911297/uscahdiy_outemu_sky_choice_of_top_v21_stems_and/) offers those four Outemu spring weights (bottom-out force), explicitly sells V2.1 stems, and explains V2.2 stems in the seller's reply. Its optional Cherry MX Black spring is not a named Outemu variant. Do not extrapolate versions, retain uncorroborated 48g combinations, or imply every version/weight combination was independently verified. V2.0 remains in historical observations but is omitted from this externally corroborated list.

The user's evidence-only request overrides the general predecessor-fill rule for this entry. Import normalization preserves its explicit list, and the reviewed override prevents collector labels or unverified combinations from returning.

Remove the three accepted open-slot entries: Outemu Silent Lemon (Open Slot), Outemu Silent Peach (Open Slot), and TTC Flame (Open Slot, No Cond.). Their original entries and three source observations are archived in [retired switch records](#retired-switch-records). Explicit source exclusions and the open-slot name filter prevent reintroduction by subsequent imports. The user also requested removal of TTC Flame (Slotted) and TTC Flame Red Half Height LED; their entries and source observations are included in the same archive, with explicit source exclusions.

## Later corrections

Cherry profile wording was removed from 11 keycap names and IDs; trailing product-description text was removed from 14 designer fields. The dedicated Cherry profile field remains. Original source observations are preserved.

GMK CYL WoB Extensions merges the former GMK WoB 40s, Colevrak+, R0/R5 duplicate. Its three kits are 40s, Colevrak+, and R0/R5; R0/R5 is a sculpt-row kit and must not generate rounds R1 through R5. The designer is ttom. Verified against [NovelKeys](https://novelkeys.com/products/gmk-wob-extensions) and the [original group buy](https://geekhack.org/index.php?topic=105239.0).

### Historical DCS group buys

The former generic DCS entry incorrectly combined two source identities and inferred R2. Restore [DCS Round 1](https://dcs.wiki/keycaps/dcs-round-1) as dcs-round-1, credited to WhiteRice, and [DCS Round 3 and 4](https://dcs.wiki/keycaps/dcs-round-3-and-4) as dcs-round-3-and-4, credited to 7bit. Both retain Signature Plastics and DCS/ABS metadata. Neither has a variants field: the round numbers are part of the historical project titles. Preserve the two original source observations and prevent predecessor filling for these titles.

## Switch variant combinations and singleton review

Reviewed: 2026-09-22

Switch labels now describe complete observed options (for example, V2 / 62g or V2 / 53g / 22mm Spring), without generating other versions or weight combinations. Version-only lists outside this review retain their prior content. Attributes whose relationship is unverified remain separate; a missing version number is not guessed. Original means a separately documented unnumbered original product, not an arbitrary unspecified option.

Reviewed all 127 pre-existing single-option entries plus 8 that became single-option entries after pairing. Checks used every retained observation, cached retailer descriptions and selectors where available, and targeted online searches for alternatives. The table records evidence and limitations; failure to find another option is not proof none ever existed. Removed 94 one-option fields, retained or added documented alternatives, and merged 12 split spring-option entries into their parent products. Three malformed KS-9 G model names were repaired. All source observations are preserved.

Outemu Sky supersedes its earlier flat list above: V2.1 / 62g, V2.1 / 68g, V2.1 / 75g, V2.1 / 80g, V2.2 / 62g, V2.2 / 68g, and V2.2 / 75g. The 80g combination is explicitly offered with V2.1 in the original seller listing; V2.2 / 80g is not inferred.

### Combination decisions

| Entry | Accepted variants |
| --- | --- |
| aqua-zilent | V2 / 62g; V2 / 67g |
| branded-stealio | V2 / 62g; V2 / 65g; V2 / 67g; V2 / 78g |
| bsun-holy-red-panda | V1 / Clear Stem; V1 / True Stem; V2 / Clear Stem; V2 / True Stem |
| c3-tangerine | V1; V2 / 62g; V2 / 67g; V2.2 / 67g |
| duhuk-lumia-matcha-pro | V3 / 55g; V3 / 63.5g |
| duhuk-matcha | V4 / 55g; V4 / 63.5g |
| durock-zealio | V2 / 62g; V2 / 65g; V2 / 67g; V2 / 78g |
| everglide-aqua-king | V3 / 55g; V3 / 62g; V3 / 67g |
| everglide-tourmaline-blue-pro | Field omitted after singleton review |
| everglide-water-king | V3 / 37g; V3 / 55g; V3 / 60g; V3 / 67g |
| hmx-caramel-pudding | V2 / 45g; V2 / 53g |
| hmx-cloud | V1 / 50g; V2 / 43g / 22mm Spring; V2 / 53g / 22mm Spring; V2 / 63g / 18mm Spring |
| hmx-cloud-18mm | Merged into hmx-cloud: V1 / 50g; V2 / 43g / 22mm Spring; V2 / 53g / 22mm Spring; V2 / 63g / 18mm Spring |
| hmx-cloud-22mm | Merged into hmx-cloud: V1 / 50g; V2 / 43g / 22mm Spring; V2 / 53g / 22mm Spring; V2 / 63g / 18mm Spring |
| hmx-hades | V2 / 42g; V2 / 58g |
| hmx-macchiato | V2 / 42g; V2 / 50g; V2 / 57g |
| holy-invyr-panda | V1 / Clear Stem; V1 / True Stem; V3 / Clear Stem; V3 / True Stem |
| jixian-white | V2; V2 / RGB Bottom; V3 |
| kailh-box-white | V1; V2 |
| keygeek-su-color | V2 / 45g; V2 / 50g |
| krazy-top-clear-base | Field omitted after singleton review |
| ktt-hyacinth | Original; V1 / Green Stem |
| ktt-wine-red | V1; V3 / 37g; V3 / 43g |
| lekker | V2 / 45g; V2 / 60g |
| mmd-princess-linear | Original / 28g; Original / 38g; Original / 45g; Original / 53g; V2 / 28g; V2 / 38g; V2 / 45g; V2 / 53g; V3 / 46.5g; V3 / 53.5g; V4 / 28g; V4 / 38g; V4 / 45g; V4 / 53g |
| mmd-princess-tactile | Original / 48g; Original / 60g; V2 / 48g; V2 / 62g; V3 / 48g; V3 / 62g; V4 / 48g; V4 / 60g |
| mmd-vivian | Original / 28g; Original / 35g; V2 / 43g; V2 / 53g |
| naevy | V1; V1 / UHMWPE Stem; V1.5; V2 |
| outemu-sky | V2.1 / 62g; V2.1 / 68g; V2.1 / 75g; V2.1 / 80g; V2.2 / 62g; V2.2 / 68g; V2.2 / 75g |
| phoenix-clear-clear | V1 / 48g; V1 / 62g; V1 / 68g; V1 / 75g; V2 / 48g; V2 / 62g; V2 / 68g; V2 / 75g |
| phoenix-manual-declaw | Field omitted after singleton review |
| phoenix-retooled-base | Field omitted after singleton review |
| red-noppoo-top-clear-base | Field omitted after singleton review |
| smoky-durock-zealio | V2 / 62g; V2 / 65g; V2 / 67g; V2 / 78g |
| tealio | V1 / 67g; V2 / 67g |
| ttc-orange | V1 / 45g; V1 / 60g |
| white-noppoo-top-clear-base | Field omitted after singleton review |
| wingtree-uchi | V2 / 32g; V2 / 43g |
| zealio | V1 / 62g; V1 / 65g; V1 / 67g; V1 / 78g; V1 / R1 / 62g; V1 / R1 / 65g; V1 / R1 / 67g; V2 / 62g; V2 / 65g; V2 / 67g; V2 / 78g |
| zealio-ac | V2 / 62g; V2 / 65g; V2 / 67g; V2 / 78g |
| zealio-milky-bottom-error | Field omitted after singleton review |
| zealio-redux | V1 / 62g; V1 / 67g |
| zilent | V1 / 62g; V1 / 65g; V1 / 67g; V1 / 78g; V2 / 62g; V2 / 65g; V2 / 67g; V2 / 78g |
| zilent-ac | V2 / 62g; V2 / 65g; V2 / 67g; V2 / 78g |
| zilent-cloudy-top | Field omitted after singleton review |

### Singleton decisions

Additional weight corroboration for Durock Medium Tactile: the [retailer specification](https://www.noon.com/uae-en/tactile-switches-clear-purple-medium-tactility-keyboard-switches-5-pins-mx-clear-type-switches-pre-lubed-purple-tactile-65g-110pcs/ZDA3CADD7AC7DB7C1EB7CZ/p/) lists 62g, 65g and 67g; the Mechbox inventory below additionally lists 78g.

Each source link is evidence for the known switch or its alternatives. Collector-only cases with no corroborated alternative lose the redundant selector, not their product entry or provenance. Other named mechanisms, materials, errors and prototypes are not assumed to be interchangeable options.

| Original entry | Result | Review and evidence |
| --- | --- | --- |
| AEBoards Raed HE White | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Aflion Ink (Shadow) | 55g; 63g | The source identifies 55g as a lighter option; a retailer documents the heavier Shadow/Ink. [Source](https://milktooth.com/products/shadow-ink-double-spring) |
| Ajazz Brown | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/ajazz-brown) |
| Ajazz Purple | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Ajazz Red | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/ajazz-red) |
| Akko Creamy Cyan | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://akkogear.eu/products/akko-creamy-cyan-switch-45pcs) |
| Akko Purple | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/akko-v1-purple) |
| Badseedtech | V1; V2 | The designer documents the V2 replacement run. [Source](https://www.reddit.com/r/MechanicalKeyboards/comments/10ob18p) |
| Boba Grey Silent | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Bobagum | 52g; 62g; 68g | The original seller lists all three spring weights. [Source](https://www.reddit.com/r/u_hbheroinbob/comments/o999hu) |
| BSUN Dustproof Brown | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/bsun-dustproof-brown) |
| BSUN Yellow Panda | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/bsun-yellow-panda) |
| C3 x TKC Tangerine Dark | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/c-equalz-kiwi) |
| C3 x TKC Tangerine Light | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/c-equalz-x-tkc-tangerine-light-62g) |
| Cherry M8 White | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Cherry MX Brown | 3-pin; 5-pin | Manufacturer part table distinguishes plate and PCB mount. [Source](https://storage.googleapis.com/mauser-public-images/prod_description_document%2F2024%2F337%2Ff8b41fe83a80fc5fd5e18b291c25eb33_mx_brown_-_mx1a-g1...pdf) |
| Cherry MX Dark Blue | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/cherry-dark-blue) |
| Cherry MX RGB Brown | 3-pin; 5-pin | Manufacturer lists RGB MX1A-G1NA and MX1A-G1NB mounting options. [Source](https://storage.googleapis.com/mauser-public-images/prod_description_document%2F2024%2F337%2Ff8b41fe83a80fc5fd5e18b291c25eb33_mx_brown_-_mx1a-g1...pdf) |
| Cherry MX RGB Speed Silver | 3-pin; 5-pin | Manufacturer lists MX1A-51NA and MX1A-51NB. [Source](https://www.mouser.com/datasheet/2/71/EN_CHERRY_MX_SPEED_Silver-2322168.pdf) |
| Cherry MX2A RGB Brown | 3-pin; 5-pin | The manufacturer selector offers both pin counts. [Source](https://shop.cherry.de/fr-fr/cherry-mx2a-rgb-brown-switch-kit.html) |
| Content Blue | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://raw.githubusercontent.com/ThereminGoat/switch-scores/refs/heads/master/1-Composite%20Overall%20Total%20Score%20Sheet.csv) |
| Content Silver | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/content-silver) |
| DareU Candy Pink | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Dukharo MO Green | V1; V2 | Both generations are described by the retained vendor; V2 is also sold separately. [Source](https://stupidbulletstech.com/collections/accessories) |
| Dukharo MO Pink | V1; V2 | Both generations are described by the retained vendor; V2 is also sold separately. [Source](https://stupidbulletstech.com/collections/accessories) |
| Durock Medium | 62g; 65g; 67g; 78g | The retained seller states four weights; retailer listings corroborate the weight options. [Source](https://mechbox.co.uk/collections/switches-singles/tactile) |
| 'Error Leaf' Aqua Zilent | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Everglide Amber Orange Tactile | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Everglide Dark Blue Optical | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Firstblood Pink | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Gateron Cap Yellow (Milky Top) | V1; V2 | The milky-top CAP has a documented V2. [Source](https://www.thockking.com/products/gateron-milky-yellow-cap-linear-switches) |
| Gateron Cap Yellow (Yellow Top) | V1; V2 | The golden CAP has a documented V2. [Source](https://www.gateron.co/products/gateron-cap-switch-set) |
| Gateron Harmonic | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://divinikey.com/products/gateron-harmonic-clicky-switches) |
| Gateron KS- Pro 3.0 Black | Merged/renamed to gateron-ks-9-g-pro-3-0-black; Variants field omitted | Restore KS-9 G in the model name; 9G was a parsing artifact, not a weight variant. [Source](https://keebsforall.com/products/gateron-ks-9-g-pro-3-0-black-linear-switches) |
| Gateron KS- Pro 3.0 Silver | Merged/renamed to gateron-ks-9-g-pro-3-0-silver; Variants field omitted | Restore KS-9 G in the model name; 9G was a parsing artifact, not a weight variant. [Source](https://keebsforall.com/products/gateron-ks-9-g-pro-3-0-silver-linear-switches) |
| Gateron KS- Pro 3.0 Yellow | Merged/renamed to gateron-ks-9-g-pro-3-0-yellow; Variants field omitted | Restore KS-9 G in the model name; 9G was a parsing artifact, not a weight variant. [Source](https://keebsforall.com/products/gateron-ks-9-g-pro-3-0-yellow-linear-switches) |
| Gateron Melodic | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://cannonkeys.com/products/gateron-melodic-switches) |
| Gateron Red Ink | V1; V2 | The same red Ink switch is offered as V2. [Source](https://keebsforall.com/en-ca/products/gateron-red-ink-v2-switches) |
| Gateron Robin | 62g / 18mm Spring; 67g / 16mm Spring | The retailer offers two weight/spring-length options. [Source](https://ktechs.store/products/gateron-robin-linear-switch) |
| Gateron Silent Ink | V1; V2 | The manufacturer documents Ink V2 Silent Black. [Source](https://www.gateron.co/blogs/news/gateron-quiet-switch-buying-guide) |
| Gateron Yellow Ink | V1; V2 | The Ink V2 range includes yellow. [Source](https://www.gateron.co/products/gateron-ink-switch) |
| Gazzew Boba LT ( Thock) | 55g; 65g | Two manufacturer-listed spring options. [Source](https://www.gazzew.com.hk/products/boba-lt) |
| Greetech Blue | Black Housing / 3-pin; Clear Top / White Bottom / 3-pin / SMD; Clear Top / White Bottom / 5-pin / Through-hole | Retained retailer listings identify these complete housing/mount combinations. [Source](https://switchoddities.com/collections/our-catalog?page=85) |
| Hako Clear | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Healio | 67g; V2 / 63.5g | The retained historical observation is 67g without a version; the current manufacturer lists V2 / 63.5g. Do not guess a version for the historical weight. [Source](https://zealpc.net/products/healio) |
| Hecate Blue | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/hecate-blue) |
| Hecate Brown | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/hecate-brown) |
| Hecate Red | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/hecate-red) |
| Hexin Workshop Bamboo Green | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://raw.githubusercontent.com/ThereminGoat/switch-scores/refs/heads/master/1-Composite%20Overall%20Total%20Score%20Sheet.csv) |
| Holy Panda X | 3-pin; 5-pin | Both mount options exist in retained Drop listings. [Source](https://switchoddities.com/products/drop-holy-panda-x-3-pin) |
| Huano Strawberry Jelly Tactile | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/huano-strawberry-jelly-v1) |
| Jerrzi Blue | Clear Bottom; Full Black Housing / Winglatch | A seller documents a separate black-housing option alongside the retained clear-bottom option. [Source](https://switchoddities.com/products/jerrzi-black-housing-blue) |
| Jixian Brown | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/jixian-brown) |
| Jixian Red | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/jixian-red) |
| Kailh Box Crystal Pink | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://cannonkeys.com/products/box-crystal-pink-switch) |
| Kailh Speed Copper | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://www.keychron.com/products/kailh-speed-switch) |
| Kailh Speed Gold | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://www.keychron.com/products/kailh-speed-switch) |
| Kailh Speed Silver | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://www.keychron.com/products/kailh-speed-switch) |
| Keychron Red | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/keychron-red) |
| Keygeek Cera X - (20mm Spring) | Merged/renamed to keygeek-cera-x; 42g / 22mm Spring; 48g / 20mm Spring | Merged into keygeek-cera-x. Vendor options pair each weight with its spring length. [Source](https://divinikey.com/products/keygeek-cera-x-linear-switches) |
| Keygeek Cera X - (22mm Spring) | Merged/renamed to keygeek-cera-x; 42g / 22mm Spring; 48g / 20mm Spring | Merged into keygeek-cera-x. Vendor options pair each weight with its spring length. [Source](https://divinikey.com/products/keygeek-cera-x-linear-switches) |
| Keygeek Muse - (17mm Spring) | Merged/renamed to keygeek-muse; 45g / 21mm Spring; 53g / 17mm Spring | Merged into keygeek-muse. Vendor options pair each weight with its spring length. [Source](https://divinikey.com/products/keygeek-muse-linear-switches) |
| Keygeek Muse - (21mm Spring) | Merged/renamed to keygeek-muse; 45g / 21mm Spring; 53g / 17mm Spring | Merged into keygeek-muse. Vendor options pair each weight with its spring length. [Source](https://divinikey.com/products/keygeek-muse-linear-switches) |
| Keygeek Y2 (18mm/) | Merged/renamed to keygeek-y2; 37g / 22mm Spring; 45g / 20mm Spring; 53g / 20mm Spring; 63g / 18mm Spring | Merged into keygeek-y2. Spring lengths and lighter/medium/heavier labels are options of Y2. [Source](https://unikeyboards.com/products/keygeek-y2-linear-switch-factory-lubed-10pcs) |
| Keygeek Y2 - 22mm | Merged/renamed to keygeek-y2; 37g / 22mm Spring; 45g / 20mm Spring; 53g / 20mm Spring; 63g / 18mm Spring | Merged into keygeek-y2. Spring lengths and lighter/medium/heavier labels are options of Y2. [Source](https://unikeyboards.com/products/keygeek-y2-linear-switch-factory-lubed-10pcs) |
| Keygeek Y2 - (Heavier feel) | Merged/renamed to keygeek-y2; 37g / 22mm Spring; 45g / 20mm Spring; 53g / 20mm Spring; 63g / 18mm Spring | Merged into keygeek-y2. Spring lengths and lighter/medium/heavier labels are options of Y2. [Source](https://unikeyboards.com/products/keygeek-y2-linear-switch-factory-lubed-10pcs) |
| Keygeek Y2 - (Lighter Feel) | Merged/renamed to keygeek-y2; 37g / 22mm Spring; 45g / 20mm Spring; 53g / 20mm Spring; 63g / 18mm Spring | Merged into keygeek-y2. Spring lengths and lighter/medium/heavier labels are options of Y2. [Source](https://unikeyboards.com/products/keygeek-y2-linear-switch-factory-lubed-10pcs) |
| Keygeek Y2 - (Medium feel) | Merged/renamed to keygeek-y2; 37g / 22mm Spring; 45g / 20mm Spring; 53g / 20mm Spring; 63g / 18mm Spring | Merged into keygeek-y2. Spring lengths and lighter/medium/heavier labels are options of Y2. [Source](https://unikeyboards.com/products/keygeek-y2-linear-switch-factory-lubed-10pcs) |
| KNC Keys Red Jacket Redux | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/knc-keys-red-jacket-linear-v1-redux) |
| KTT Grapefruit | 3-pin; 5-pin | Retailers document both mount options. [Source](https://milktooth.com/products/grapefruit) |
| KTT Hyacinth Green | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/ktt-hyacinth-v1-green) |
| KTT Phalaenopsis | Long Stem; Standard Stem | The seller has two physically different stem lengths; neither is assigned an invented version. [Source](https://switchoddities.com/products/ktt-phalaenopsis) |
| LCET Black | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/lcet-black) |
| LCET Brown | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/lcet-brown) |
| LCET Red | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/lcet-red) |
| Longhua Black | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Longhua Blue | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Longhua Brown | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Longhua Red | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Mountain Linear | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/mountain-linear-45g) |
| Mountain Speed | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/mountain-gaming-45-speed) |
| Mountain Tactile | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Niz Plum | 35g; 45g | The manufacturer offers both dome weights. [Source](https://www.nizkeyboard.com/products/ec-switch-experience) |
| NK Dry Black | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/novelkeys-nk-dry-black) |
| NK Dry Red | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| NK Dry Yellow | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Outemu Black | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/outemu-black) |
| Outemu Blue ICE | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Outemu Dustproof Teal | Black Bottom; White Bottom | The retained collector records both bottom colors; ICE stays a separately named switch. [Source](https://switchoddities.com/products/outemu-dustproof-teal) |
| Outemu ICE All Clear | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Outemu ICE Dark Purple Tactile | V1; V2 | Contemporary owner records demonstrate tactile Dark Purple V2; clicky remains a different entry. [Source](https://www.youtube.com/watch?v=T16gBjYyNl4) |
| Outemu ICE Dustproof Blue | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Outemu ICE Dustproof Light Purple | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Outemu ICE Dustproof Teal | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Outemu ICE Silver | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Outemu Ocean | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://lumekeebs.com/products/outemu-ocean-clicky-tactile-switches) |
| PrimeKB Grey 'T1' | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Punkshoo Summertime Error | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Raeds HE - RGB | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://cannonkeys.com/products/raeds-he-switches) |
| Rapoo Blue | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/rapoo-blue) |
| Redragon Dustproof Red | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| RK Brown | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/rk-brown) |
| RK Red | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/rk-red) |
| Roselio | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Sakurio | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| SoulCat Poro | 37g; 45g | Both force options appear in the retailer selector. [Source](https://www.whatgeek.com/products/soulcat-poro-thocky-linear-switches) |
| SP-Star Meteor Purple | Grey Housing; Original Housing | The seller explicitly identifies the grey housing as an alternate to its original listing. [Source](https://switchoddities.com/products/sp-star-meteor-purple-grey-housing) |
| Strawberry Jelly Tactile | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Swirl Dustproof Blue | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Swirl Dustproof Brown | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Swirl Dustproof Red | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Tecsee Candling Dragon | V1; V2 | The retained seller describes the revised V2 top housing. [Source](https://switchoddities.com/products/tecsee-candling-dragon-v1) |
| Togar Snow Rabbit Pink | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/togar-snow-rabbit-v1-pink) |
| Togar Snow Rabbit White | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/togar-snow-rabbit-r1-white) |
| Togar Snow Wolf Blue | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/togar-snow-wolf-v1-blue) |
| Transmit Green | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| TTC Dustproof Razer Orange | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| TTC Orange Tactile | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| U4T | 62g; 68g | Both original Boba U4T weights are documented by the original seller and retained Boba U4T observations. [Source](https://www.reddit.com/r/u_hbheroinbob/comments/o999hu) |
| Varmilo Rose | V1; V2 | The manufacturer documents EC V2 Rose. [Source](https://varmilo.com/fr/pages/switches-parameter) |
| Varmilo Sakura | V1; V2 | The manufacturer documents EC V2 Sakura. [Source](https://varmilo.com/fr/pages/switches-parameter) |
| Vertex V1 | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://lumekeebs.com/products/vertex-v1-linear-switches) |
| YOK EMT | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Zealios Linear | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://raw.githubusercontent.com/ThereminGoat/switch-scores/refs/heads/master/1-Composite%20Overall%20Total%20Score%20Sheet.csv) |
| Zorro Brown | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/zorro-brown) |
| Zorro Purple | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Zorro Red | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://switchoddities.com/products/zorro-red) |
| Everglide Tourmaline Blue Pro! | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Krazy Top/Clear Base | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Phoenix Manual Declaw | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Phoenix Retooled Base | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Red Noppoo Top/Clear Base | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| White Noppoo Top/Clear Base | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Zealio Milky Bottom Error | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |
| Zilent Cloudy Top | Variants field omitted | No second option established for this exact named switch in the checked records; omit the one-option selector. Original observations remain in sources.json. [Source](https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv) |

## Retired switch records

The following JSON preserves all five removed entries and their original source bindings. It is an archival snapshot, not active import input; source exclusions remain in overrides.json.

```json
{
  "reviewedAt": "2026-09-22",
  "reason": "User requested removal of all open-slot entries, TTC Flame (Slotted), and TTC Flame Red Half Height LED.",
  "entries": [
    {
      "id": "outemu-silent-lemon-open-slot",
      "name": "Outemu Silent Lemon (Open Slot)",
      "manufacturer": "Outemu",
      "brand": "Outemu",
      "switchType": "tactile",
      "switchFamily": "MX",
      "variants": [
        "V1",
        "V2",
        "V3"
      ]
    },
    {
      "id": "outemu-silent-peach-open-slot",
      "name": "Outemu Silent Peach (Open Slot)",
      "manufacturer": "Outemu",
      "brand": "Outemu",
      "switchType": "linear",
      "switchFamily": "MX",
      "variants": [
        "V1",
        "V2",
        "V3"
      ]
    },
    {
      "id": "ttc-flame-open-slot-no-cond",
      "name": "TTC Flame (Open Slot, No Cond.)",
      "manufacturer": "TTC",
      "switchType": "linear",
      "switchFamily": "MX"
    },
    {
      "id": "ttc-flame-red-half-height-led",
      "name": "TTC Flame Red Half Height LED",
      "manufacturer": "TTC",
      "switchType": "linear",
      "switchFamily": "MX"
    },
    {
      "id": "ttc-flame-slotted",
      "name": "TTC Flame (Slotted)",
      "switchType": "linear",
      "switchFamily": "MX"
    }
  ],
  "bindings": [
    {
      "source": "theremingoat",
      "sourceId": "2908",
      "kind": "switches",
      "id": "ttc-flame-open-slot-no-cond",
      "url": "https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv",
      "observed": {
        "name": "TTC Flame (Open Slot, No Cond.)",
        "manufacturer": "TTC",
        "switchType": "linear"
      }
    },
    {
      "source": "theremingoat",
      "sourceId": "3228",
      "kind": "switches",
      "id": "outemu-silent-peach-open-slot",
      "url": "https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv",
      "observed": {
        "name": "Outemu Silent Peach V3 (Open Slot)",
        "manufacturer": "Outemu",
        "switchType": "linear"
      }
    },
    {
      "source": "theremingoat",
      "sourceId": "3229",
      "kind": "switches",
      "id": "outemu-silent-lemon-open-slot",
      "url": "https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv",
      "observed": {
        "name": "Outemu Silent Lemon V3 (Open Slot)",
        "manufacturer": "Outemu",
        "switchType": "tactile"
      }
    },
    {
      "source": "switchoddities",
      "sourceId": "9379550724380",
      "kind": "switches",
      "id": "ttc-flame-slotted",
      "url": "https://switchoddities.com/products/ttc-flame-slotted",
      "observed": {
        "name": "TTC Flame (Slotted)"
      }
    },
    {
      "source": "theremingoat",
      "sourceId": "1597",
      "kind": "switches",
      "id": "ttc-flame-red-half-height-led",
      "url": "https://drive.google.com/uc?export=download&id=1lEsJaTX4nwtxcx2WL1EcwuwokWyqnuDv",
      "observed": {
        "name": "TTC Flame Red Half Height LED",
        "manufacturer": "TTC",
        "switchType": "linear"
      }
    }
  ]
}
```

## Switch attribute combinations follow-up

Reviewed 2026-09-22. This follow-up supersedes the corresponding rows in the earlier switch review. Audited all remaining multi-option switch lists for mixed axes, overlapping descriptions, and repeated force values. The flat string-array schema is unchanged. Each combined label describes an observed configuration; never take a Cartesian product of version, mount, force, housing, or stem values. `Standard` below names the unqualified vendor option, not an inferred material or color.

| Entry | Accepted variants | Evidence / decision |
|---|---|---|
| `bsun-red-panda` | `3-pin`, `5-pin` | The collector documents both mount options and different molds. V1 is not a separate option alongside 5-pin. [Source](https://www.theremingoat.com/blog/the-pandaverse). |
| `cherry-mx-blue` | `3-pin`, `5-pin` | Clicky is the mechanism shared by both mount options, not a third option. Original retained observations in `sources.json`. |
| `hmx-yogurt-s` | `37g Actuation / 43g Bottom-out`, `45g Actuation / 53g Bottom-out` | The short weight labels duplicate the two complete operating/bottom-out force pairs. [Source](https://divinikey.com/products/hmx-yogurt-s-linear-switches). |
| `mz-peripherals-y1` | `28g Actuation / 36g Bottom-out`, `42g Actuation / 48g Bottom-out`, `49g Actuation / 57g Bottom-out` | Three source-listed force pairs, not six independent weights. [Source](https://switchoddities.com/products/mz-peripherals-y1-28-36g). |
| `alps-skcm-blue` | `Logo / White Plate`, `No Logo / Black`, `No Logo / White` | Restore the combined logo/plate descriptions in the three original collector names. Original retained observations in `sources.json`. |
| `naevy` | `V1 / POM Stem`, `V1 / UHMWPE Stem`, `V1.5`, `V2` | The first-hand review distinguishes stock V1 POM from the aftermarket UHMWPE-stem build. No stem choices inferred for later versions. [Source](https://www.theremingoat.com/blog/naevy-v15-switch-review). |
| `jixian-white` | `V2 / RGB Bottom`, `V2 / Standard`, `V3` | Standard identifies the unqualified V2 seller listing, distinct from the collector RGB-bottom listing; it does not imply a housing material. [Source](https://switchoddities.com/products/jixian-white-v2). |
| `primekb-t1-translucent` | `62g / Standard`, `67g / Grey`, `67g / Standard` | Standard identifies the two unqualified seller listings. The separately named Grey option is only observed at 67g; no 62g/Grey combination inferred. [Source](https://switchoddities.com/products/copy-of-copy-of-primekb-t1-translucent-67g-grey). |
| `jixian-blue` | `Black Bottom`, `White Bottom` | White Bottom and White Bottom Housing are identical descriptions. Original retained observations in `sources.json`. |
| `rapoo-black` | `Clear Top / Black Bottom`, `Clear Top / White Bottom`, `Full Black Housing` | Three explicitly named retailer configurations cover the shorter collector housing descriptions. [Source](https://switchoddities.com/products/rapoo-black-full-black-housing). |
| `rapoo-brown` | `Clear Top / Black Bottom`, `Full Black Housing` | Clear/Black duplicates Clear Top/Black Bottom; retain the separately listed black housing. [Source](https://switchoddities.com/products/rapoo-brown-clear-top-black-bottom). |
| `zorro-black` | `Clear Top / Black Bottom`, `White Bottom` | Black Bottom is a partial description of the explicitly listed clear-top option, not an additional established option. [Source](https://switchoddities.com/products/zorro-black-clear-top-black-bottom). |
| `sp-star-meteor-white` | `Grey Housing`, `Original Housing` | Gray and Grey Housing duplicate the alternate housing; seller explicitly contrasts it with the original. [Source](https://switchoddities.com/products/sp-star-meteor-white-grey-housing). |
| `ajazz-black` | Omitted | Only the clear-top/black-bottom configuration was established. The shorter Black Bottom observation does not establish a second choice; omit the resulting singleton after search. [Source](https://switchoddities.com/products/ajazz-black-clear-top-black-bottom). |
| `swirl-blue` | Omitted | White Bottom and Clear/White Housing do not establish two different products. No additional configuration was found; omit the unsubstantiated choice list while preserving all observations. Original retained observations in `sources.json`. |
| `ttc-flaming-snow` | `V1 / POK Stem`, `V2 / PC Stem` | Seller original POK listing includes a FlameSnowV1 product image; the V2 listing specifies a PC stem. Keep only these observed pairings. [Source](https://thockfactory.com/tc/products/ttc-flaming-snow-v2-switch). |
| `ttc-red` | `Clear Top Housing (Version Unspecified)`, `V2` | Remove inferred standalone V1. Sources do not establish the version of the clear-top specimen or V2 housing; the unresolved descriptions remain explicit. Original retained observations in `sources.json`. |
| `gateron-cap-brown` | `Milky Housing (Version Unspecified)`, `V2` | Remove inferred standalone V1. Historical Milky observation is unversioned; current manufacturer distinguishes Brown from Milky Brown. Do not fabricate a version/housing cross-product. [Source](https://www.gateron.co/products/gateron-cap-switch-set). |

Coverage limits: TTC Red and historical Gateron CAP Brown housing/version associations remain unresolved and are labelled as such; a V2 observation alone does not justify adding V1. Other partial collector descriptions (for example Jixian Black top-housing variant 2 versus White Bottom, and Content/Greetech/BSUN black-bottom versus full-black housing) are retained because they may distinguish clear and opaque tops. They were not combined without evidence. Unversioned historical weights in Healio and early KTT Wine Red likewise must not be assigned a guessed version. No original source observations were deleted.

## Specific switch colors and component labels

Reviewed 2026-09-22. These decisions supersede the matching earlier rows. Durock Sea Glass retains its five plain color names, as requested. Seller photographs were inspected directly; no housing materials or numbered releases were inferred from color.

| Entry | Accepted labels | Evidence |
|---|---|---|
| `jixian-black` | `Black Bottom`, `White Bottom` | Remove Top Housing Variant 2. Mechbox product photo shows a clear top and black bottom; SwitchOddities photo shows a clear top and white bottom. [Source](https://mechbox.co.uk/products/jixian-black-switch). |
| `alps-skcm-orange` | `Logo / White Switchplate`, `No Logo / Grey Switchplate`, `No Logo / White Switchplate` | Restore all three combinations explicitly named in retained collector observations and switchplate notes. See retained `sources.json` observations. |
| `alps-skcm-blue` | `Logo / Long White Switchplate`, `No Logo / Black Switchplate`, `No Logo / White Switchplate` | Retained collector notes identify the colored component as the switchplate and the logo specimen as a long white switchplate. See retained `sources.json` observations. |
| `dareu-low-profile-red` | `Black`, `White / Attached Stabilizer` | Collector explicitly notes an attached stabilizer on White. Which component Black/White describes remains unverified; do not relabel them as housing or stem colors. See retained `sources.json` observations. |
| `primekb-t1-translucent` | `62g / Red Stem`, `67g / Grey Stem`, `67g / Red Stem` | Inspected all three retailer product photos: 62g and unqualified 67g have red stems; the Grey listing has a grey stem. Replaces editorial Standard labels. [Source](https://switchoddities.com/products/primekb-t1-translucent-62g). |
| `jixian-white` | `V2 / Clear Top / White Bottom`, `V2 / RGB Bottom`, `V3` | Inspected the V2 seller photo, which shows a clear top and white bottom. RGB Bottom remains the independent collector description; its exact physical difference is not established. [Source](https://switchoddities.com/products/jixian-white-v2). |
| `ktt-hyacinth` | `V1 / Green Stem`, `Yellow Stem` | Seller photo of the unnumbered Hyacinth shows a yellow stem. Keep that specimen unversioned rather than assigning a release from its color. [Source](https://switchoddities.com/products/ktt-hyacinth). |
| `sp-star-meteor-purple` | `Blue Housing`, `Grey Housing` | Inspected seller photo of the original Meteor Purple: blue top and bottom housing. [Source](https://switchoddities.com/products/sp-star-meteor-purple). |
| `sp-star-meteor-white` | `Blue Housing`, `Grey Housing` | First-hand review explicitly distinguishes the original dark blue housing from the later light grey housing. [Source](https://nextrift.com/202117752/). |

Photo evidence: [Jixian Black black bottom](https://cdn.shopify.com/s/files/1/2782/6684/products/jixian-black-switch-sample-342.png?v=1649102907), [Jixian Black white bottom](https://cdn.shopify.com/s/files/1/0708/5804/7772/files/JixianBlack2.jpg?v=1697493388), [PrimeKB 62g red stem](https://cdn.shopify.com/s/files/1/0708/5804/7772/products/IMG_20230307_170557.jpg?v=1678482687), [PrimeKB 67g red stem](https://cdn.shopify.com/s/files/1/0708/5804/7772/products/IMG_20230307_170627.jpg?v=1678482753), [PrimeKB 67g grey stem](https://cdn.shopify.com/s/files/1/0708/5804/7772/products/IMG_20230307_170655.jpg?v=1678482829).

Still unresolved: DareU color-component identity; Jixian White RGB-bottom geometry; the historical housing/version association for Gateron CAP Brown and TTC Red. Their remaining source descriptions are retained without invented pairings. MMD Original labels identify unnumbered source listings with verified weights and are not silently converted to V1. Original collector notes, including the retired Top Housing Variant 2 wording, remain as provenance only.

## PrimeKB T1 and Greetech family consolidation

Reviewed 2026-09-22. Supersedes previous PrimeKB housing-specific decisions.

- PrimeKB T1: one `primekb-t1` entry, with exactly `62g / Red Stem`, `65g / Red Stem`, and `67g / Grey Stem`, as explicitly requested. Merge the red, grey, opaque, and translucent entries. Opaque/translucent are no longer separate catalog choices. Retain all eight original observations unchanged, including the seller-labelled translucent 67g red specimen; the accepted three-option list is the user-curated selection, not a claim that the source observation never existed.
- Greetech Black, Blue, and Brown: one entry per switch color/mechanism. Combine housing, pin count, and LED compatibility from explicitly named source products. OG Black/Brown are retained as `OG / 3-pin` and `OG / 5-pin` variants; their seller describes a newer release, so do not silently equate them with older housing variants.
- More specific source configurations absorb overlapping partial labels such as Black Housing, Black Bottom, Clear/White, and pin-only child names; these do not become extra choices. Black retains Clear Top / Black Bottom without an invented pin count or LED compatibility. All resulting lists have multiple real options; no standalone singleton attributes or Cartesian products are added. Source observations are preserved through ID rebinding, and explicit entry overrides pin the accepted lists on reimport.

Sources: retained SwitchOddities product names in `sources.json`; [Greetech OG Black](https://switchoddities.com/products/greetech-og-black-3-pin), [Greetech OG Brown](https://switchoddities.com/products/greetech-og-brown-3-pin), [Greetech Brown clear/white SMD plate-mount specimen](https://www.simshadows.com/tech/mechanical-keyboard-switch-collection/). Plate mount identifies the 3-pin option.

## Simplified Greetech variants

Reviewed 2026-09-22. Supersedes the preceding Greetech variant lists per user instruction: through-hole, SMD, and OG are not selectable distinctions. Black/Brown OG observations map to the corresponding full-black housing and pin-count options; no bare pin-count or OG singleton is retained. Black clear/white 3-pin LED variants collapse to one choice. Green and Red housing-specific entries merge into their color parents. Chroma-labelled Razer Green/Orange observations merge into the respective Greetech Razer Green/Orange families; with lighting distinctions omitted, there is no established second selectable option, so both entries omit variants. Razer-branded families remain distinct from generic Greetech colors, and Razer Yellow and Sunset remain unchanged.

Green additionally has a documented [clear-top/white-bottom plate-mount option](https://www.mechanicalkeyboards.co.id/products/detail/greetech-gt02-green-smd-rgb-switch-tactile-click-plate-mount), retained as 3-pin without its LED label. [The first-hand OG review](https://www.theremingoat.com/blog/greetech-og-brown-switch-review) establishes full-black housings and both 3-/5-pin mounts for OG Black/Brown. Red Clear/White remains without an invented pin count. Original observations retain their complete source wording, including lighting and OG details, for provenance only.

- `greetech-black`: `Clear Top / Black Bottom`, `Clear Top / White Bottom / 3-pin`, `Full Black Housing / 3-pin`, `Full Black Housing / 5-pin`.
- `greetech-blue`: `Clear Top / White Bottom / 3-pin`, `Clear Top / White Bottom / 5-pin`, `Full Black Housing / 3-pin`.
- `greetech-brown`: `Clear Top / White Bottom / 3-pin`, `Full Black Housing / 3-pin`, `Full Black Housing / 5-pin`.
- `greetech-green`: `Clear Top / White Bottom / 3-pin`, `Full Black Housing / 3-pin`.
- `greetech-red`: `Clear Top / White Bottom`, `Full Black Housing / 3-pin`.
- `greetech-razer-green`: no variants field.
- `greetech-razer-orange`: no variants field.

## Pin-count display policy

Reviewed 2026-09-22. Remove 3-/5-pin wording from all accepted names and IDs; merge identities that differ only by pin count. Keep pin counts in variants only when every option has an explicit count and both 3-pin and 5-pin occur. Otherwise remove counts from every option, deduplicate, and omit empty/singleton variant lists. Never fill unknown counts. A generic parent entry without variants is not an additional unknown-pin choice when explicit pin-specific children supply the options. This supersedes earlier Greetech pin lists: Black, Green, and Red lose counts; Blue and Brown retain their fully specified 3-/5-pin choices.

Changes are pinned in entry overrides and source bindings, with original observations preserved. 39 resulting entries changed; 10 redundant entries merged.

- `bsun-blue-panda`: no variants field.
- `bsun-mint-panda`: no variants field.
- `bsun-white-panda`: no variants field.
- `cherry-mx-black`: no variants field.
- `cherry-mx-green`: no variants field.
- `cherry-mx-hyperglide-blue`: no variants field.
- `cherry-mx-hyperglide-brown`: `3-pin`, `5-pin`.
- `cherry-mx-hyperglide-rgb-black`: no variants field.
- `cherry-mx-hyperglide-rgb-blue`: no variants field.
- `cherry-mx-hyperglide-rgb-brown`: no variants field.
- `cherry-mx-hyperglide-rgb-red`: no variants field.
- `cherry-mx-hyperglide-silent-black`: no variants field.
- `cherry-mx-hyperglide-silent-red`: no variants field.
- `cherry-mx-hyperglide-speed-silver`: no variants field.
- `cherry-mx-lock-grey-black`: no variants field.
- `cherry-mx-rgb-black`: no variants field.
- `cherry-mx-rgb-ergo-clear`: no variants field.
- `cherry-mx-rgb-nature-white`: no variants field.
- `cherry-mx-rgb-silent-red`: no variants field.
- `cherry-mx-silent-black`: no variants field.
- `cherry-mx-tactile-grey`: no variants field.
- `cherry-mx-white`: no variants field.
- `cherry-mx2a-rgb-ergo-clear`: no variants field.
- `cherry-mx2a-rgb-purple`: no variants field.
- `drop-holy-panda-x`: `3-pin`, `5-pin`.
- `drop-holy-panda-x-clear`: `3-pin`, `5-pin`.
- `gateron-jupiter-banana-two-stage-spring`: `3-pin`, `5-pin`.
- `gateron-jupiter-brown`: `3-pin`, `5-pin`.
- `gateron-jupiter-red`: `3-pin`, `5-pin`.
- `greetech-black`: `Clear Top / Black Bottom`, `Clear Top / White Bottom`, `Full Black Housing`.
- `greetech-green`: `Clear Top / White Bottom`, `Full Black Housing`.
- `greetech-red`: `Clear Top / White Bottom`, `Full Black Housing`.
- `holy-bsun-blue-panda`: `Clear Stem`, `True Stem`.
- `holy-bsun-green-panda`: `Clear Stem`, `True Stem`.
- `holy-bsun-red-panda`: `Clear Stem`, `True Stem`.
- `holy-bsun-white-panda`: `Clear Stem`, `True Stem`.
- `holy-bsun-yellow-panda`: `Clear Stem`, `True Stem`.
- `holy-bsun-translucent-panda`: `Clear Stem`, `True Stem`.
- `ktt-mango-sago`: no variants field.

## Weight names, Jellyfish, Camo Camel, and Raw cleanup

Reviewed 2026-09-22. Remove Linear from variant labels (the switchType field already specifies it). Restore Cherry MX RGB Black 3-/5-pin choices per user correction. Rename Kailh Jellyfish Box - X to Kailh Box Jellyfish (X), including its ID. Aliaz Silent weight children merge into 60g/70g/80g/100g options; duplicated Kangaroo Box Ink weight children merge into 59g/63g options. Clione Limacina linear/tactile remain distinct mechanisms, with 58 gf removed from names/IDs and no singleton weight list: the retained Keychron selector offers only 58 gf for each.

All four Camo Camel records, including T. Camo Camel, merge into Keychron Camo Camel as requested; Patt. labels are excluded from selectable variants, not deleted from historical source observations. Keygeek Raw merges flat-/round-pole children into one parent with Flat Pole and Round Pole choices, supported by the [Divinikey selector](https://divinikey.com/products/keygeek-raw-linear-switches). Weight evidence: original retained Gateron/Keychron selectors in sources.json, and [Clione Limacina](https://www.keychron.com/products/kailh-clione-limacina-linear-switch).

Jellyfish X also repairs its pre-existing stale source binding to kailh-jellyfish-box-x-clicky, now pointing to kailh-box-jellyfish-x.

## Jellyfish, Budgerigar, and specimen annotations

Reviewed 2026-09-22. Merge Kailh Box Jellyfish (X) into Kailh Box Jellyfish with V1/V2 labels; preserve the conflicting-but-real mechanism options using the existing linear/clicky type, as the retained V2 specimen is linear and X is explicitly clicky. Y remains a separate entry. The [seller lists both mechanisms for V2](https://kineticlabs.com/shop/kailh). Merge both misspelled Budgreigar records into correctly spelled Epomaker Budgerigar, retaining V1/V1.1/V2 without mold wording.

Remove Manu Err/Error, New Mold, and other identified defect annotations from accepted identities. Cream Soda, Cloud Blue, Summertime, Aqua Zilent, and Zealio specimens merge into their existing parent entries without adding defect variants. Drop x Invyr Holy Panda, Maroon Durock, Bobagum Monroe, Kailh Berry Tactile, and Punkshoo Summertime BX retain their remaining model descriptors after cleanup. Original source observations and defect notes remain unchanged for provenance.

SWK Error is not rewritten to the meaningless manufacturer-only name SWK: its sole observation does not identify a base model or establish that Error is an annotation. It remains unresolved. Manufacturer credits containing Patent Molds and designer credits for Manu are not specimen annotations and remain intact.

## Jellyfish mechanism correction

Reviewed 2026-09-22. User correction supersedes the preceding Jellyfish merge: Kailh Box Jellyfish and Kailh Box Jellyfish (Y) are one linear entry, kailh-box-jellyfish, retaining V1/V2. Kailh Box Jellyfish (X) is restored as a distinct clicky entry, kailh-box-jellyfish-x, without a singleton variant. Source bindings are reassigned by their original X/Y labels; the V2 collector observation stays with the linear parent.

## Duplicated Kailh Box prefix

Reviewed 2026-09-22. The three Kailh Box - Box Brown/Red/White records came from keychronswitches product 7179510186073, whose retained source names concatenate Kailh Box V2 with option labels Box Brown/Red/White. Version consolidation removed V2 from the name but retained the repeated Box prefix, leaving redundant identities. Merge kailh-box-box-brown/red/white into kailh-box-brown/red/white, preserving V1/V2 options and rebinding all original observations. Explicit source binding overrides prevent these three known records from recreating duplicates.

Jellyfish naming follow-up (2026-09-22): user prefers Kailh Box Jellyfish (Y) / kailh-box-jellyfish-y for the merged linear entry, retaining V1/V2; X remains the separate clicky entry. Renamed the parent and all source/override references accordingly.

## Historical import coverage note

The initial simple-variant pass combined 519 groups. At that checkpoint, 79 pre-existing missing switch IDs affected 83 source bindings; six Alps mappings were repaired, leaving 73 IDs / 77 bindings unresolved. These are historical counts, not a fresh validation result. Later corrections are recorded above. Full accepted-state validation may still report pre-existing stale mappings; focused replay checks cover the reviewed entries. Reviewed historical keycap pages use reviewedkeycaps source bindings with original observations, HTTPS evidence, and dated notes; they survive replay without a live fetch adapter.

## TP-1 Dieter metadata

Reviewed 2026-09-22 against [NovelKeys specifications and kit selector](https://novelkeys.com/products/tp-1-dieter-keycaps) and [Geistmaschine profile documentation](https://geistmaschine.io/pages/tp-1). Manufacturer Keyreative; brand/design Geistmaschine, with biip credited for novelty designs; uniform TP-1 profile; dye-sublimated PBT/fiberglass compound. The existing fields carry manufacturer, brand, designer, profile, material and literal kit labels. No schema fields were added for printing method or profile geometry. Preserve the original NovelKeys observation and pin reviewed metadata through overrides.

## Realforce replacement keycaps

Reviewed 2026-09-22. Retained after user confirmation: [R3 keycap set](https://mechanicalkeyboards.com/products/topre-realforce-r3-keycap-set) and [RC1 keycap set](https://mechanicalkeyboards.com/products/topre-realforce-rc1-keycap-set) explicitly sell keycaps only, not keyboards. Names now say Replacement Keycaps. R3/RC1 identify compatible keyboard models; R3 must not create R1/R2/R3 release variants. Use literal retailer kit/color combinations and PBT material. The RC1 selector lacks Dark Grey Text Modifiers, so that unoffered combination is not inferred. Original source observations remain unchanged.

## TFUE keycap identity

Reviewed 2026-09-23. The retained NovelKeys source [TFUE Keycaps](https://novelkeys.com/products/tfue-keycaps) is a real standalone dye-sublimated PBT keycap set with Cherry profile and MX compatibility. Add the supported profile and material fields. The page does not establish a factory or designer credit, so those fields remain absent. This is not the separate Ducky x Tfue doubleshot set. No second option is established, so variants stays omitted. Printing method and compatibility remain in this note because the catalog schema has no dedicated fields for them.

## KBParadise compatibility models

Reviewed 2026-09-23. V60 and V80 denote compatible keyboards, not release versions. Remove inferred predecessor lists from KBParadise ALPS V60 Vintage Blank, KBParadise ALPS V80 Vintage, and KBParadise MX V60 Black Blank; retain model names and IDs. The retailer describes the [ALPS V60](https://mechanicalkeyboards.com/products/kbparadise-alps-v60-vintage-61-key-blank-abs-keycap-set), [ALPS V80](https://mechanicalkeyboards.com/products/kbparadise-alps-v80-vintage-oem-profile-abs-keycap-set), and [MX V60](https://mechanicalkeyboards.com/products/kbparadise-mx-v60-black-abs-keycaps-blank) as individual sets. No alternate options are established. Pin omitted variants and exempt these compatibility models from release inference. An audit of both accepted catalogs, including names, IDs, and options with V10+/R10+ or spelled-out version/round labels, found no other matches.

## Sparse-entry metadata review

Researched and applied 2026-09-23. Research was initially held for the user's commit; the subsequent request to add metadata authorized applying the supported fields below. Accepted metadata is pinned in overrides; original source observations remain unchanged. Scope: entries containing only id/name and at most one other field; no switch entries matched.

Findings below come from existing product URLs, their full specification sections, and explicitly linked supplementary sources. Unknown factories/designers remain unknown. Do not infer a factory from a seller, copy navigation/recommended-product metadata, expand compatibility model numbers, or apply artisan-only specifications to full sets. Conflicting profiles remain unset for Akko Shiny Kitten and Steam Engine Cyrillic. Work Louder Wrk. gains its brand only; TRIFL gains its confirmed profile while its final material remains pending. NicePBT and CannonCaps manufacturer labels follow the existing catalog convention and the vendor specifications; these identify product lines rather than establish an underlying factory. Biip's PBoW sublegend credit remains in these notes. New material combinations use the existing slash notation. Names, IDs, and variant lists are unchanged.

### Supported metadata and caveats

| Existing entry | Evidence and caveats for applied metadata | Evidence |
|---|---|---|
| Akko Black & Cyan Cyrillic | Material PBT; profile ASA. | [Source 1](https://akkogear.eu/products/black-cyan-cyrillic-keycap-set-98-key) |
| Akko Black & Gold Cyrillic | Material PBT; profile ASA. | [Source 1](https://akkogear.eu/products/black-gold-russian-layout-cyrillic-keycap-68-key), [Source 2](https://akkogear.eu/products/black-gold-cyrillic-keycap-set-98-key) |
| Akko Black & Gold ISO Nordic | Material PBT; profile Cherry. | [Source 1](https://akkogear.eu/products/black-gold-iso-nordic-keycap-set-76-key) |
| Akko Black & Pink Cyrillic | Material PBT; profile ASA. | [Source 1](https://akkogear.eu/products/black-pink-cyrillic-keycap-set-68-key), [Source 2](https://akkogear.eu/products/black-pink-cyrillic-keycap-set-110-key) |
| Akko Black & Pink Gradient | Material PBT; profile Cherry. | [Source 1](https://akkogear.eu/products/black-pink-gradient-keycap-set-135-key) |
| Akko Blue Gradient | Material PBT; profile Cherry. | [Source 1](https://akkogear.eu/products/blue-gradient-keycap-set-135-key) |
| Akko Blue on White Cyrillic | Material PBT; profile ASA. | [Source 1](https://akkogear.eu/products/blue-on-white-cyrillic-keycap-set-68-key), [Source 2](https://akkogear.eu/products/blue-on-white-cyrillic-keycap-set-97-key), [Source 3](https://akkogear.eu/products/blue-on-white-cyrillic-keycap-set-88-key), [Source 4](https://akkogear.eu/products/blue-on-white-cyrillic-keycap-set-84-key) |
| Akko Blue on White ISO Spare | Material PBT; profile Cherry. | [Source 1](https://akkogear.eu/products/blue-on-white-iso-nordic-keycap-set-49-key) |
| Akko Forest Gradient | Material PBT; profile Cherry. | [Source 1](https://akkogear.eu/products/forest-gradient-keycap-set-135-key) |
| Akko Glassy Carbon Fiber | Material PC+ABS; profile Cherry. | [Source 1](https://akkogear.eu/products/glassy-carbon-fiber-keycap-set-126-key) |
| Akko Greedy Bear | Material PBT + PC; Cherry profile. | [Source 1](https://akkogear.eu/products/greedy-bear-keycap-set-119-key) |
| Akko Horizon ISO Nordic | Material PBT; profile Cherry. | [Source 1](https://akkogear.eu/products/horizon-iso-nordic-keycap-set-95-key) |
| Akko Love | PC; Cherry profile. Decorative Love-shaped kit; do not assume a complete keyboard set. | [Source 1](https://akkogear.eu/products/love-keycaps) |
| Akko Orange on Black ISO-DE/UK/Nordic | Material PBT; profile Cherry. | [Source 1](https://akkogear.eu/products/orange-on-black-iso-de-uk-nordic-keycap-set-210-key) |
| Akko PC Transparent | PC; Cherry profile. | [Source 1](https://akkogear.eu/products/akko-pc-transparent-keycap-set-82-key) |
| Akko Purple Gradient | Material PBT; profile Cherry. | [Source 1](https://akkogear.eu/products/purple-gradient-keycap-set-135-key) |
| Akko Quantum Horizon | PC; Cherry profile; co-created by Akko and OSHID. | [Source 1](https://akkogear.eu/products/quantum-horizon-keycap-set-126-key) |
| Akko Shine-Through Black | Material PBT; profile OEM. | [Source 1](https://akkogear.eu/products/shine-through-black-keycap-set-61-key) |
| Akko Shine-Through White | Material PBT; profile OEM. | [Source 1](https://akkogear.eu/products/shine-through-white-keycap-set-61-key) |
| Akko Shiny Kitten | PBT + PC confirmed. Profile conflict: feed description says Cherry, full-page specification says MAO. Resolve before editing. | [Source 1](https://akkogear.eu/products/shiny-kitten-keycap-set-113-key) |
| Akko Steam Engine Cyrillic | PBT confirmed. Profile conflict: feed description says ASA, full-page specification says Cherry. Resolve before editing. | [Source 1](https://akkogear.eu/products/steam-engine-cyrillic-keycap-set-108-key) |
| Akko The King's Avatar | Material PBT; profile MOA. | [Source 1](https://akkogear.eu/products/the-kings-avatar-keycap-set-137-key) |
| Akko Timeline | Material PBT; profile Akko ACA. | [Source 1](https://akkogear.eu/products/timeline-keycap-set-151-key) |
| CannonKeys x Tamagotchi | Cherry; PBT. Page labels NicePBT as manufacturer; confirm catalog treatment of this product line versus factory. Exclude keyboard/deskmat purchase options. | [Source 1](https://cannonkeys.com/products/tamagotchi) |
| CannonKeys x Tamagotchi (Cheffy Edition) | Cherry; PBT. Page labels NicePBT as manufacturer; confirm catalog treatment of this product line versus factory. Exclude keyboard/deskmat purchase options. | [Source 1](https://cannonkeys.com/products/cannonkeys-x-tamagotchi-cheffy-edition) |
| Cherry Charcoal | PBT confirmed by NovelKeys specifications. | [Source 1](https://novelkeys.com/products/cherry-charcoal) |
| Cherry Flowershop | PBT confirmed by NovelKeys specifications. | [Source 1](https://novelkeys.com/products/cherry-flowershop) |
| Cherry Industrial Keys | PBT confirmed by NovelKeys specifications. Designer biip. | [Source 1](https://novelkeys.com/products/cherry-industrial-keys) |
| Cherry Olivia | PBT confirmed by NovelKeys specifications. Designer Olivia. | [Source 1](https://novelkeys.com/products/cherry-olivia) |
| Cherry PBoW | PBT confirmed by NovelKeys specifications. biip credited for sublegends specifically; do not imply sole set designer. | [Source 1](https://novelkeys.com/products/cherry-pbow) |
| Cherry Sage | PBT confirmed by NovelKeys specifications. Designer dotnick. | [Source 1](https://novelkeys.com/products/cherry-sage) |
| CXA Pine | CXA; ABS/PBT blend; designed by CannonKeys; page labels CannonCaps as manufacturer (product-line/factory distinction needs care). | [Source 1](https://cannonkeys.com/products/cxa-pine) |
| Dimensional | GMK; ABS; CYL (canonical Cherry profile); designer LittleAad. Check existing GMK CYL Dimensional identity before enriching or renaming. | [Source 1](https://omnitype.com/products/gmk-dimensional-cyl) |
| Doodlecaps Nakseo | Cherry; PBT; Swagkeys Doodlecaps line. | [Source 1](https://swagkeys.com/products/doodlecaps-nakseo) |
| Doodlecaps Nakseo Black | Swagkeys Doodlecaps line; PBT retained. Profile not explicitly established on fetched Black page. | [Source 1](https://swagkeys.com/products/doodlecaps-nakseo-black) |
| Doys Dat.1 | DOYS; PC; manufacturer Deadline Studio. | [Source 1](https://keebsforall.com/products/doys-dat-1-by-deadline-studio), [Source 2](https://deadline.space/products/in-stock-deadline-doys-dat-1-doys-pc-keycaps) |
| Epomaker AegisSil | Silicone; Cherry profile. | [Source 1](https://epomaker.com/products/epomaker-aegissil-keycaps-set) |
| Epomaker AquaShift | PBT; Cherry profile. | [Source 1](https://epomaker.com/products/epomaker-aquashift-keycaps-set) |
| Epomaker Cream Bunny | PBT; MOA profile. | [Source 1](https://epomaker.com/products/epomaker-cream-bunny-keycaps-set) |
| Epomaker DuoChrome | PBT + PC; Cherry profile. | [Source 1](https://epomaker.com/products/epomaker-duochrome-keycap) |
| Epomaker Frost Jelly | ABS + PC; Cherry profile. | [Source 1](https://epomaker.com/products/epomaker-frost-jelly-keycap-set) |
| Epomaker Glintrix | PBT + PC; Cherry profile. | [Source 1](https://epomaker.com/products/epomaker-glintrix-keycaps-set) |
| Epomaker × Human Fall Flat FALLEN Series | PBT; Cherry profile. | [Source 1](https://epomaker.com/products/epomaker-human-fall-flat-fallen-series-keycap-set) |
| Epomaker JellyPaw | ABS + PC; MAO profile. | [Source 1](https://epomaker.com/products/epomaker-jellypaw-keycap-set) |
| Epomaker Lavender Jade | PBT; Cherry profile. | [Source 1](https://epomaker.com/products/epomaker-lavender-jade-keycap-set) |
| Epomaker Lusterfly Jelly | ABS + PC; MDA profile. | [Source 1](https://epomaker.com/products/epomaker-lusterfly-jelly-keycaps-set) |
| Epomaker Meow Sushi | PBT; MOA profile. | [Source 1](https://epomaker.com/products/epomaker-meow-sushi-keycaps-set) |
| Epomaker Mist Clouds | PBT; Cherry profile. | [Source 1](https://epomaker.com/products/epomaker-mist-clouds-keycap-set) |
| Epomaker Opalux | PBT; Cherry profile. | [Source 1](https://epomaker.com/products/epomaker-opalux-keycap-set) |
| Epomaker Phos | PBT + PC; Cherry profile. | [Source 1](https://epomaker.com/products/epomaker-phos-keycap-set) |
| Epomaker Shimmeow | ABS + PC; MAO profile. | [Source 1](https://epomaker.com/products/epomaker-shimmeow-keycap-set) |
| Epomaker SmokFiber | PC; Cherry profile. | [Source 1](https://epomaker.com/products/epomaker-smokfiber-keycaps-set) |
| Epomaker Sunflower | PBT; Cherry profile. | [Source 1](https://epomaker.com/products/epomaker-sunflower-keycap) |
| Epomaker × vvyruus Retromania | PBT; Cherry profile. | [Source 1](https://epomaker.com/products/epomaker-vvyruus-retromania-keycaps-set) |
| Epomaker x LINDSEYSZN Azure Dragon SZN | PBT; MOA profile. | [Source 1](https://epomaker.com/products/epomaker-x-lindseyszn-azure-dragon-szn-keycap-set) |
| FFXIV Eorzean | PBT; manufacturer Keyreative. Profile not stated in fetched specification. | [Source 1](https://novelkeys.com/products/ffxiv-eorzean-keycaps) |
| Galactic Empire | DSA; PBT; manufacturer Signature Plastics. Full Aurebesh and Aurebesh with English Sublegends kits are described. | [Source 1](https://novelkeys.com/products/star-wars-galactic-empire-dsa-keycap-set) |
| Glorious GPBT Afterparty | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-afterparty) |
| Glorious GPBT Alpine Forest | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-alpine-forest) |
| Glorious GPBT Arctic White | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-arctic-white-dye-sublimated-keycaps) |
| Glorious GPBT Armor Grey Basics | PBT; OEM profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-armor-grey-basics-keycaps) |
| Glorious GPBT Backlit | PBT; OEM profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-backlit-keycaps) |
| Glorious GPBT Classic Black Basics | PBT; OEM profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-classic-black-basics-keycaps) |
| Glorious GPBT Classic White Basics | PBT; OEM profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-classic-white-basics-keycaps) |
| Glorious GPBT Daydream | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-daydream) |
| Glorious GPBT Epic Purple Basics | PBT; OEM profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-epic-purple-basics-keycaps) |
| Glorious GPBT High Tide | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-high-tide) |
| Glorious GPBT Ink Noir | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-ink-noir) |
| Glorious GPBT Mana Blue Basics | PBT; OEM profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-mana-blue-basics-keycaps) |
| Glorious GPBT National Parks Collection | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-national-parks-collection-keycaps) |
| Glorious GPBT Olive | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-olive-green-dye-sublimated-keycaps) |
| Glorious GPBT Potion Pink Basics | PBT; OEM profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-potion-pink-basics-keycaps) |
| Glorious GPBT Power Panel | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-power-panel) |
| Glorious GPBT Purple Haze | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-purple-haze) |
| Glorious GPBT Revive Red Basics | PBT; OEM profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-revive-red-basics-keycaps) |
| Glorious GPBT Star Sign | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-star-sign) |
| Glorious GPBT Synth Sunrise | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-synth-sunrise) |
| Glorious GPBT Synth Sunset | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-synth-sunset) |
| Glorious GPBT Totem Green Basics | PBT; OEM profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-totem-green-basics-keycaps) |
| Glorious GPBT Zero One | PBT; Cherry profile (product specification table). | [Source 1](https://www.gloriousgaming.com/products/gpbt-zero-one) |
| JKDK Studio Blue Gradient Side Print | OEM; PBT. | [Source 1](https://lumekeebs.com/products/jkdk-studio-blue-gradient-keycaps-side-print-keycap-set-134-key-rgb-compatible) |
| Keycapsule Prism | PBT; manufacturer Keyreative; designer biip. Black and white described; profile not stated in fetched specification. | [Source 1](https://novelkeys.com/products/keyreative-keycapsule-prism-keycaps) |
| Keychron K12 | ABS; OEM. | [Source 1](https://www.keychron.com/products/keychron-k12-keycap-set) |
| Keychron K3 | ABS confirmed; avoid assigning a named profile from generic curved/low-profile wording. | [Source 1](https://www.keychron.com/products/keychron-k3-keycap-set) |
| Keychron K7 | ABS confirmed; avoid assigning a named profile from generic curved/low-profile wording. | [Source 1](https://www.keychron.com/products/keychron-k7-keycap-set) |
| MCR Horseman | MCR; ABS; produced by MelGeek, designed by Muni. Base A/B/C offered. | [Source 1](https://www.melgeek.com/en-eu/products/melgeek-mcr-horseman-abs-doubleshot-keycap-set-for-mechanical-keyboard) |
| MKL Classic Black (WoB) | Leopold profile; PBT; MK/Leopold collaboration. Base, Numpad, Accents offered. | [Source 1](https://mechanicalkeyboards.com/products/mkl-keycap-set-classic-black-wob) |
| MM Glazed Beige | PBT confirmed for the keyset. Cherry R4/aluminum specifications on this page belong to separate stepped artisan keys; do not apply them to the whole set. | [Source 1](https://prototypist.net/products/in-stock-mm-glazed-beige-keycaps) |
| Moomin® Summertime | Cherry; PBT; CannonKeys collaboration. | [Source 1](https://cannonkeys.com/products/moomin®-summertime) |
| OTC 9009 | Cherry; PBT. | [Source 1](https://omnitype.com/products/otc-9009) |
| OTC Modo® Light | Cherry; PBT; designers Omnitype & Qoda Studio; Icon+Text / Icon Only offered. | [Source 1](https://omnitype.com/products/otc-modo-light) |
| PBT Office Beige | Cherry; PBT; designer Hibi, artwork Eura Bang. | [Source 1](https://omnitype.com/products/pbt-office-beige) |
| PFF Verdant Retro | PFF; PBT; official page describes CannonKeys/Keyreative collaboration. Profile designed by matt3o; retain separate colorway credit. | [Source 1](https://cannonkeys.com/products/pff-verdant-retro) |
| PGA Circus | PGA; ABS, verified retailer specification. | [Source 1](https://kprepublic.com/products/pga-sa-abs-circus-doubleshot-keycap-set-sa-profile-for-mx-stem-keyboard-60-87-104-xd64-xd68-xd84-xd87-bm60-cstc75-bm65-bm68) |
| PGA Jeans | PGA; ABS, retailer title/specification. | [Source 1](https://kprepublic.com/collections/keycaps/products/pga-sa-abs-jeans-doubleshot-keycap-set-sa-profile-for-mx-stem-keyboard-60-87-104-gh60-xd64-xd68-xd84-xd87-bm60-cstc75-bm65-bm68) |
| QK Pearl Milk Tea | Cherry; PBT; Qwertykeys product page. | [Source 1](https://www.qwertykeys.com/products/qwertykeys-keycap) |
| QK Taro Balls | Cherry; PBT; Qwertykeys product page. | [Source 1](https://www.qwertykeys.com/products/qwertykeys-keycap) |
| Series 1 Yuji and Megumi Edition | Cherry; PBT; manufacturer MONOKEI. | [Source 1](https://novelkeys.com/products/series-1-yuji-and-megumi-edition) |
| SW Antarctic | PBT + ABS; Cherry retained. | [Source 1](https://swagkeys.com/products/sw-antarctic-keycap) |
| SW BoW Accents | PBT + ABS; Cherry retained. | [Source 1](https://swagkeys.com/products/in-stock-sw-bow-accents) |
| TRIFL alpha | TRIFL uniform profile; PA12 (Nylon 12), Multi Jet Fusion printed. Designer announcement; confirm final group-buy specification before applying. | [Source 1](https://geekhack.org/index.php?topic=112653.0) |
| Work Louder Wrk. | Work Louder line; material varies by edition: Blind/Cartridge/Dime ABS, Daily/Icon/Legend coated PC, Pure frosted PC. Do not set one plastic for every edition. | [Source 1](https://keygem.com/products/work-louder-wrk-keycaps) |

### Unresolved identities and follow-up sources

These leads remain unapplied. CC ZeRo also appears to duplicate the existing CannonCaps ZeRo entry; verify and merge separately rather than independently enriching a duplicate.

| Existing entry | Remaining research | Lead |
|---|---|---|
| Berry Trackday | NuPhy Berry profile, PBT indicated by regional retailer; primary NuPhy page returned 403. Verify final specs. | [Source](https://nuphy.pl/products/berry-trackday) |
| CC ZeRo | Located likely CannonCaps product page; not yet verified. | [Source](https://cannonkeys.com/products/cannoncaps-zero-keycaps) |
| Doys PC Blanks | Located designer DOYS page, but not successfully retrieved. Do not substitute DAT.1 printed-set specifications without checking. | [Source](https://deadline.space/products/doys-keycaps) |
| HiPro EC BoW | No independently verified specification yet. | Original observation retained in sources.json |
| HiPro EC Renso | Original KBDFans page returns 404; historical seller/designer announcement found. Need archived specification. | [Source](https://www.reddit.com/r/mechmarket/comments/12d6is7) |
| HiPro EC Topre Commander, Monotone | Secondary listings mention HiPro/PBT; primary specification still needed. | Original observation retained in sources.json |
| HiPro EC Topre Commander, Two Tone | Secondary listings mention HiPro/PBT; primary specification still needed. | Original observation retained in sources.json |
| Meow Meow PBT | Located Meow-profile set sold under MOMOKA; identity against SoulCat observation needs verification. Do not confuse with Electronic/Baking Meow sets. | [Source](https://shop.yushakobo.jp/en/products/9387) |
| Meow Waon PBT | Waon linked from Meow retailer page; identity/brand against SoulCat observation needs verification. | [Source](https://shop.yushakobo.jp/en/products/9387) |
| NCC Baja | Aeternus announcement describes NovusCaps, Cherry, PBT; verify original author and final product before applying. | [Source](https://www.reddit.com/r/mechmarket/comments/t50wzn) |
| PFF WoB | Official page located; detailed specification still to inspect. | [Source](https://cannonkeys.com/products/pff-wob) |
| TK Chalk | Official page located; detailed specification still to inspect. | [Source](https://terrakeycaps.com/products/tk-chalk-keycap-set) |
| URSA Black/Blue/Red | URSA profile confirmed at family level. Current family page mixes PBT and ABS by supported layout; verify historical colorway material and exact identity. | [Source](https://fkcaps.com/products/ursa-keycaps) |
| URSA Classic | URSA profile confirmed at family level. Current family page mixes PBT and ABS by supported layout; verify historical colorway material. | [Source](https://fkcaps.com/products/ursa-keycaps) |
| URSA MiniCom | URSA profile confirmed at family level. Current family page mixes PBT and ABS by supported layout; verify MiniCom material. | [Source](https://fkcaps.com/products/ursa-keycaps) |
| WRK Cartridge | Work Louder retailer describes Cartridge as ABS with biip credit; check identity against parent Wrk. before creating overlapping metadata. | [Source](https://keygem.com/products/work-louder-wrk-keycaps) |
