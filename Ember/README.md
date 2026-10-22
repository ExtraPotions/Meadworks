# Ember

Faster nights when some players sleep, torches that warm you like a campfire, and doors that open as you walk up.

Version 0.5.7. Requires Crucible 0.3.27 / Crucible.UI 0.3.20. Changes are in [CHANGELOG.md](CHANGELOG.md).

## Features

- Interaction reach: 1.5 times the normal local-player range while Ember is enabled; object-specific permissions, offsets and limits remain native.
- Bed enemy warnings: discard stale sensing after a threat is gone or friendly when local AI evidence is complete; live threats and uncertain remote states retain native checks.

- Sleep: when some players are in bed, the night speeds up by their share. The normal all-in-bed morning skip still works.
- Sleep window: choose when beds start working, or keep the game's own sleep window.
- Sleep HUD: a small display below the minimap with the number of sleepers and current boost. It gives way to Cairn while Cairn shows its own sleep row.
- Station timers: while the night is sped up, cooking stations, smelters, crops, fermenters and pickables keep working at normal speed (each can be turned off).
- Torches: lit torches and other listed fire pieces act like a campfire (warmth, resting and the bed's fire check), and a torch in your hand warms you too. Rain, storms and cold biomes can make a held torch burn down faster.
- Doors: nearby doors open as you walk up and close behind you, and stay open while other players are near. Optional toggle key. The server can turn automatic doors off for everyone.
- Sleep / Sleep In Any Bed (the host decides, on by default): sleep in a bed another player has claimed. The hover shows the sleep prompt, and the game's time, enemy, roof, fire and wet checks apply. The bed keeps its owner and your spawn point does not change.
- Each door kind has its own Open Distance and Close Distance. Defaults are 1.5 m and 2.25 m for doors, and 3 m and 4 m for drawbridges. A saved distance that still equals the older default moves to the new default once. A distance you set is kept.
- Sleep / Preset and Doors / Preset are Vanilla, MeadWorksDefault, Relaxed or Custom. Choosing one applies that group's values immediately. Editing a member selects Custom. Loading does not replace saved values. The host or server owns the sleep preset. Each player owns the door preset, subject to server locks. [Preset values](docs/settings-presets.md).
- Seasons, when Solstice is installed and its seasons are on: Seasons / Night Length, Fuel Burn and Torch Heat (host or server, on by default). Night Length uses Solstice's dawn and nightfall, and keeps a night between 10% and 50% of a day. It needs Sleep on and Sleep / Use Vanilla Sleep Window off. The world's day length and day count stay as they are. Fuel Burn uses Solstice's held-torch drain, kept between 0.1 and 5 durability a second. Torches / Fuel Free Fires, and Torches / Endless Torches when the server allows that cheat, still win. Placed-fire fuel stays Solstice's own rule. Torch Heat lets Solstice turn torch warmth on or off for the season. It does not turn the Torches module, or held-torch warmth, on by itself. Off, or with Solstice missing or its seasons off, sleep and torches stay as Ember set them.
- Sleep, Torches and Doors each have their own on/off switch.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.27 or a later 0.3.x, which includes Crucible.UI 0.3.20 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Ember with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Ember` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Ember on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.ember.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- In multiplayer the server's values win for server-synced settings, admins can lock settings (Server / Locked Player Settings), and locked settings show as read-only.
- Cheats: Sleep Any Time, Testing Mode and Endless Torches are off by default. In multiplayer they stay locked unless the server allows them.
- Converter: imports your BetterSleepBruh (including its station timer settings), TorchesAreFires and DoorOpenerBruh settings after a preview, with a backup first.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version. Sleep time runs on the server and torch fire areas affect every player.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Ember turns off for that session and cheats stay locked.
- Players without Meadworks (vanilla clients) cannot join a server running Ember. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Each module turns itself off when the mod it overlaps is installed: Sleep for BetterSleepBruh, Torches for TorchesAreFires, Doors for DoorOpenerBruh. The other modules keep running.
- Optional Meadworks integration: Cairn can show Ember's sleep state.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Inspired by / replaces (behavior reimplemented, no code reused): TorchesAreFires (blacks7ar).
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Ember

## Changelog

Sleep and automatic-door presets are documented in [Settings presets](docs/settings-presets.md).

See the Changelog tab (CHANGELOG.md).

## Credits and license

Sleep adapts BetterSleepBruh by Pete Navarra (aka Vapok) (MIT); automatic doors adapt DoorOpenerBruh by Pete Navarra (aka Vapok) (MIT). TorchesAreFires by blacks7ar is a behavior reference only; no code used.

Code: GPL-3.0-or-later (`LICENSE-CODE.md`, `licenses/GPL-3.0.txt`). Original artwork and documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). Adapted source copyrights and permission notices remain in headers and `THIRD_PARTY_NOTICES`.

Author: expDARE.

## Beds and respawning

`Sleep / Nearest Owned Bed Respawn` is off by default. With Sleep enabled, Ember remembers beds it observes claimed by your character in the current world. After death it chooses the nearest known bed by map distance, including your existing bound spawn. The game validates the destination; destroyed or reclaimed beds fall back to the next known candidate, then the world's normal start. Unknown or unloaded beds that Ember has never observed are not discovered automatically. Login and first spawn retain the normal bound spawn. OpenKeep's enabled equivalent takes precedence; an unknown equivalent setting also leaves that feature to OpenKeep.

This option works with the vanilla sleep window. The candidate fallback queue is transient during a respawn; quitting mid-respawn does not preserve that queue.

Version 0.5.0 adds a native-map choice after death when at least two candidate beds are known and the world permits maps. The nearest bed is selected initially; choose another within 30 seconds or confirm with Map, Escape or gamepad B. A single click waits through the native double-click window, so double-clicking can still place an ordinary map pin. Gamepad A selects a candidate at the map center. With Quick Respawn off, confirmation retains the remaining normal death delay and full loading wait. With Quick Respawn on, confirmation skips the remaining waits while keeping native destination validation and remote arrival checks. Candidate icons appear during choice even if ordinary bed markers are off; temporary filters and owned visuals are restored afterward.

Version 0.4.0 adds `Sleep / Show Owned Beds on Map`, a local display option enabled by default. It requires Nearest Owned Bed Respawn and Sleep to be enabled. Beds Ember has learned for your character in this world get temporary yellow native bed icons on the minimap and large map. Your bound bed keeps its native icon without a duplicate, and your bed-icon filter still applies. The display does not discover unknown beds, change a spawn point or save extra map pins. Turning it off removes Ember's added icons.

`Sleep / Quick Respawn` is a separate, default-off option in 0.3.0. It shortens the requested death and bed-loading delays according to map distance: approximately one second at the death location, increasing to the full native delay at 1,000 meters. Terrain and bed validation can take longer. It works with an ordinary bound bed even when Nearest Owned Bed Respawn is off; login timing stays unchanged.

Remote clients also wait for a short quiet period in nearby replicated objects, bounded by the game's normal loading deadline. For a confirmed bed choice, early release additionally requires the game's actual current bed to be loaded; missing beds retain native deadline fallback. This does not guarantee every network object has arrived. Disabling Quick Respawn leaves an already scheduled death request in place and stops further load acceleration. OpenKeep's active equivalent takes precedence. Compatibility with mods that bypass the game's respawn-request method is not established.

`Sleep / Stand Up On Respawn` is off by default. With Ember's Sleep module active, enabling it skips only the getting-up animation after death. Logging in and first-spawn flight keep the game's behavior. OpenKeep's active equivalent takes precedence. The server controls this setting in multiplayer.


## Fuel-free, smokeless fires and optional night schedule

`Torches / Fuel Free Fires` and `Smokeless Fires` default on. The five standard placed torches, campfires and held Torch burn without fuel or durability loss and create no new smoke. Stored fuel and durability are preserved rather than refilled. Campfires keep their native heat area; Ember adds no duplicate. Other fireplace types retain their existing rules. Turning off the relevant option or Torches module restores its prior behavior. Existing smoke clouds finish their native dissipation.

`Torches / Torches Night Only` defaults off. When enabled, player-built `Torch Pieces` go out by day and light at night; `Torch Margin` extends the lit window by the configured in-game hours. This list is separate from `Fire Pieces`, which controls warmth. While looking at an eligible torch, press the local `Torch Switch Key` (O by default) to keep it lit or return it to the schedule. Ward access and native interaction range apply. Manual on/off changes within the same phase remain until the next phase; a keep-lit choice persists in the world. OpenKeep's presence disables only this schedule to avoid conflicting changes, and the existing TorchesAreFires whole-module step-aside remains.

Disabling or unlisting the schedule relights loaded torches it put out and retains their keep choice. Disable the option and visit your bases before removing Ember so unloaded day-marked torches can be released.
