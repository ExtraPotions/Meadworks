## Changes in 0.3.33

- Requires Crucible 0.3.36 (dependency pin updated so mod managers install the current Crucible).

# Burrow

Version 0.3.33. Requires Crucible 0.3.36 / Crucible.UI 0.3.25. Changes are in [CHANGELOG.md](CHANGELOG.md).

Craft and build from nearby chests, preview chest contents, bigger chests, sorting, quick stacking and shared chests.

0.3.13 update: the trash controls use the shared framed MeadWorks hover tips. Storage and sorting behavior remain as documented below.

## Features

- Reach: craft, build, upgrade and feed stations straight from permitted chests within 30 meters (configurable; quick stack, top up, shared chest transfers and shared materials use the same 30 meters). Shift+interact fills a station until it is full; Alt+interact or Alt+Craft pulls matching materials into your inventory.
- Quick stack, store all, take all, top up (restock carried stacks), sort your inventory or the open chest, and store one hovered unit, from the button row or hotkeys.
- With Mantle the buttons are stacked under the weight plate in the armor/weight column and move under the chest panel while a chest is open; without Mantle, the row sits centered under the inventory panel. Take all and Place stacks sit in the open chest's header.
- Favorite items and slots that sorting, storing, quick stack, dumping and trashing leave alone, and items you can exclude from restocking. A favorite item keeps its inventory slot when you sort, and the other items sort around it. Trashing a favorite reports that it is a favorite and was not trashed, and nothing is destroyed or salvaged. Favorite Item Key toggles an item favorite.
- Route a stack to the right chest, find which chests hold an item, and cycle through nearby chests without closing the panel.
- Chest preview panel: look at any chest you could open (dungeon and world chests too) and a panel lists its contents with icons, quality and counts, with its fill beside the name.
- The preview sorts by value, weight, name or amount, combines stacks or not, and uses 1 to 3 columns. Position, opacity and size are settings; Chest Preview / Enabled (in the F8 menu and Almanac) and an unbound toggle key turn it on and off.
- Bigger chests: every chest you can build has the inventory's 8 columns by default, with at least twice its slots (`chests: wide` in `Burrow.Containers.yml`; a wood chest is 8x3, a reinforced chest 8x6). Shepherd's feeding troughs keep their own size.
- Newly discovered container prefabs are appended to the main container YAML as commented size examples, including after Jotunn registers late prefabs. Existing entries, comments and companion files stay intact; invalid YAML is left for you to correct.
- Any container can be set to `wide`, `double`, `vanilla` or a size such as `6x4`. A chest is never shrunk over its items: it keeps just enough rows and columns to show them.
- Trash: drop a stack on the trash can by the inventory, after the game's popup (Confirm Trash, on by default). Optional keys (unbound by default) trash the hovered stack, mark an item as junk and destroy every junk stack. With Trash Uses Salvage (on by default), items a salvage provider (Kindle) accepts are salvaged instead. The can shows a bin while it destroys and the game's hammer while Trash Uses Salvage is on and Kindle can salvage. The tooltip reads "Trash can: destroy" or "Trash can: salvage". Favorites are never trashed or salvaged.
- Stacks: stack size and weight multipliers, per-item rules in `Burrow.Stacks.yml`, and an option to carry ores and other restricted items through portals.
- By default a stack holds four times as much at a quarter of the weight, so a full stack weighs what a vanilla full stack weighs: Wood 200 at 0.5 each instead of 50 at 2. Items that do not stack keep their stack of 1 and their weight.
- Auto Tidy (off by default): chests send stray items to the nearby chest that already holds them; an item you put back by hand stays.
- Upgrade tools reach farther: anvils, the tanning rack, the chopping block, the adze and the other station upgrades connect to their crafting station from twice the vanilla distance (Station Extension Range Multiplier, 1 to 10, synced from the server).
- Station capacity: fuel and ore limits of smelters, kilns and other stations from `Burrow.Stations.yml`.
- Ship and cart storage count as nearby chests and ward checks apply (all on by default). Private player chests are left out by default and stay owner-only when you include them.
- Multi-user chests (on by default): several players can use the same chest at once. Changes go through the player whose game holds the chest, so nothing is duplicated or lost.
- With multi-user chests off, the Shared Chests setting decides: chests another player has open are skipped, shown read-only, or used through their owner. Transfers are saved to disk so they survive disconnects; an unanswered request is never refunded automatically.
- Sorting keeps the hotbar, equipped items, favorite slots and extra inventory rows from other mods in place. Moved items keep their custom data, variant and crafter name.
- Rules files (`Burrow.Reach*.yml`, `Burrow.Stow*.yml`, `Burrow.Containers*.yml`, `Burrow.Stacks*.yml`, `Burrow.Stations*.yml`) for per-container material, stacking and grid rules.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.36 or a later 0.3.x, which includes Crucible.UI 0.3.25 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Burrow with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Burrow` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Burrow on the server and on every client (see Multiplayer).
- Before upgrading, moving profiles or restoring saves, close the game and back up your world, characters, Burrow config and YAML files, and the `BepInEx/Burrow.transactions`, `BepInEx/Burrow.shared-owner` and `BepInEx/Burrow.shared-transfers` folders together. These hold pending transfers; do not delete them.

## Configuration

- Accepted own-value changes made through Almanac save by default. Local preferences remain separate from synchronized server values. Existing configuration is preserved during profile updates; explicit resets still replace the selected values. If an existing YAML file cannot be read, Burrow leaves it intact instead of writing defaults over it.
- Settings file: `BepInEx/config/com.meadworks.burrow.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager, or with the game closed.
- In multiplayer the server's values win for gameplay settings, which are locked to admins by default; locked settings show as read-only. Display settings and key bindings stay local.
- Burrow has no cheat settings.

Default controls (with the inventory open unless stated; all rebindable in Almanac):

| Control | Action |
|---|---|
| Q | Quick stack to open and nearby chests |
| G | Store all into the open chest |
| T | Sort your inventory |
| Shift+interact | Fill a station, or quick-stack into the aimed chest |

Unbound by default (bind them in Almanac if you want them): take all, top up, sort the open chest, favorite item / slot, junk, trash / destroy junk, feed source, route modifier (held with left-click), store one, find, dump, cycle previous / next chest, pull modifier (held with interact or Craft), Reach on/off and show storage links. The Top Up and Sort buttons, the trash can and mouse-wheel chest cycling work without a key. Keys that still held their old defaults move to unbound once when you update; keys you changed are kept.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Burrow turns off for that session and cheats stay locked.
- A slow join is not refused at 20 seconds while the server is still sending. Charter waits until that send queue drops to 64 KB, then allows 60 seconds for the mod list, and never waits longer than 5 minutes from the greeting. A player who never answers is still refused.
- Players without Meadworks (vanilla clients) cannot join a server running Burrow. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Steps aside: when Quick Stack Store Sort Trash Restock, AzuAutoStore or Simple Sort is installed, Burrow hides its own button row (Quick stack, Store all, Top up, Sort) and those hotkeys (including Alt+D), writes one notice to the log, and keeps every other feature running.
- While one of those mods is installed, Burrow does not sort on its own: the `Auto Sort Containers` and `Auto Sort Inventory` settings do nothing.
- Not compatible with OpenKeep: Burrow refuses to load alongside it.
- Steps aside: when MultiUserChest is installed, Burrow turns its own multi-user chests off, writes one notice to the log, and leaves shared chest use to MultiUserChest.
- Optional Meadworks integrations: Mantle (Burrow respects its gear rows and blocked slots), Kindle (the Salvage tab can list a Reach chest and take one listed item; the item is removed only after that take is saved, and a refused take changes nothing; Trash Uses Salvage is unchanged), Shepherd (troughs are never used as material sources; chests marked as feed sources), Wildroot (pays building costs from nearby storage) and Timber (inventory is held while a Timber delivery is unresolved). Each works without the others.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): ChestSizeConfig (LuciDreams), ChestPreview (MagicMike), MultiUserChest (MSchmoecker).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Burrow

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Storage code adapts OpenKeep and ValheimModLibs by Gerald Glasgow (GPL-3.0-or-later).

Code: GPL-3.0-or-later (`LICENSE-CODE.md`, `licenses/GPL-3.0.txt`); adapted parts keep their upstream notices (`THIRD_PARTY_NOTICES/`); YamlDotNet is MIT. Original artwork and documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). Adopted sources and notices: `LICENSE_SCOPE.md`, `SOURCE_NOTICE.md`.

Author: expDARE.
