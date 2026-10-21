# Mantle

Version 0.1.22. Requires Crucible 0.3.24 / Crucible.UI 0.3.17. Changes are in [CHANGELOG.md](CHANGELOG.md).

A configurable inventory grid, dedicated gear slots, and wearable backpacks.

0.1.16 update: Inventory stat-row hover tips use the shared framed MeadWorks presentation while retaining their existing breakdown text.

## Features

- Configurable inventory grid (width and rows) and base carry weight. New items fill the main grid first; the hotbar is used last.
- Dedicated slots beside the grid: equipment, utility slots, a trinket slot, two ring slots, a belt slot, a backpack slot, Food, Mead and Ammo slots and a coin purse.
- Slot counts are configurable. Mead, Food and Ammo start at 3 and go from 0 to 5; 0 removes that row and its squares on the bar.
- Lowering a slot count moves what was in the removed slots into free inventory cells. With no free cell for them the count stays as it was and a notice asks you to make room, so nothing is dropped.
- Food slots can follow how many foods you can eat.
- Keep On Death: choose which slot groups stay with you when you die (Gear, Backpack, Utility, Trinket, Ring, Belt, Food, Mead, Ammo, Tacklebox).
- Backpacks: one craftable backpack per biome, from the Deerhide Satchel to the Moosehide Pack. Wear one in the Backpack slot to add rows and carry weight; it hangs on your back (you can hide it).
- The Moosehide Pack can carry its contents through portals.
- A worn backpack is equipment (a Utility item worn in the Backpack slot, never as the game's utility), so mods that read worn equipment see it.
- Crafting the next pack while one is worn puts the new pack straight into the Backpack slot with everything still in it, even with a full grid.
- Rings and belts: two Ring slots and a Belt slot right of the utility slots in the Gear tab; items are tagged for them in `Mantle.Accessories.yml` (the item registry, server-synced and editable in game).
- Six craftable rings: Bone Band +5 max stamina, Copper Ring +10 max health, Iron Ring +10% stamina regeneration, Silver Ring +5% move speed, Black Metal Ring -10% attack stamina, Eitr Ring +15 max eitr.
- Three craftable belts: Leather +25, Troll Hide +40 and Lox +60 carry weight; the Lox belt also gives +5 max stamina.
- What lies in a slot gives its bonus, and two identical rings give double. A death, logout or world change takes the bonus away with the cell.
- The Megingjord (+150 carry weight) goes in the Belt slot only, where it is worn as in the game: it shows on the character with its status icon, gives +150 once and uses no utility slot.
- Ring Slots and Belt Slot switch the slots (Slots section); the registry can tag other items and change any number or recipe.
- Gear: right click armor, a cape, a utility, a trinket or a backpack in an open chest to wear it; the piece it replaces moves to a free cell, and with no free cell nothing changes (`Auto Equip`, on by default).
- A piece that lands in its slot is put on; one taken off moves to a free cell.
- `Instant Equip` (off by default) puts weapons, tools and armor on and off with no equipping bar.
- With EliteEquipment's `Separate Boots` on, Mantle adds a Feet slot for its boots.
- Key ring: keys you find go onto a ring under the Backpack slot, and doors still find them. Key stack size is configurable.
- Tacklebox: a slot under the Key ring and four craftable tackleboxes for bait; the fishing rod takes bait from it first.
- Food Key (Z) eats every ready food from the Food slots.
- Mead Slot 1 to 5 Key each drink the mead in their own Mead slot; they start at Left Alt + 1 to 5, as in PackPanel. A key whose Mead slot is not there does nothing: with the default 3 Mead slots, Alt + 4 and Alt + 5 wait until `Mead Slots` is raised.
- While a Mead Slot key's modifier is held the number keys use no hotbar item.
- A small food and mead bar sits under your health. It hides while a hammer, hoe, cultivator or other build tool is in hand, so the game's key hints stay readable.
- Look: the Meadworks wood panel look (Crucible.UI style `Timber`) is on by default (setting `Panel Look`; off restores the game's wood).
- Slot labels and icons for empty slots (`Slot Labels`, `Slot Icons`), the equipment panel with Gear and Consumables tabs, the stat sheet, coins, keys and tackle, a bigger crafting panel (`Crafting Panel Width` 100, `Height` 90), and carry weight shown under the minimap.
- The inventory screen follows the Crucible layout (grid top left, stat column and equipment panel beside it, character header and crafting at the right); `Meadworks Screen Layout` in the Crucible.UI Look settings turns that off.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.24 or a later 0.3.x, which includes Crucible.UI 0.3.17 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Mantle with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Mantle` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install the same Mantle version on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.mantle.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- In multiplayer the server's values win for server-synced settings (grid, slots, backpacks, key ring, tacklebox, rings and belts). Lock Configuration is on by default, so only server admins can change them; locked settings show as read-only.
- Your own display choices (look, slot labels, the Food Key and Mead Slot keys, showing your backpack) stay per player.
- Mantle has no cheat settings.
- Backpack and tacklebox slots, carry weight, stations and costs are in `Mantle.Backpacks.yml` and `Mantle.Tackleboxes.yml`; the ring and belt items, their stats and recipes are in `Mantle.Accessories.yml`.

## Multiplayer

- Required on all peers: install it on the server and on every client. Mantle checks the full version, so everyone needs exactly the same Mantle version.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Mantle turns off for that session and cheats stay locked.
- Players without Meadworks (vanilla clients) cannot join a server running Mantle. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

Backpacks have four quality levels, upgraded at their crafting station. Worn packs slow movement by 8%, 6%, 4%, or 2%; items in their added cells weigh 100%, 95%, 90%, or 85% of their normal weight. The tooltip previews the selected quality. Upgrading a worn pack keeps its slot and contents.

- Mantle is built from PackPanel code, so it does not load when PackPanel is installed. Remove it first.
- Mantle has no settings converter.
- Tested side by side in single player with ExtraSlots, EquipmentAndQuickSlots and AdventureBackpacks: no errors.
- With Burrow: Burrow's Quick stack, Store all, Top up and Sort buttons stand in the armor/weight column under the weight plate, and move under the chest panel while a chest is open. Burrow respects Mantle's protected slots. Burrow's Stacks feature writes the key ring's Key Stack; Mantle then leaves the keys to it.
- With Inscribe: backpacks are an Inscribe item class of their own (item level by biome, the Deep Pockets inscription), a magic worn pack and the extra utilities count as worn equipment, and the stat sheet lists Inscribe's inscriptions.
- With Wildblood: a globe square under the armor and carry squares beside the minimap shows the world tier.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Mantle

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

All Mantle code, including the parts adapted from PackPanel by Gerald Glasgow, is GPL-3.0-or-later (Copyright (C) 2026 expDARE; see LICENSE-CODE.md and licenses/GPL-3.0.txt). Adopted upstream sources keep their preserved notices in THIRD_PARTY_NOTICES/.

Author: expDARE.
