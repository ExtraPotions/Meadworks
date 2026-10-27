# Inscribe

Item rarity, inscriptions, runes that reshape your gear, gems and sockets.

Version 0.2.8. Requires Crucible 0.3.26 / Crucible.UI 0.3.19. Changes are in [CHANGELOG.md](CHANGELOG.md).

## Features

- Item rarity with inscriptions: equipment can roll a rarity with extra stats.
- Five runes that reshape equipment: Ascension (one rarity step, Common to Uncommon to Rare to Epic on a full inscription pool), Recasting, Honing (one inscription a tier up), Cleansing and the Sealed (Serpent) Rune. Destructive runes ask you to hold Shift to confirm (configurable). The Awakening and Tempering Runes of earlier builds became Ascension and Honing; owned ones convert one for one. Boss and raid drops remain the source of Legendary gear for classes with a full pool.
- A rune on a pickaxe, tool or fishing rod moves prefix slots that pool cannot fill onto suffixes, so Ascension can reach Epic and Legendary on those items. Dropped gear keeps its own limits.
- Gems and sockets: the Dvergr Chisel cuts up to three sockets in weapons, pickaxes, staves, armor, shields and backpacks, and eleven gems fill them. A gem in a pickaxe gives its weapon stat. Other tools, fishing rods and torches take no sockets.
- Worn items in the inventory and hotbar show a thin parchment ring at the cell edge. While Inscribe is drawing item rarity, the game's blue equipped fill is hidden, so a worn magic item keeps its rarity color and a worn ordinary item does not look Rare. When that display is not drawn, the blue fill comes back.
- Rune Table: a buildable table that stores runes, turns trophies into essence and applies runes; an essence chooses what the Ascension Rune adds.
- Recasting at the Rune Table can focus one prefix or suffix on Offense, Defense or Utility. It still costs one Recasting Rune; effect values and the other inscriptions use the usual rolls. Keep Any category for the original behavior. Your focus and side choices are saved locally. If the item cannot receive a complete result with that focus, no rune is consumed.
- Enhancement levels (+1 to +20) and enhancement scrolls were removed in 0.2.0. Existing enhanced items and scrolls convert once, when carried or when their chest is opened: each enhancement level becomes Honing steps that raise the item's inscriptions (up to what the item's level allows), and the rest becomes Honing Runes; scrolls become Honing Runes and Essence. The old item data is kept.
- Creatures and bosses can drop runes and magic equipment; world chests can hold runes and magic equipment.
- Item identification keeps rarity color and etched tier marks, the game's own quality number and socket markers separate across item surfaces. Tooltips summarize these properties before the item details.
- Magic drops retain their rarity glow and loot beam; Legendary drops also have a small bronze and ivory ground glint. Item models, materials and inventory layout stay native.
- Tooltips taller than the screen scroll with the mouse wheel or right stick.
- Keeps the item data of EliteCrafting and ItemEnhancements items, so existing gear keeps its state; ItemEnhancements levels convert as described above.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.26 or a later 0.3.x, which includes Crucible.UI 0.3.19 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Inscribe with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Inscribe` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Inscribe on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.inscribe.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager. Rarity and inscription rules also use YAML data files.
- In multiplayer the server's values win for server-synced settings and YAML rules, admins can lock settings, and locked settings show as read-only.
- Cheats: Item Commands (the `inscribe give`, `roll`, `reroll` and `cleanse` console commands) is off by default. In multiplayer they stay locked unless the server allows them.
- Converter: the server admin can preview and import EliteCrafting settings and YAML rules (inscriptions formats 2-4, economy formats 2-3), with a backup first. Legacy inscription conversion is previewed without changing the source: customized values are retained, removed conditions are explained, and conflicting retired/plain overrides are refused.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Inscribe turns off for that session and cheats stay locked.
- Players without Meadworks (vanilla clients) cannot join a server running Inscribe. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- With EliteCrafting installed, Inscribe's rarity, runes and rune and gear drops turn off.
- With ItemEnhancements installed, Inscribe leaves enhanced items and enhancement scrolls to it (nothing converts), and Inscribe's rarity stats add on top of ItemEnhancements' values.
- With both installed, Inscribe turns itself off completely.
- Optional Meadworks integrations: Mantle treats backpacks as an Inscribe item class and lists Inscribe's inscriptions in its stat sheet, Kindle uses Inscribe rarity when salvaging, Magpie's loot multiplier applies to creature rune drops, and Wildblood's stars can raise drop chances (Wildblood Star Synergy, off by default).

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Inscribe

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Gameplay adapts EliteCrafting 0.7.0, 0.10.0 and 0.11.0 by Gerald Glasgow (GPL-3.0-or-later); model helpers adapt ValheimModLibs by Gerald Glasgow (GPL-3.0-or-later). Versions before 0.2.0 also adapted ItemEnhancements 1.0.2 by ckforgame (GPL-3.0-only); 0.2.0 removed that code and only reads its item key and scroll names to convert old items.

Original code is GPL-3.0-or-later; combined assemblies are distributed under GPL-3.0. Retained notices and source availability are documented in [LICENSE_SCOPE.md](LICENSE_SCOPE.md) and [SOURCE_NOTICE.md](SOURCE_NOTICE.md). Original artwork and documentation are CC BY-NC-SA 4.0. EliteCrafting asset bundles used by Inscribe stay under GPL-3.0-or-later.

Author: expDARE.
