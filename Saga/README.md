# Saga

Version 0.1.16. Requires Crucible 0.3.28 / Crucible.UI 0.3.20. Changes are in [CHANGELOG.md](CHANGELOG.md).

Boss kills turn the Forsaken powers into permanent passives, and Defense, Sneak, Woodcutting, Mining, Foraging and Fishing gain perks as they level.

## Features

- Powers: each boss you kill (within 200 m when it dies, by default) deepens its Forsaken power into a permanent passive. Attune to one power at a time by default (up to 8 if the server allows). Activating the vanilla powers is blocked by default. Activate All Attuned Powers is on by default: one activation runs every attuned power, they share the longest recharge, and the power wheel stays hidden. Turn it off to hold Power Wheel Key or Power Wheel Gamepad (both unbound by default) and choose one attuned power. That choice keeps the power's recharge. Escape closes the wheel without opening the pause menu. While the wheel is open, the gamepad buttons it uses do not also use, build, dodge, or start the guardian power. Almanac warns when the key is already used by the game or another MeadWorks mod. Every power HUD layout shows the time left and dims recharging icons; changing attunements and saving/loading the character retain that recharge.
- Defense skill: levels from blocking, parrying and taking hits, and gives more max and food health, damage reduction, out-of-combat regeneration, poise, a longer parry window and cheaper blocks and dodges, plus perks such as Riposte, Shield Wall, Hardened, Last Stand, Reflex, Shield Bash and Thorns.
- Dodging: Long Roll extends the native protection window by up to 30% within the roll. At Dodge 50, a perfect dodge arms Counter for 2 seconds; the chosen melee attack deals 25% more damage. Heavy Roll at 75 allows an encumbered native dodge at twice its normal cost. Edge Quickstep keeps its existing priority.
- Weapon Mastery: at each melee skill's level 50, swords gain damage against staggered foes, knives add poison, clubs build stagger, polearms push harder, thrown spears gain damage, axes build a short repeated-hit chain, and unarmed hits deal more damage. Its master also gates Weapon Switch experience; the existing Weapon Switch suboption is retained.
- Ranged and magic perks follow the Weapon Mastery switch: Bow Recover can spare arrows, Steady Reload retains partial crossbow reload progress, Attunement improves eitr regeneration, and Life Tap reduces Blood Magic health costs. Shield Caster credits the original local caster with Blood Magic experience when a shield absorbs enemy damage.
- Crafting: Maker's Mark improves crafted equipment durability, Sturdy Build improves the health of pieces you place, Thrifty can return consumed crafting materials, and Journeyman adds a crafting-station level at Crafting 75. Crafting has its own switch.
- Swimming: Quick Dry shortens new Wet effects by up to 50%. Long Breath delays drowning by up to 3 seconds per breathless spell and halves its damage at level 100. Salvage doubles body pickup reach while swimming at level 100.
- Riding: Gallop adds up to 15% mount run speed on top of the native rider bonus, and Steady Seat reduces mount damage by up to 25%, using the current rider's native skill factor.
- Jumping: Light Feet reduces jump stamina by up to 50%. Soft Landing adds up to 4 m of safe fall height and halves longer fall damage at level 100. At Jump 50, landing forward can trigger a roll with no stamina cost or protection from hits; falls up to twice the safe height do no damage, and higher rolled falls deal half the remaining damage.
- Running: Pack Mule adds up to 50 carry weight at level 100. Burden at level 50 halves encumbered stamina drain. Second Wind at level 75 restores 30 stamina when running exhausts you, once every 180 seconds.
- Sneak: faster crouched movement, lower visibility and less crouching noise as the skill rises, stronger backstabs and Sneak experience for successful backstabs. At Sneak 75, Vanish can clear nearby non-boss targets after quiet crouching away from enemies, with a cooldown.
- Woodcutting: experience for felling and splitting, and perks such as tree fall push, protection from your own falling logs, domino damage to other trees, clean fells, free replanting, stamina refunds, clean splits, more wood from the largest trees, and finds in felled trees.
- Mining: experience scaled by biome and ore, clean strikes and discovery bonuses, with mining perks.
- Foraging: experience for each pick, scaled by biome, with a discovery bonus. `19 - Foraging / Planted Pickable Foraging XP` (host or server, default 10) is the percent of that experience for a pickable a player planted, such as cultivator stones, berries or mushrooms. Wild plants give the full amount. Yields and crops are unchanged. Wildroot 0.1.17 and later writes the planted mark; a pickable planted earlier still counts when the game recorded a builder.
- Fishing: experience for fights and catches, better bite chance with level, dawn, dusk and rain bonuses, chum, species, size and water senses, a fishing tension bar and a `fishlog` console command for your catch records.
- Skill book: the Skills window gets an info pane with a page for every skill, including other Meadworks products' skills.
- Optional skill gain notices (off by default) and floating callouts for perks.
- Death: 5% of every skill is lost on death by default (the world's death penalty still scales it).
- Each part (Powers, Defense, Running, Jumping, Swimming, Riding, Dodging, Weapon Mastery, Crafting, Sneak, Woodcutting, Mining, Foraging, Fishing) has its own switch.
- Grouped presets: Experience Preset, Perk Threshold Preset, Perk Strength Preset, Death Preset, Finds Preset, Fishing Fight / Preset and Power Strength Preset. Each choice is Vanilla, MeadWorksDefault, Relaxed or Custom. Choosing one writes that group's values immediately. Editing a member selects Custom. Loading does not replace saved values. The host or server owns every group, and joining clients keep their own saved preferences. Planted Pickable Foraging XP, Activate All Attuned Powers and the power wheel keys are not part of a preset. [Preset values](docs/settings-presets.md).

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.28 or a later 0.3.x, which includes Crucible.UI 0.3.20 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Saga with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Saga` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Saga on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.saga.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- In multiplayer the server controls every gameplay setting and data file; your display choices (skill book, notices, power HUD, callouts) stay personal. Locked settings show as read-only. In single player you control everything.
- `Saga.Powers.yml` can replace the per-power settings when Advanced Power Config is on.
- Cheats: Power Commands (the `saga givekills` and `saga resetkills` console commands) and Mastery Override (every power acts at a set level) are off by default. In multiplayer they stay locked unless the server allows cheats.
- Converter: Saga can import your GrindstoneSkills, ProgressivePowers, SNEAKer and SecondaryAttacks settings after a preview, with a backup first.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Saga turns off for that session and cheats stay locked. Your Defense and Foraging levels are kept.
- Players without Meadworks (vanilla clients) cannot join a server running Saga. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Steps aside: with ProgressivePowers installed, Saga's powers turn off. With SNEAKer installed (and enabled), Saga's sneak speed turns off; with SecondaryAttacks installed, Saga's sneak effects turn off. With GrindstoneSkills installed, Saga's skill book, skill gain notices and death skill loss turn off, and so does each of Defense, Woodcutting, Mining, Foraging and Fishing that GrindstoneSkills has enabled.
- Converter imports: GrindstoneSkills, ProgressivePowers, SNEAKer and SecondaryAttacks settings (see Configuration).

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): ProgressivePowers (MidnightMods), SNEAKer (blacks7ar).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Saga

## Changelog

Presets are listed in [docs/settings-presets.md](docs/settings-presets.md).

See the Changelog tab (CHANGELOG.md).

## Credits and license

Original code GPL-3.0-or-later. Files and display prose adapted from GrindstoneSkills (ValheimMods, Gerald Glasgow) keep GPL-3.0-or-later; files adapted from SecondaryAttacks are GPL-3.0-only (its repository carries the GPLv3 text and no "or later" statement), so the assemblies are distributed under GPL-3.0 (see `LICENSE-CODE.md`, `LICENSE_SCOPE.md`). Powers use independently implemented ProgressivePowers behavior; SNEAKer provides behavior and user settings only. Sneak formulas adapt SecondaryAttacks (GPL-3.0-only); skill texts adapted from GrindstoneSkills by Gerald Glasgow stay GPL-3.0-or-later. Original documentation CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`).

Author: expDARE.
