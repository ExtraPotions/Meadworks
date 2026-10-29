## Changes in 0.2.17
- Dependencies updated to the latest versions (Crucible 0.3.38, BepInExPack_Valheim 5.4.2360, Jotunn 2.30.2).

# Edge

Version 0.2.17. Requires Crucible 0.3.38 / Crucible.UI 0.3.25. Changes are in [CHANGELOG.md](CHANGELOG.md).

Combat additions: a secondary attack for every weapon family, a quickstep, swings that hit on slopes, and weapons back in hand after a swim.

## Features

- Secondary attacks for every weapon family, such as barrages, sentinels, boomerang axes, spear rain and launch slams. Bows, crossbows, staves and bombs each have a preset setting; melee secondaries come from the melee rules file. An overcharged bomb is canceled if its exact extra-ammunition cost cannot be confirmed.
- Cooldown and charge HUD, secondary names in weapon tooltips and key hints for keyboard and gamepad.
- A "Secondary Attacks" entry in the Compendium (Texts/Lore) lists every secondary attack with its description.
- Quickstep: a short dash for knives and Unarmed-skill weapons.
- Slope assist: melee swings connect with creatures up or down a slope (Fixed or Adaptive mode).
- Swim recovery: weapons hidden by swimming come back after you leave the water, and the shield you last used comes back when you equip a one-handed weapon.
- Keep Crossbow Loaded (on by default): a crossbow put away loaded stays loaded when drawn again.
- Blood Magic tuning: summon lifetime and quality rules and health costs based on maximum health.
- Dodge Cancel (off by default): dodging or pressing block during an attack cancels it. Stamina and other costs already paid are not refunded.
- Archery: arrows that hit the ground or an object can be picked up again (25% by default, set by the server; arrows that hit creatures, land in water or burn are lost), the camera zooms in as you draw a bow, and a small bar under the crosshair fills with the draw. Zoom and the bar are your own settings in `6 - Interface`.
- Weapon, melee and Blood Magic rules files (`Edge.Ranged`, `Edge.Melee`, `Edge.BloodMagic`) for server owners.

## Requirements

- BepInExPack Valheim 5.4.2360
- Jotunn 2.30.2
- Crucible 0.3.38 or a later 0.3.x, which includes Crucible.UI 0.3.25 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Edge with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Edge` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Edge on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.edge.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- Each module (secondaries, quickstep, slope assist, equipment) has its own switch; Dodge Cancel is in `1 - General / Dodge Cancel`.
- In multiplayer the server's values win for combat settings and the rules files, and admins can lock settings; locked settings show as read-only. Swim and HUD settings stay your own.
- Cheat: `7 - Cheats / No Secondary Cooldowns` is off by default. In multiplayer it stays locked unless the server allows cheats.
- Converter: imports SecondaryAttacks, Slope Combat Assistance and KeepWeaponsAfterSwim settings after a preview, with a backup first.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Edge turns off for that session, slope assist and swim recovery included, and cheats stay locked.
- Players without Meadworks (vanilla clients) cannot join a server running Edge. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Steps aside, module by module: SecondaryAttacks turns off Edge's secondaries (and its quickstep and last-shield features while SecondaryAttacks' own versions are on); Slope Combat Assistance or Slope Combat Fix turns off slope assist; KeepWeaponsAfterSwim turns off swim recovery while it is active; Quickstep (shudnal) turns off Edge's quickstep; ShieldMeBruh turns off the last-shield feature; BetterArchery turns off Edge's archery features.
- Converter imports: SecondaryAttacks, Slope Combat Assistance and KeepWeaponsAfterSwim (see Configuration).
- No quiver: Mantle's Ammo slots already hold extra arrow stacks beside the inventory.
- Optional Meadworks integrations: Edge adds its weapon, Sneak and Blood Magic lines to the shared skill book.

## Notes

- Optional EpicLoot support: Burst rolls multishot and pays ammo independently for each actual shot. Ammo Conservation refunds one paid ammo per successful shot; the final successful shot determines reload preservation. EpicLoot remains optional.

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): KeepWeaponsAfterSwim (Lazly), DodgeCancel (SirJose), BetterArchery (ishid4: recoverable arrows, bow zoom).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Edge

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Secondary attacks, the quickstep and the last-shield feature adapt SecondaryAttacks by sighsorry1029 (GPL-3.0-only); the rules-file merge adapts EliteCrafting by Gerald Glasgow (GPL-3.0-or-later); Keep Crossbow Loaded adapts EliteCreaturesReborn by Gerald Glasgow (GPL-3.0-or-later). Slope assist and swim recovery follow other mods' behavior only (no code used).

Code: original code is GPL-3.0-or-later; files adapted from SecondaryAttacks are GPL-3.0-only (its repository carries the GPLv3 text and no "or later" statement), so the assemblies are distributed under GPL-3.0 (`LICENSE-CODE.md`, `licenses/GPL-3.0.txt`). Original artwork and documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). Adopted sources and notices: `LICENSE_SCOPE.md`, `SOURCE_NOTICE.md`, `THIRD_PARTY_NOTICES/`.

Author: expDARE.
