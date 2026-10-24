# Crucible

The shared library every Meadworks mod needs. It adds no gameplay on its own.

Version 0.3.30, with Crucible.UI 0.3.21. Changes are in [CHANGELOG.md](CHANGELOG.md).

## Features

- One shared install that every Meadworks product uses: product registration, shared settings conventions, server authority and permission checks, localization and logging.
- Crucible.UI, the shared Meadworks interface kit, adapted from PackPanel by Gerald Glasgow, used by every Meadworks window, with gamepad navigation.
- Panel look settings: Timber or Brown style, border width and roughness, night shading, and an option to dress the game's own wooden panels in the same look.
- Logging control for the whole suite: quiet by default (errors and warnings only), per-product levels, repeated lines written once and counted, and an optional console filter for all installed mods.
- Achievement guard: quality-of-life actions never mix marked and clean items in one stack. Products whose cheats create items say on their own page whether those items are marked. An optional past-stat resync copies stats the game counted while achievements were not tracked, never for characters that used cheats.
- A shared experience multiplier for Meadworks custom skills (not the game's own skills).
- The `meadworks` console command lists installed Meadworks products, their versions and the session's authority state.
- Notices: MeadWorks top-left and center messages stack in a Timber or Brown panel. The newest card is on top. The same notice again becomes one card: item amounts add, and a notice with no amount shows how many times it repeated. A notice with an item icon shows that icon. Cards past Maximum Visible, and cards that do not fit, wait, and their timers wait with them. The stack stays off the compass, the status-effect row and the hotbar. It hides while the inventory, the map, the pause menu, or a MeadWorks window is open, and the timers wait then too. Unlock and biome banners stay on the game's banners. Other mods' UI is left alone. Stacked Notices off uses the game's own message for those top-left and center notices. Local settings, section Notices: Stacked Notices (on by default), Position (TopLeft by default; also TopRight, BottomLeft or BottomRight), Duration (4 seconds, from 1 to 30; the next notice uses it) and Maximum Visible (5, from 1 to 8). The other notice settings apply at once. A mod that registers its own notice provider replaces this stack.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2

## Installation

- **Mod manager (recommended):** you normally do not install Crucible by hand. Installing any Meadworks product with r2modman, Gale or the Thunderstore Mod Manager installs Crucible and its dependencies for you.
- **Manual:** install BepInExPack Valheim and Jotunn, then copy the `plugins/Crucible` folder from this package into `BepInEx/plugins/`. Install Crucible once; Meadworks products never ship their own copy.
- In multiplayer, install Crucible on the server and on every client (see Multiplayer).

## Configuration

- Accepted own-value edits through the shared settings menus save by default, even when a configuration file has automatic saving disabled. Only the edited value is saved against the existing disk configuration; unrelated keys and personal preferences remain intact. Remote server overlays are kept separate from local saved values, and server-owned edits retain their authority and synchronization rules. Saving failures are reported.
- Crucible's settings live in `BepInEx/config/com.meadworks.crucible.cfg`; the interface kit's look settings live in `BepInEx/config/com.meadworks.crucible.ui.cfg`. Edit them in game with Almanac or any ConfigurationManager.
- Server-synced settings (such as the skill experience multiplier) use the server's values in multiplayer and show as read-only to non-admin players.
- Notice stack settings are local to this player. The server does not override them. They apply without a restart. Duration applies to the next notice. Position, Stacked Notices and Maximum Visible apply at once.
- Crucible has no cheats of its own. It provides the shared gate that every product's cheats go through: in single player you decide; in multiplayer cheats stay locked unless the server allows them.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default, setting `VanillaServerTimeoutSeconds`), then treats it as a vanilla server; required Meadworks products turn off for that session and cheats stay locked.
- A joining MeadWorks client is not dropped while the server is still sending that peer's first data. The server waits until the send queue drops to 64 KB, then allows 60 seconds for the MeadWorks answer, and never waits longer than 5 minutes from the greeting. A peer that never answers is still refused. This wait is separate from `VanillaServerTimeoutSeconds`.
- A required mod is still checked against the server's peer class. A client that marks that mod server-only is refused when the major.minor does not match, and admitted when it does.
- Admission is always Strict: every client must run MeadWorks. The retired `Network.AdmissionMode = AllowVanilla` value still parses and remains saved, but is hidden from settings menus. Startup logs and the `meadworks` diagnostics command explain that Strict applies. See [admission compatibility](docs/server-only-mode.md).

## Compatibility

- AchievementSync (LiveriaDevs): when it is installed, Crucible's past-stat resync turns off and leaves that job to AchievementSync.
- Every Meadworks product steps aside when the mod it replaces is installed; each product's page lists those mods.

## Notes

- Gamepad: provides the controller navigation used by every Meadworks window.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): AchievementSync (LiveriaDevs), achievement guard.
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/meadworks-crucible

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

The shared interface kit and the build-menu folder, selection and drag code adapt PackPanel, ValheimModLibs and OpenKeep 3.0.0 by Gerald Glasgow (GPL-3.0-or-later); the notices are in THIRD_PARTY_NOTICES/.

Original code: GPL-3.0-or-later (Copyright (C) 2026 expDARE; see LICENSE-CODE.md and licenses/GPL-3.0.txt). Original documentation/artwork: CC BY-NC-SA 4.0 (see LICENSE-ASSETS.md). Complete terms are included in licenses/. External dependencies retain their terms.

Author: expDARE.
