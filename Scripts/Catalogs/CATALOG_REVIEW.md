# Catalog review notes

Updated: 2026-09-24

Accepted decisions and remaining research, grouped by category. Superseded decisions and raw record archives are omitted; the catalogs contain the full accepted option lists.

## Catalog conventions

- Keep the existing entry structure with an optional flat variants list. Combine related attributes into observed configurations such as V2 / 62g; do not generate combinations from independent lists.
- Omit singleton selectors. Shared properties such as Linear or a common spring type are not additional choices.
- Move selectable rounds, weights, kits, and other options out of parent names and IDs. Preserve meaningful product, profile, mechanism, and compatibility distinctions.
- Normalize round/version labels to uppercase. Sculpt rows, profile names, and keyboard model numbers are not release sequences. Do not invent earlier switch versions from a later version alone.
- Remove 3-/5-pin wording from names and IDs. Retain it in variants only when every option has a known pin count and both counts occur; otherwise remove all pin labels and deduplicate.
- Remove incidental manufacturing-error, replacement-top, new/old-pin, and mold annotations. Distance options use labels such as 3.7mm, without the word Travel.
- Keep Cherry in the profile field while removing redundant profile wording from names and designer descriptions. Leave unsupported or conflicting metadata unset.

## Keycaps

### CRP rounds and standalone projects

- Use CRP in names; retain Hammerworks in manufacturer/designer metadata. Main rounds remain separate entries, including the documented R2.2 rerun, with kits as variants within each round.
- R0, R5, and R5A can be sculpt kits. Preserve documented kit labels without generating predecessor rounds or copying kits between rounds.
- Early-round coverage is incomplete: R1 relies on a [participant retrospective](https://geekhack.org/index.php?topic=106389), while R2 relies on [historical owner listings](https://www.reddit.com/r/mechmarket/comments/dnpxni). The [original Hammer thread](https://geekhack.org/index.php?topic=95163.0) documents the BSP-to-CRP transition; proposed BSP kits are not automatically delivered CRP kits.
- Later kit evidence comes from the [R2.2 organizer inventory](https://geekhack.org/index.php?topic=102992.0), [Daily Clack R3](https://dailyclack.com/products/hammerworks-crp-r3), and Prototypist's [R4](https://prototypist.net/products/group-buy-crp-r4), [R5](https://prototypist.net/products/group-buy-crp-r5), [R6](https://prototypist.net/products/group-buy-crp-r6), and [R7](https://prototypist.net/products/group-buy-crp-r7) listings. These establish offered choices, not exhaustive worldwide coverage.
- Keep standalone Purple, Colorful Fonts Beige/White, Desko Black, and Mint & RGBY separate where a round is unproven. C64, Mirror Image, Wind God, and CRP-X Parallel Worlds are separate projects.
- CRP C64 R1 and R2 use BUGER.WORK as designer and Hammerworks as manufacturer. R1 includes 80s, Mac, and Vertical F; R2 includes 40s, ordinary Alphas, ISO, and TKL. Do not copy those options across rounds. Sources: [R1 announcement](https://geekhack.org/index.php?topic=109469.0), [R2 announcement](https://geekhack.org/index.php?topic=117973.0), [R2 vendor](https://prototypist.net/products/in-stock-hammer-x-buger-crp-c64-r2-keycaps).
- Remove CRP Tulip + Peacock as a combined identity: it refers to separate sets, with no verified round assignment. Exclude its imported record rather than inventing a product.

### GMK names, releases, and kits

- Prefer GMK CYL where retained sources explicitly identify that product line, including Classic Beige duplicates. Do not infer CYL solely from the manufacturer; keep MTNU distinct.
- Merge release-specific names into their parent and retain release choices, including Modern Dolch, Olivia, Vaporwave, and Gregory/GREG 2. Remove Hammerhead's year/small-batch duplicates, Classic Beige's KA1953 Revamp suffix, and Beloved's KA2017 Revival suffix.
- Merge GMK 9009's 40s Addon and Ortho & Vim into the parent. Alt Grrrrr Addon is an alternate name, not an extra option. Child-kit authors do not replace the parent designer.
- Black Snail uses documented keycap kits, including Novelties & Shinethrough and the separate Retro Point choice; exclude accessories. Sources: [GEON](https://geon.works/products/gmk-black-snail), [Yushakobo](https://shop.yushakobo.jp/en/products/8286).
- WoB and White on Black are aliases. WoB Extensions remains a distinct project with 40s, Colevrak+, and R0/R5 kits, credited to ttom. R0/R5 denotes sculpt rows, not releases R0 through R5. Sources: [GMK WoB](https://www.gmk.net/shop/en/gmk-cyl-wob-white-on-black-keycaps/fptk5007.0), [WoB Extensions](https://novelkeys.com/products/gmk-wob-extensions).
- Preserve sculpt labels such as Dolch R5 rather than expanding them into numbered releases.

### Other kits and product identities

- Merge matching PBTfans child kits into BOW/WOB, Kabuki-Cho, Klein Blue, Resonance, Retro Dark Lights, and Twist. Keep distinct kit labels within the correct parent.
- Merge osume kits only into the matching Cherry or Marshmallow product. Keep Year of the Snake's kit when no base parent is established.
- Merge [NicePBT BoW Bakeneko](https://cannonkeys.com/products/nicepbt-bow-bakeneko-kit) into BoW and DCS After School 1992's 40s/Monokit aliases into the same DCS product; do not cross DCS/DSS profiles.
- Keep independent compatibility/add-on projects separate when they serve multiple sets or lack an established parent, including Centinela Extension, BAE/LAE, Wyse Moogle variants, GMK N9 Ortho, and DSS Honeywell Fix.
- Replace the incorrectly generic DCS entry with [DCS Round 1](https://dcs.wiki/keycaps/dcs-round-1), credited to WhiteRice, and [DCS Round 3 and 4](https://dcs.wiki/keycaps/dcs-round-3-and-4), credited to 7bit. Both use Signature Plastics, DCS, and ABS; their historical project titles are not variant ranges.
- Keep SA-R3 1976's profile identity. KBParadise V60/V80 describe compatible keyboards, not version ranges; ALPS and MX compatibility remain distinct.
- Keep Realforce R3/RC1 replacement keycaps, explicitly named as keycaps and marked PBT. Do not add unoffered RC1 options.
- Identify TFUE as NovelKeys' PBT Cherry-profile set, separate from the Ducky keyboard collaboration. Leave unverified factory/designer fields unset.

### Metadata

- Add supported material, profile, brand, manufacturer, and designer fields from product specifications. Do not infer factories from retailers or borrow specifications from recommended products and accessories.
- TP-1 Dieter uses Keyreative manufacture, Geistmaschine branding/design, biip novelty credit, the uniform TP-1 profile, and PBT/fiberglass material. Sources: [NovelKeys](https://novelkeys.com/products/tp-1-dieter-keycaps), [Geistmaschine](https://geistmaschine.io/pages/tp-1).
- Zoom WS identifies Wuque Studio as manufacturer; retain supported product options in the catalog.
- Akko and Epomaker metadata follows each product's specifications, including mixed plastics and distinct profiles. Glorious GPBT Basics/Backlit use OEM where documented; the reviewed themed sets use Cherry.
- Add supported credits and materials across NovelKeys, CannonKeys, Swagkeys, Keychron, Qwertykeys, and other sparse entries. NicePBT/CannonCaps manufacturer labels follow existing vendor/catalog conventions and do not establish the underlying factory.
- Keep credit scope precise: biip's PBoW contribution is sublegends, matt3o's PFF credit concerns the profile, and artwork/colorway credits remain separate.
- Work Louder Wrk. gains its brand without assigning one plastic across editions. TRIFL gains its confirmed profile while final material remains pending. MM Glazed Beige's artisan aluminum/Cherry R4 details do not describe the full set.

## Switches

### Variant combinations and component labels

- Pair versions, spring weights/lengths, housing choices, and actuation/bottom-out forces only when sources establish the combination. This applies to families such as Zeal, Keygeek Cera X/Muse/Y2, HMX Yogurt S, and MZ Y1.
- Remove redundant singleton selectors such as Outemu Ocean's Clicky. BSUN Red Panda retains supported 3-/5-pin choices; V1 is not an independent choice alongside a mount option. See the [Pandaverse history](https://www.theremingoat.com/blog/the-pandaverse).
- Use component-specific colors where supported: Alps logo/switchplate combinations, SP-Star Meteor housing colors, Jixian bottom housings, and KTT Hyacinth stem colors. Keep Durock Sea Glass as plain colors, as requested.
- Jixian Black retains Black Bottom and White Bottom; remove Top Housing Variant 2. Jixian White's RGB-bottom geometry remains unresolved.
- Preserve meaningful physical distinctions such as [Cherry Hirose White's dotted marking](https://www.theremingoat.com/shorts/the-hirose-cherry-mx-rainbow); do not invent a corresponding weight.

### Outemu, PrimeKB, and Greetech

- Merge unverified Outemu Sky Clear/Clear and Clear/ICE names into Outemu Sky; keep silent products separate. Supported combinations are V2.1 at 62g/68g/75g/80g and V2.2 at 62g/68g/75g. Do not infer V2.2 at 80g or unsupported earlier versions. See the [original seller's options](https://www.reddit.com/r/mechmarket/comments/911297/uscahdiy_outemu_sky_choice_of_top_v21_stems_and/).
- PrimeKB T1 is one entry with the user-curated choices 62g / Red Stem, 65g / Red Stem, and 67g / Grey Stem. Opaque/translucent labels are not separate choices. Retained observations include a seller-labelled translucent red 67g specimen; the accepted list does not erase that provenance.
- Consolidate Greetech housing-specific names within each color, including Black, Blue, Brown, Green, and Red. Through-hole, SMD, and OG are not selectable distinctions.
- Greetech Blue/Brown retain complete mount/housing choices. Black/Green/Red omit pin counts under the shared pin policy. Razer Green/Orange absorb Chroma aliases but remain separate from generic Greetech colors; omit their resulting singleton selectors.

### BSUN, Tangerine, Kailh, and other families

- BSUN Brown combines black/cream/white bottom choices. Omit a shared manufacturer because the cream-bottom observation credits Tecsee while others credit BSUN. Keep Dustproof Brown separate.
- Ordinary linear BSUN Yellow combines clear-top/white-bottom and full-black housing choices. Yellow Clicky, Yellow Panda, and RGY Traffic Light Yellow remain distinct. Dark Brown and ordinary Green lose redundant housing annotations without gaining singleton selectors.
- C3 Tangerine combines complete version/weight/housing options. V1 uses the observed black-bottom 67g configuration; V1.5 retains documented milky/black-bottom combinations; later versions retain observed weights. Light/Dark are weight aliases. Manufacturing changed from Gateron to JWK, so omit a single parent manufacturer. See the [Tangerine history](https://www.theremingoat.com/blog/tangerine-v2s).
- Kailh Box Jellyfish (Y) is the linear family with V1/V2 options; (X) remains the separate clicky product. Merge duplicated Box - Box Brown/Red/White names into their parents.
- Merge Epomaker Budgreigar/mold aliases under Budgerigar, preserving supported V1/V1.1/V2 choices. Merge Camo Camel entries excluding PATT, and consolidate Keygeek Raw.
- Remove spring weights from names where they belong in choices; omit singleton weights. Normalize Blue Velvet Linear/Tactile, Cherry MX Hyperglide Brown, KTT Bamboo Gleam, and Ajazz Violet names. Removing Bamboo Gleam's Maybe qualifier does not independently verify its identity.
- DK Strawberry Milk Linear/Tactile each combines Strawberry, Milk, and Ice configurations with corresponding housing/stem colors. Absorb incidental EpicGear/HORI LED and FFFF replacement-top annotations. Omit LED diffuser choices from Haimu Raw and merge Yumo D's diffuser specimen into its parent.
- DareU Purple Gold absorbs Violet Gold aliases and treats Pro as V3 / Pro. DareU Sky Blue retains its full name and supported MX version choices; omit POM Top / 3.8mm at user request. The seller's [unofficial V5 nickname](https://switchoddities.com/products/dareu-sky-sky-blue-v5) does not establish a numbered release. Optical and magnetic Sky remain separate.
- Merge Boba LT aliases as Gazzew Boba LT, retaining 55g/65g choices. The redundant Thock annotation expands LT rather than naming a separate model. See [Gazzew's product](https://www.gazzew.com.hk/products/boba-lt).
- Remove Snow Crash's Overlubed Batch annotation from its name and ID; the [first-hand review](https://unikeyboards.com/blogs/unikeys-switch-reviews/keebscape-co-x-hmx-snow-crash-review-by-vere) describes a lubrication defect, not a separate product. Merge Huano Aries flaw/flawed-version aliases under Huano Aries without a defect selector.
- Keep Geonworks (OEM) as Raw ZERO's manufacturer wording because [Geon's own specification](https://geon.works/products/raw-zero) explicitly uses it; no underlying factory is inferred.
- Remove parentheses around Linear/Tactile throughout switch names, including Gugu Ice, Clione Limacina, MOD, and Zeal Clickiez.
- Consolidate HMX Cola into complete choices: black stem / 3.5mm at 37g, 42g, or 45g; red stem / 3.8mm at 45g. Do not invent lighter red-stem options. Sources: [Divinikey weights](https://divinikey.com/products/hmx-cola-linear-switches), [UniKeys red stem](https://unikeyboards.com/products/hmx-cola-3-8mm-linear-switch-factory-lubed-10pcs), [LumeKeebs color/travel mapping](https://lumekeebs.com/products/hmx-coa-linear-switches-10pcs-pre-order).
- Consolidate Hi-Tek 725 White into One Eye, One Eye / Gundam, and Two Eye construction variants. Other retained colors omit singleton eye annotations. Keep Black Clicky and Black Tactile separate and correct Black Clicky's type to clicky; a shared color does not establish the same mechanism. See [Hi-Tek construction history and original documentation](https://telcontar.net/KBK/Hi-Tek/Series_725).
- Everglide Haimu Jade absorbs Jasper and uses the HE family, as identified by the [seller](https://switchoddities.com/products/everglide-haimu-jade-jasper). Other Jade products are not merged solely by name.

### Gateron CAP and named variants

- CAP Brown pairs revision and housing: V1 Golden (Chocolate), plus V2 Golden (Chocolate), Milky, Clear Top / White Bottom, and Black Crystal. Historical unversioned Milky observations do not establish V1 Milky.
- CAP Yellow retains documented V1/V2 Golden and Milky combinations plus V2 Black Crystal. CAP Red/Blue retain V2 Clear Top / White Bottom and Black Crystal; Silver/Silent Red omit their single known housing choice. Sources: [Gateron CAP](https://www.gateron.co/products/gateron-cap-switch-set), [Black Crystal](https://kprepublic.com/products/gateron-cap-black-crystal-switch-3pin-smd-rgb-mx-stem-switch-for-mechanical-keyboard-pre-lubed-brown-yellow-silent-red-silver).
- Keep G Pro generations separate, with KS-9 in names. White/Silver's original and 2.0 generations retain single-/two-stage spring choices. Their 3.0 entries retain Standard / White Bottom and Goldenrod / Green Bottom; the common two-stage spring is not another option.
- Deepping retains documented distance options and Swagkeys design credit. Jupiter Banana retains [Keychron's offered 3-/5-pin choices](https://www.keychron.com/products/gateron-jupiter-switch-set).
- Kangaroo Ink retains Monstargears and NovelKeys choices because [documented springs and branding differ](https://www.theremingoat.com/blog/gateron-kangaroo-ink-switch-review). North Pole Brown retains V2 With/Without Band and absorbs Box Brown; no Brown V1 is established.
- Root Beer Float absorbs New/Old Pin records without a pin-revision selector. CAP KS-25/KS-26 lighting support is not a separate choice.

### Gateron KS families and compatibility

- IDs include the KS code shown in the accepted name, including G Pro, Milky Pro, magnetic families, and Rantopad Orange. Source bindings and entry override keys follow the same IDs.
- Keep standard KS-3, KS-8, and KS-9 colors as separate entries. Within KS-3, retain KS-3 / Full Black Housing, KS-3X1 / Full Milky Housing, and KS-3X47 / Milky Top / Black Bottom where documented. KS-8 and KS-9 omit singleton housing choices. Sources: [KS-3](https://www.gateron.co/products/gateron-ks-3-full-black-switch-set), [KS-3X1](https://www.gateron.co/products/gateron-ks-3x1-full-milky-switch-set), [KS-3X47](https://www.gateron.co/products/gateron-ks-3x47-milky-switch-set).
- Keep Silent KS-3 and KS-9 colors separate; consolidate Silent Clear/White aliases under White. Preserve KS-9 in Silent Yellow 2.0 and keep newer Silent 3.0 products distinct.
- Preserve KS-3/KS-3X1 in Milky Pro names. Yellow retains documented housing options; Pink, Purple, and Heavy remain distinct. Do not merge tactile Heavy with linear Yellow.
- Keep legacy KS-1 distinct because of its stem geometry; merge reversed-name aliases only within matching products. Retain supported housing choices, omit KS-1 Black's singleton, and preserve Gateron KS-1 in Rantopad Orange's name. KS-2 Blue and KS-5 Black remain distinct legacy/prototype records.
- Merge KS-33/Low Profile 2.0 aliases under KS-33 Low Profile names and mark them low profile. Chocolate is tactile. Named Bamboo, Banana Chocolate, Strawberry Chocolate, Grey Heron, and Silent Red remain distinct. Sources: [KS-33](https://www.gateron.co/products/gateron-low-profile-mechanical-switch-set), [Chocolate](https://switchoddities.com/products/gateron-ks-33-chocolate).
- Preserve KS-20 in Dual-Rail Magnetic Orange and KS-37B in Magnetic Fox; merge duplicate aliases. Original Magnetic Orange remains separate from the dual-rail mold/diffuser revision.
- KS-22 Optical absorbs matching Keychron Optical Version 2 observations. Remove the unsupported V1/V2 selector within KS-22; retain earlier optical records separately. [Gateron's FAQ](https://www.gateron.co/pages/faq) states that KS-22 is incompatible with KS-15 keyboards. No missing optical generations are inferred.

### Removed entries

- Remove Outemu Silent Lemon/Peach Open Slot, TTC Flame Open Slot, TTC Flame (Slotted), and TTC Flame Red Half Height LED at user request.
- Remove Aristotle Black (Oval Slot), retaining ordinary Aristotle Black. Remove unresolved Alps SKCM (Dust Cover) without assigning it to an Alps color or claiming it was an accessory.
- Remove collector-specific Holy BSUN/Invyr/GSUS/YOK combinations, BSUN Holy Red Panda, Unholy Panda recipes including V2 Stem and Clear Spring specimens, Hiroseal, and Gatistotle Clear. These are custom component recipes rather than established manufactured products. Sources: [Pandaverse](https://www.theremingoat.com/blog/the-pandaverse), [Unholy Panda construction](https://switchoddities.com/products/unholy-pandas-clear-spring), [Gatistotle build](https://stgmva.github.io/keyboards/2017-11-07-RedScarf-II-Ver-D-Build-Log).
- Remove unresolved Blue and Pink Holy Panda and Kailh Holy Panda Red as requested; removal does not establish that they were definitively handmade.
- Retain identifiable manufactured derivatives such as Drop/Massdrop Holy Panda, Holy Panda X, Cherry Ergo Clear, Sarokeys/Kailh BCP, Gateron Cream Soda, and Huano Holy Tom/Jerry. Lubed/filmed stock specimens are not automatically frankenswitches.

## Unresolved research

- CRP's oldest rounds lack complete original vendor inventories. Do not fill missing kits from later rounds.
- GMK Child Kits has no established parent; GMK International Kit may overlap TIK, but the available outbound link does not establish that identity.
- Akko Shiny Kitten has conflicting Cherry/MAO descriptions; Steam Engine Cyrillic has conflicting ASA/Cherry descriptions. Keep profiles unset pending clarification.
- Work Louder Wrk. materials vary by edition; [WRK Cartridge](https://keygem.com/products/work-louder-wrk-keycaps) needs an identity check against the parent. [TRIFL](https://geekhack.org/index.php?topic=112653.0) needs final-production material confirmation.
- [CC ZeRo](https://cannonkeys.com/products/cannoncaps-zero-keycaps) may duplicate CannonCaps ZeRo. [Doys PC Blanks](https://deadline.space/products/doys-keycaps) needs its own specification rather than DAT.1's printed-set details.
- [Berry Trackday](https://nuphy.pl/products/berry-trackday), [NCC Baja](https://www.reddit.com/r/mechmarket/comments/t50wzn), [PFF WoB](https://cannonkeys.com/products/pff-wob), and [TK Chalk](https://terrakeycaps.com/products/tk-chalk-keycap-set) have research leads awaiting verification.
- HiPro EC BoW, Renso, and Topre Commander Monotone/Two Tone need reliable historical specifications. [Meow Meow/Meow Waon](https://shop.yushakobo.jp/en/products/9387) need SoulCat/MOMOKA identity checks. [URSA colorways](https://fkcaps.com/products/ursa-keycaps) need edition-specific materials because the family mixes plastics and layouts.
- DareU Low Profile Red's color-component identity, Jixian White's RGB-bottom geometry, and TTC Red's historical housing/version association remain unverified. Do not assign versions to unversioned Healio or early KTT Wine Red weights.
- Gateron KS-22 Low Pro Banana conflicts with the documented optical use of KS-22. Silent Clear/Silent Yellow have KS-1 only in old collector notes. Keep these unmerged until identities are established.
- Gateron Low Profile Blue/Brown/Red retain Wide because its meaning and KS model are unresolved; do not guess KS-27 or KS-33.

## Import persistence and provenance

- Accepted catalogs live in Assets/Catalogs/. Entry overrides and source bindings in overrides.json preserve accepted metadata, names, merges, and variant lists on reimport; exclusions prevent removed source records from returning.
- Retained source observations, URLs, and collector notes remain in sources.json, even where their wording differs from the accepted entry. Rebinding an observation does not rewrite its original claim.
- Removed records are summarized above rather than duplicated as JSON archives. Git history retains earlier committed snapshots.
- Review new observations before expanding choices. A later import is not evidence for guessed combinations, missing generations, or cross-family compatibility.
