# Hex

Version 0.1.19. Requires Crucible 0.3.36 / Crucible.UI 0.3.25. Changes are in [CHANGELOG.md](CHANGELOG.md).

A cheat toolkit for Valheim that the server controls: personal powers, spawning, skills, map tools, ESP and presets.

## Features

- Personal powers: unlimited stamina and eitr, infinite jumps, God Mode, Fly, NoClip, FreeCam, moon gravity, bouncy landings, super knockback and confetti on your hits.
- Speed and size sliders: walk, run, swim, fly and mounted speed, jump power, and player size from 25% to 300%.
- Survival helpers: pause food timers, no durability loss, no carry limit (shown as ∞) or a custom carry weight.
- Death protection: keep your items, food, skills or status effects when you die.
- Ghost Mode: once the server confirms it, creatures ignore you in every game that runs Hex.
- Local time: pick a time of day for your own lighting (or press Midnight, Sunrise, Noon or Sunset in the Local Time row on the Debug page), or freeze it. The world clock does not change.
- Achievements despite cheats, and optional clearing of cheat marks on your own items.
- Item and creature catalogs with search and filters for spawning (off by default, needs the server's grant).
- Skill editor, map reveal and reset, world seed display, and boss and trader location overlays.
- Debug mode shortcuts (Z, B, K, L, Ctrl + middle-click map teleport, Shift + C time and field of view), ESP labels for nearby players, creatures, items, resources, pickables and dungeons.
- Debug page Radius (m), from 5 to 1000, applies to Kill enemies (K), Remove drops (L) and Tame. Stop event ends the current event and does not use the radius. Kill and Tame skip players and tames. Tame affects wild creatures in that radius. When Thing is installed, its World tools grant can allow kill, remove drops, tame and stop event, and its Spawn grant can allow spawn, when the only refusal is that the player is not an admin. Any other block still refuses. If Thing is not installed, those actions stay admin-only as before.
- Spawn protection overlay: on the Debug page or with `hex protection`, show base protection and structural cover in green and gaps in red on loaded terrain within 50 meters. Requires Debug Tools and Information permission. It starts off each session, has an optional unassigned hotkey and is excluded from saved presets. Red does not guarantee spawning.
- Quick actions: heal, Rested and boss powers for yourself.
- Saved presets, a panic key that resets every power, and optional hotkeys for each cheat and action.
- One window, opened from the Meadworks menu (F8, or Esc > Meadworks), with pages for Cheats, Movement, Skills, Spawn (items and creatures), Map, Debug and Presets. Achievements options, Menu and Panic keys, ESP search and filters, hotkeys and the server cheat policy are edited in Almanac.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.36 or a later 0.3.x, which includes Crucible.UI 0.3.25 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Hex with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Hex` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Hex on the server and on the game of each player who wants it; other players do not need it (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.hex.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- Cheat preferences are your own; the server grants or denies each cheat category instead of overwriting your values. Losing a grant stops the effect and keeps your preference.
- Every power, speed and slider is off or at the game's normal value by default. The four achievement options (Achievements Despite Cheats and the three Clear Cheat Marks options) default on, but do nothing unless the server allows Progress cheats.
- In multiplayer the server's values win for server-synced settings, admins can lock settings, and locked settings show as read-only. Cheats stay locked unless the server allows them. Items Hex's cheats create carry Valheim's own cheated flag (m_cheated); servers running CheatTagEnforcement remove them.
- Hotkeys: the Meadworks menu (F8) opens the Hex window; its own Menu Key, the panic key, per-cheat hotkeys and action hotkeys are unassigned until you set them.
- Console: with Quick Actions, Debug Tools, Map Tools or Skill Editor turned on in General, use `hex help` for the commands.
- Presets are saved in `BepInEx/config/Meadworks/hex/presets.json`. Restore Preset On Join defaults on.
- Converter: imports Valheim_Admin_Tool menu and toggle bindings and AchievementSync preferences after a preview.

## Multiplayer

- Client-optional: install Hex on the server; each player who wants the Hex menu installs it too, with the same major.minor version. Players without Hex, with or without Meadworks, join as usual and get no Hex menu; the server's rules still apply to them.
- Joining a server without Hex: every Hex cheat stays locked for that session. On a server without Meadworks, Crucible first waits for the server's reply (10 seconds by default), then treats it as a vanilla server.
- Players without Meadworks (vanilla clients) can play alongside: Hex does not stop them from joining when an admin sets Crucible's `Network.AdmissionMode` to `AllowVanilla` and every other installed mod allows it. Only players with Hex get the menu.
- With players without Hex connected, creatures their games run still see a Ghost Mode player. The server-local `Ghost With Vanilla Clients` setting (Allow by default) can turn Ghost Mode off while they are connected; configure it on the server because it is not synced to clients.
- Remote debug kill and drops skip creatures and items that the games of players without Hex own.
- Players without Hex see other players at normal size, not the size Hex sets.

## Compatibility

- Hex turns itself off when Valheim_Admin_Tool is installed.
- Hex's achievement features turn off when AchievementSync is installed.
- Debug free build steps aside when Meadworks Timber is installed.
- Optional Meadworks integrations: Edge (no durability loss also covers Edge's wear), Thing (remote seed and location lookups, spawn audit).

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): Valheim_Admin_Tool (QoLTeam), cheat part, AchievementSync (LiveriaDevs).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Hex

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Stamina, movement and shield-wear calculations adapt public-domain code from aedenthorn's ValheimMods (Regeneration, JumpRunDodgeSneakWalk, Durability). ESP adapts MIT code from ValheimTooler by Yohann MARTIN. No Valheim_Admin_Tool or AchievementSync code is used. Details: [SOURCE_NOTICE.md](SOURCE_NOTICE.md).

Code is GPL-3.0-or-later; documentation is CC BY-NC-SA 4.0.

Author: expDARE.
