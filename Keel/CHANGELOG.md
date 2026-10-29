## 0.1.18 (2026-10-28)

- Requires Crucible 0.3.36 (dependency pin updated so mod managers install the current Crucible).

## 0.1.17 (2026-10-28)

- Ship speed multipliers now reflect Forward Drag changes.

## 0.1.16 (2026-10-26)

- A HUD part that keeps failing waits five seconds before its next try instead of being rebuilt and removed every frame. The first failure is still logged in full.

## 0.1.15

- Guard ship and container naming during object teardown.
- Handle an unavailable ship crew list when resolving the helmsman.

# Changelog

## 0.1.14 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.

## 0.1.13 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- German, Russian, Chinese and Korean sailing text uses the game's words for ship health, the Karve, sailing, recipes and the hold. Paddle force is the oars. Typed config tokens are unchanged.
- Wind Call and Lookout ignore a networked call unless the sender is a player on that ship. Wind Call also requires that player to be the one steering, and the announcement names that player rather than the id in the packet.
- The wind square keeps its strength text until the strength or the language changes. The arrow still turns every frame.

## 0.1.12 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.
- Simpler in-game settings: the F8 MeadWorks menu shows Keel's 7 essential settings (Sailing Enabled, Wind Call and Lookout keys, Ship Panel, Speed Unit, Boat Camera Max Distance, Sail Opacity). Everything else stays in Almanac; ship recipes, ship lists, per-ship `[Ships]` tuning and managed server/import entries are normally edited in the config file. The module step-aside notices no longer pop up in the middle of the screen; they stay in the log and Almanac diagnostics. Keys, defaults, values and hotkeys are unchanged.

### Settings text

- Setting names and help in English, Spanish and French.
- Hold rows: the help now reads "Number of storage rows in this ship's hold." Keys, defaults and values are unchanged.

## 0.1.11 - batch 4E gameplay fixes (2026-10-19)

- Settings audit: ship catalog settings apply live.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.1.10 (2026-10-18)

- Use the shared owned HUD layout and presentation controls for the existing sailing display.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

## 0.1.9 (2026-10-15)

- First release on Thunderstore.
