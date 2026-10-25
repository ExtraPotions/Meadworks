## Changes in 0.1.20

- Use the modern native tameable discovery API while preserving InstanceID seed order.
- Preserve husbandry registration ownership and activation-failure rollback.

# Shepherd

Version 0.1.20. Requires Crucible 0.3.32 / Crucible.UI 0.3.21. Changes are in [CHANGELOG.md](CHANGELOG.md).

The ECR breeding refresh and its adoption boundaries are recorded in [docs/ecr-breeding-refresh.md](docs/ecr-breeding-refresh.md).

Animal keeping as part of your base: feed troughs, a Husbandry skill, and breeding with sires, star inheritance, twins, herd limits and lineage.

## Features

- Feed troughs: buildable plank and stone troughs. Tamed animals walk to them and eat on their own; troughs show the food they hold.
- Young creatures with native food AI, including asksvin hatchlings, use their own food list. Their saved meal interval comes from the adult prefab, or ten minutes if unavailable. Native ground meals also reset that interval for trough feeding. Young creatures without native food AI remain excluded; feeding does not accelerate growth. Feed Wild Animals, deny lists and effective No Hunger still apply.
- With Require Move To Feed on, eating requires the source to be within one meter vertically as well as within horizontal reach. Turning that setting off retains remote feeding within the configured search range.
- Reach Through Walls permits up to one meter of combined solid wall thickness by default (0–2 meters). Zero requires a clear feeding reach. Overlapping wall parts count once; floors and terrain remain blocked. The obstacle is checked again before the meal is requested.
- Container feeding (off by default): when the server allows it, animals can also eat from nearby player-built containers. Shift+Use on a container marks or unmarks it as a feed source (gamepad: the alternate-use button shown in the hover hint). Private containers, graves and troughs are never used.
- Honey production (native/off by default): Honey Per Day sets a bounded daily rate; Honey Per Player Online replaces it with the actual online player count. Native hive conditions and capacity still apply. Existing extra-honey skill rewards happen separately at harvest.
- Husbandry skill: levels as you tame, feed, pet and breed animals, and gives faster taming, animals that stay fed longer, calmer animals, more butcher yield and produce, and tougher tamed companions.
- Follow and stay for supported tames: Use on a tamed boar, lox, hen or other tame that supports commands toggles the game's own follow/stay command. Summons and unsupported tameables stay unchanged.
- Breeding: the father (sire) is recorded at conception, twins, and lineage records (parents, generation, birth day and breeder). Newborn stars follow Wildblood. When Wildblood is installed and its `4 - Traits / Tamed Stars` is on, stars are inherited from both parents (`6 - Breeding / Star Inheritance`), with `6 - Breeding / Better Offspring At 100` and `6 - Breeding / Birth Star Chance`. Off, or without Wildblood, a newborn keeps its mother's level, as the game does, and those bonuses add nothing. Shepherd's own `6 - Breeding / Tamed Stars` setting is ignored. The old key stays in the config file and does not change a birth. Wildblood, not Shepherd, gives a tame that an older build left at the game's level its stored stars back when that setting is on. Parked births can regain stored stars too; fresh births without parking metadata keep their assigned level. A due pregnancy waits for Wildblood's server rules before the newborn is created or inherits traits.
- `6 - Breeding / Seasonal Breeding` (on by default, host or server): while Solstice seasons are on, tame breeding uses that season's rate. Spring is faster, winter is slower. The pace stays between one quarter and three times the usual pace. A rate of zero starts no new pregnancies. A pregnancy already underway still finishes. Off, or without Solstice, or with seasons off, breeding stays as it is. Feeding and troughs are unchanged.
- Herd limits: an optional strict herd limit delays births while a herd is over its limit. A buildable land pen sets its own herd capacity, and breeding can pause on moving ship pens.
- Herd book: a shared book listing nearby animals with their lineage, opened from the Meadworks menu (F8 or Esc > Meadworks), the trough header, or a key or gamepad chord (unbound by default), within 50 m. Tames can be renamed.
- Animal lore lines on hover (compact or full).
- Feeding, Breeding and Herd each have their own switch.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.32 or a later 0.3.x, which includes Crucible.UI 0.3.21 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Shepherd with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Shepherd` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Shepherd on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.shepherd.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- In multiplayer the server's values win for server-synced settings. Locked settings show as read-only.
- Cheats: Instant Tame, Instant Growth, No Hunger, Unlimited Herd and Breeding Commands are all off by default. In multiplayer they stay locked unless the server allows cheats.
- Breeding Commands enables two console commands while aiming at a tame within reach: `shepherd breed` and `shepherd stars <count>`.
- Converter: Shepherd can import your AutoFeedRedux and GrindstoneSkills (Husbandry) settings and EliteCreaturesReborn breeding rules after a preview, with a backup first. Older breeding data on animals is kept.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Shepherd turns off for that session and cheats stay locked.
- Players without Meadworks (vanilla clients) cannot join a server running Shepherd. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Steps aside: with AutoFeedRedux installed, Shepherd's feeding turns off. With GrindstoneSkills installed and its Husbandry enabled, Shepherd's Husbandry and feeding turn off. With EliteCreaturesReborn installed and its breeding enabled, Shepherd's star inheritance turns off. With OpenKeep installed and its pet option enabled, Shepherd's container feeding turns off.
- Converter imports: AutoFeedRedux, GrindstoneSkills and EliteCreaturesReborn (see Configuration).
- With Burrow: Burrow's container tags mark feed sources.
- With Wildblood: Wildblood's Tamed Stars decides whether a new birth inherits stars. Taming does not remove stars. Newborns inherit Wildblood traits (with a mutation chance), and young and eggs keep them as they grow or hatch.
- With Keel: breeding pauses in livestock pens on a moving ship (`Pause Breeding On Moving Ships`, on by default).

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): FeedingTrough (Brainless_Azura), Cartur's Follow Command (Cartur).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Shepherd

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Husbandry and breeding rules adapt GrindstoneSkills and EliteCreaturesReborn by Gerald Glasgow (GPL-3.0-or-later).

Code: GPL-3.0-or-later (`LICENSE-CODE.md`, `licenses/GPL-3.0.txt`). Documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). Adopted sources and notices: `LICENSE_SCOPE.md`, `SOURCE_NOTICE.md`, `THIRD_PARTY_NOTICES/`.

Author: expDARE.
