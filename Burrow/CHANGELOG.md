# Changelog

## 0.3.20 (2026-10-20)

- Requires Crucible 0.3.24 / Crucible.UI 0.3.17.
- Favorites stay put on Sort: a favorite item keeps its slot on the Sort button, the sort hotkey and Auto Sort Inventory, and the other items sort around it. The hidden `2. Stow/Sort Favorite Items` setting is gone (its default sorted favorites); an old value left in the cfg file is ignored. Unfavoriting an item lets the next sort move it again.
- Trashing a favorite says so: the trash can, Trash Key, drag onto the can and trash-mode clicks refuse a favorite with "{0} is a favorite and was not trashed" (English, Spanish, French), and nothing is destroyed or salvaged. Quick stack, Store all, Dump and Top up already left favorites in the inventory; that is unchanged.
- The game's Place stacks button leaves favorites in your inventory. In a chest you opened yourself, a favorite item, or whatever is in a favorite slot, is kept the same way the game keeps equipped items. Everything else about Place stacks is the game's own (only that one check in `Inventory.StackAll` is widened).
- In trash mode (Shift + click the can), the pointer shows the hammer while the trash salvages, and the bin otherwise.
- The trash can shows its mode. It shows the bin while trashing destroys and the game's hammer while Trash Uses Salvage is on and Kindle's salvage is installed. Its tooltip names the mode ("Trash can: destroy" or "Trash can: salvage") and what happens. It follows the setting, Kindle and the game language at once, on both the stat-column plate and the button-row can. English, Spanish and French.
- Item safety: Burrow's inventory guard no longer keeps or later applies an inventory write that another mod's earlier Harmony prefix already skipped (it checks `__runOriginal`). The same applies to a death another mod cancelled while the inventory was held. Wildblood's guard still runs after Burrow's, so a kept removal is applied once.

## 0.3.19 (2026-10-20)

- Requires Crucible 0.3.23 / Crucible.UI 0.3.16.
- Shared Materials: a chest that Burrow holds for a reservation reloads completely when the game re-reads it, and Burrow's own hold markers no longer make an unchanged chest reload. Before, the reload left the held chest empty in memory, so most cells of a Wildroot grid planted from a chest failed or were refused at payment.
- The Quick stack, Store all, Top up and Sort buttons beside the inventory are no taller than a two-line caption needs (36 units at most), instead of growing to the square size of Mantle's 64-unit plates.
- The trash can salvages by default: Trash Uses Salvage is now on (Kindle salvages items it can; everything else is destroyed as before). Existing players: a saved "false" (the old default) moves to "true" once and the log names it; a value turned off again after that stays off.
- Removed: the Find materials button in the crafting window. It had no setting, so no config changes.
- Item safety: the player's inventory is no longer held for the first frames after spawn. A new character session held it until Burrow's first update, so other mods converting items at spawn had their adds refused (Inscribe's enhancement conversion lost Honing Runes). It is now held only while an earlier shared transfer is actually unsettled.
- Item safety: while an inventory is held, methods that answer (AddItem, RemoveItem with an item, RemoveOneItem) answer false or null and change nothing, as before. Void removals (RemoveItem by name, RemoveAll) are now kept and applied when the hold ends instead of being dropped. Void moves still skip both sides together. Load is no longer blocked (character load, a chest's data arriving).
- Shared Materials (Wildroot planting and other callers):
  - A request beyond the 24 queued at once answers Failed with nothing held or charged, instead of "needs recovery" for a request Burrow never recorded. If Burrow already has a record for that request id, it still answers "needs recovery" so the caller asks its status.
  - While Burrow cannot read its ledger (world authority not ready, no player, ledger fault), it answers "needs recovery" with a reservation the caller can poll; Failed now always means nothing is held. Short conditions while the ledger is open (dead, teleporting, inventory held by a transfer) wait inside Burrow up to the request's ten seconds, then answer Failed.
  - Undecided holds also end when their deadline is further ahead than a fresh hold could be (a deadline from another run's clock). Opening the ledger already ended every undecided hold of an earlier run. Ending an undecided hold never deducts anything.
- Chest Preview: smaller text on the hover panel (rows 16 pt, title 18 pt, rows and icons shrunk to match). The column width setting is unchanged.

## 0.3.18 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.
- Removed: Quick Right Click Transfer — right click always uses the game's own action (use/equip/unequip). An existing `Quick Right Click Transfer` line in the Burrow cfg is ignored.
- Simpler in-game settings: Burrow ships a settings tier list (`settings-tiers.json`), so the F8 menu shows only the eight everyday settings (Reach and Stow on/off, Quick Stack, Store All and Sort Inventory keys, Sort Order, Stack Multiplier, Chest Preview on/off). Almanac still lists every setting; twelve rarely changed ones are marked as normally edited in the config file. No setting was renamed or removed.
  - Only Quick Stack (Q), Store All (G), Sort Inventory (T) and the Fill Modifier (Left Shift) are bound by default. The other 18 Burrow hotkeys and modifiers start unbound and can be bound in Almanac. The Top Up, Sort and trash can buttons are unchanged.
  - Existing players: a key still on its old default is moved to unbound once, and the log names the keys it moved. A key you changed stays as you set it, and a key you bind again later is never touched.
  - The separate Chest Preview entry in the F8 menu is gone; Chest Preview / Enabled is the same switch. The toggle key still works.
  - The container hover lines (Capacity / Hover Contents, off by default) now appear only while Chest Preview is off, so the contents are never shown twice.
  - The one-time notice that the stow button row steps aside for another sort mod is no longer shown on screen; the reason stays in the log and in Almanac's diagnostics.

### Batch 4D settings text

- Burrow.Reach.yml: the Ores group adds the Valheim 1.0.20 ores FlametalOreNew (Flametal Ore) and GoldOre (Petrified Tissue). Applies to new config files; an existing Burrow.Reach.yml keeps its own groups.
- Clearer setting text for Reach / Link Seconds, Stow / Pickup Range and Pickup Only Held Items, Shared Materials / Enabled, and Shared / Request Timeout (a timeout shows the no-answer message; the transfer is not canceled).
- Clients one or more patch versions behind the host (same major.minor) can join; the synced-config floor is now major.minor.0.
- Containers another mod flags with MultiUserChest's `MUC_Ignore` ZDO bool are left out of Multi-User Chests and Shared Chests (no shared view, shared stow targeting, cycling view or owner-applied requests); everything else about them is unchanged.
- Chest Preview: new per-player option Hide Preview For Empty Chests (default off, so empty chests are still previewed).
- Shepherd's feeding troughs (Feed Barrel, Plank Trough, Stone Trough) keep Shepherd's own size (6x1, Feed Barrel 4x2): `chests: wide` and Burrow.Containers.yml entries no longer resize them. A trough saved bigger by an earlier Burrow still shows every item on load and is not saved over.

## 0.3.17 - batch 4E follow-up (2026-10-19)

- Shared / Request Timeout now applies to shared chest requests.
- Core / Shared Chests explains what it does and is shown as depending on Multi-User Chests.

## 0.3.16 - batch 4E gameplay fixes (2026-10-19)

- Shift+E fills smelters and other stations to their maximum again (the fill was skipped because the item parameter was reassigned).
- Shift+E on a chest quick-stacks carried items whose types are already in that chest, respecting capacity and permissions.
- Stations with an inventory hold at least 50 items per slot (Station Minimum Capacity, applies live).
- Crafting window: the vertical side panel is gone; Repair All and the horizontal tabs stay.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.3.15 - current-build preview refresh

Release stamps and dependency floors for the tested current UI/settings work. This is an intermediate build; expanded 4D and queued 4E are not completed. Exact checks and known limits ship in installer build evidence.

## 0.3.14 (2026-10-18)

- Bind cloned inventory controls to the existing shared look and route trash confirmations through typed dialogs with fresh action validation.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

## 0.3.13 (2026-10-18)

- Use the shared framed control hover tips for Find Materials and the trash control.

## 0.3.12 (2026-10-17)

- Keep inventory sort controls on the player panel while a chest is open. The same Sort action sorts player inventory and the current open chest through existing permission and protected-slot guards.

## 0.3.11 (2026-10-17)

- Preserve existing YAML configuration when reading it fails; a failed read no longer triggers replacement with defaults.
- Create missing default YAML files without overwriting a file created in the meantime.

- Refresh carried inventory's cached total weight when stack and weight configuration changes.
- Let the mouse wheel scroll a tall open chest or an active scrolling tooltip instead of cycling to another chest. The cycle keys still work.
- Keep Top Up on eligible ordinary stacks when Mantle owns inventory insertion, preserving occupied protected cells.
- Append missing container-prefab examples to edited YAML without replacing existing choices, comments or companion files.

## 0.3.10 (2026-10-15)

- First release on Thunderstore.
