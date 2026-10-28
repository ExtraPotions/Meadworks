## Changes in 0.3.12

- After a portal trip starts, Threshold verifies the fuel removal. A fuel-removal error no longer prevents arrival protection from being set up.

# Threshold

Version 0.3.12. Requires Crucible 0.3.26 / Crucible.UI 0.3.19. Changes are in [CHANGELOG.md](CHANGELOG.md).

Choose destinations from the integrated map/list, use a named-portal selector with an explicit Teleport action, or keep native tag pairing.

## Features

- Teleport Mode (server-authoritative): Map keeps the integrated map/list by default. TargetTeleport offers named destinations in a dropdown with an explicit Teleport button. Default keeps native tag pairing with portal access modes and custom map icons off. Quick jumps and sea gates remain available in all modes.

- Portal travel: walking into a portal opens the game's large map with a searchable, categorized destination list on the left. Click a list entry to travel at once. A single click on a portal pin travels after the game's 0.3 second double-click window. Double-click the map, or a portal pin, to place one of the game's own map pins. Right-click a pin to remove it. Those pins are saved with your character for that world. While you are choosing a destination, "You are here" marks the portal you walked into. With Show Portal Pins on, the ordinary large map uses the same words above your player arrow. Closing the map or stepping out of the trigger cancels; when the map cannot open, the game's tag-paired jump remains the fallback.
- Hubs: name portals as hubs and pick one as your home hub per world. The map's Return To Hub button leads back to it (the button appears once you pick a usable hub).
- Access modes: make your portals Public, Private or Admin. Alt + E on your own portal cycles its mode; Shift + E on a hub opens the game's tag input.
- Personal favorites: right-click a destination in the list to add or remove it. The list also retains the home-hub and source-hub actions.
- Optional fuel per trip (off by default; the server sets the item and amount, Greydwarf Eye by default). An unavailable configured fuel item refuses travel while fuel is enabled. The authorized Ignore Teleport Restrictions cheat still waives fuel.
- Quick Portals (on by default): near portals jump faster, and a jump into an area that is already loaded skips the teleport screen.
- Sea gates (off by default, experimental): two Sea Gate Pillars across water become a portal for ships. The helmsman picks the destination gate, and the ship and crew jump together.
- Gamepad: D-pad and A navigate the destination list and its actions, X travels to the selected destination, Y searches, and B closes. The map uses the game's own navigation.
- Ore Transport defaults to the game's source-portal carry rule (FromStoneToWood). The server may select Blocked, StoneToStone or All for Threshold-routed trips; hard item blocks and world restrictions still apply.
- Bring Tames (off by default, server setting): tames following you within 10 m (up to 5) come through the portal with you.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.26 or a later 0.3.x, which includes Crucible.UI 0.3.19 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Threshold with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Threshold` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Threshold on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.threshold.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- In multiplayer the server's values win for server-synced settings (such as fuel, Quick Portals and sea gates), and admins can lock settings; locked settings show as read-only.
- Cheats: `Ignore Teleport Restrictions` (carry anything through portals and sea gates, ores and metals included, and travel without fuel) is off by default. `Teleport Listed Items` (off by default) lets only the items named in `Listed Items` through, ores and metals by default; fuel still applies. In multiplayer both stay locked unless the server allows them.
- Converter: imports your PortalSelector settings.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Threshold turns off for that session and cheats stay locked. On a Meadworks server without Threshold, Threshold is also off for the session and says so.
- Players without Meadworks (vanilla clients) cannot join a server running Threshold. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Steps aside: Threshold turns itself off when PortalSelector, PotalMap or Wayfare (Gerald Glasgow's portal mod, not Meadworks Wayfarer) is installed. With TeleportEverything installed, Threshold keeps routing portals but leaves listed items and tames to it.
- Converter: imports PortalSelector's settings.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): PortalSelector (Lazly), PotalMap (korCaptain). Listed items and Bring Tames inspired by TeleportEverything (OdinPlus).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Threshold

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

- Wayfare 0.3.2 by Gerald Glasgow (GPL-3.0-or-later): portal list discovery, sync, list version hash, access rules, favorites keys, quick jumps and sea gates are adapted from it; ValheimModLibs AreaLoading (same author and license): quick area loading (see LICENSE_SCOPE.md).
- PortalSelector by Lazly and PotalMap by korCaptain: behavior references only (no code).

Code: GPL-3.0-or-later (LICENSE-CODE.md). Artwork and documentation: CC BY-NC-SA 4.0 (LICENSE-ASSETS.md).

Author: expDARE.
