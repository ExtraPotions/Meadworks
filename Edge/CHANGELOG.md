# Changelog

## 0.2.16 (2026-10-28)

- Dependencies updated to the latest versions (Crucible 0.3.37, BepInExPack_Valheim 5.4.2360, Jotunn 2.30.2).

## 0.2.15 (2026-10-28)

- Requires Crucible 0.3.36 (dependency pin updated so mod managers install the current Crucible).

## 0.2.14 (2026-10-28)

- Edge starts with its built-in combat rules if it cannot create or read its rule folder.
- If Edge cannot remove all extra ammunition for an overcharged bomb, it restores any ammunition already removed, cancels the attack and removes the projectiles it spawned.

## 0.2.13 (2026-10-26)

- A sticky charge follows the creature or object it stuck to without being parented to it. When that target is destroyed first, the charge is removed through the game's network cleanup, which ends the NullReferenceException logged every frame afterwards.
- A failing animation reference write is retried every 30 seconds instead of every frame.
- Less per-frame work: the summon quality HUD stays idle while its module is off, and a settings check no longer builds a string on each call.

## 0.2.12 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.

## 0.2.11 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Smoked monsters are cleared only by the peer that owns them. If another player takes the monster, it can pick a target again until ownership comes back.
- A character's shield number is written only by the peer that owns that character.
- Secondary-attack preset choices are compared without building a text key every frame. A rejected apply still waits for a real change before it tries again.

## 0.2.10 (2026-10-20)

- Requires Crucible 0.3.24 / Crucible.UI 0.3.17.
- Faster inventory: the secondary-attack preview under the equipment panel reads the HUD panel's saved visibility once when the inventory opens, instead of switching the HUD panel on and off every frame (that rebuilt its canvas each frame the inventory was open). No visible change.

## 0.2.9 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.
- New host setting 2 - Ranged / Arrows To Crosshair (default off, applies at once): your arrows, bolts, thrown weapons and staff shots leave toward the point under the crosshair instead of parallel to the camera. Only the launch direction changes; gravity, speed and spread stay the game's. On servers without Edge your own value applies. English, Spanish and French texts.
- Dodge Cancel has two new host settings: Dodge Cancel Only Before The Hit (default off) cancels only until the attack lands its hit, and Dodge Cancel Extra Stamina Cost (default 0) charges stamina on top of the dodge, refusing the cancel without enough stamina.
- With Inscribe, Quickened and Swift Casting on the weapon shorten secondary cooldowns (shown in the tooltip; at most by half; never longer), through Inscribe's `meadworks.inscribe.secondary_cooldown`. Without Inscribe nothing changes. New host setting 1 - General / Inscription Cooldown Bonus (default on) turns this off; Inscribe's Inscription Effects does too.
- Settings help: the seven ranged secondary-attack choices explain each option. Keys, defaults and values are unchanged.
- Read existing text and indicator colors from shared Crucible.UI color roles; colors are unchanged.
- Simpler in-game settings: the F8 per-mod view shows only the essential settings. Everything stays in Almanac and the config file. No keys or values change.

## 0.2.8 - batch 4E gameplay fixes (2026-10-19)

- The secondary attack HUD docks directly below the Equipment frame and follows it, so it no longer covers player windows.
- New setting Return Thrown Weapons (default on): thrown non-disposable weapons return to the slot they left.
- Settings audit: affected settings apply live.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.2.7 (2026-10-18)

- Use the shared owned HUD layout and presentation controls for the existing combat display.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

- Add optional EpicLoot Burst support with per-shot multishot, exact ammo payment, Ammo Conservation and reload handling. Validate callback contracts and actual Harmony registrations instead of requiring one EpicLoot version string. Focused native checks passed with EpicLoot absent and with 0.14.13; multiplayer and other EpicLoot versions remain unqualified.

## 0.2.6 (2026-10-15)

- First release on Thunderstore.
