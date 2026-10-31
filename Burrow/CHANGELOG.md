## 0.3.36 (2026-10-30)

- Picking up an item while an inventory change is still settling no longer reports "no room"; the item stays where it is and is picked up again afterwards.
- Taking from a chest while another transfer is settling waits up to two seconds and retries instead of failing.
- Chest transfers right after joining a world use your real inventory rows.
- Storage recovers on its own after a journal error instead of staying faulted until you restart.
- Payments that only touch chests no longer write a character save.
- A bare left or right mouse button bound to a Burrow hotkey is ignored, so it no longer fires on every click.
- Taking from a chest no longer errors on an item that is already gone from its source.

## 0.3.35 (2026-10-29)

- Package files now carry this release's date, so BepInEx reloads updated mods instead of using stale cached plugin details (fixes 'missing dependency Crucible' after updating).

## 0.3.34 (2026-10-28)

- Dependencies updated to the latest versions (Crucible 0.3.37, BepInExPack_Valheim 5.4.2360, Jotunn 2.30.2).

## 0.3.33 (2026-10-28)

- Requires Crucible 0.3.36 (dependency pin updated so mod managers install the current Crucible).

## 0.3.32 (2026-10-28)

- Two pickup chests owned by different players can no longer both store the same ground drop. A chest asks for the drop the way the game's own pickup does, and two equally near chests always agree which one takes it.
- Moving items between chests and inventories (Reach pull, shared chests) removes only what reached the destination, and a partial move still moves what fits.
- If Burrow adds items to the destination but cannot remove the full amount from the source, it takes back only the extra items it just added and leaves other inventory changes alone.
- A locked or unreadable station settings file no longer interrupts world startup.

## 0.3.31 (2026-10-26)

- A payment waiting on a chest that is not loaded or not owned is retried with a backoff up to five seconds instead of every frame, and right away when a chest loads. The warning repeats at most every five minutes.
- A waiting payment can take over an idle chest nobody has open, then completes once. An open chest or one held by another payment is left alone.
- Finished Shared Materials and chest-salvage payments are archived once per game run, after a verified backup of the journal, so they no longer count toward the 4096-record limit. A completed payment is archived only after its receipt is seen again after a reload, so a lost world save can still apply it again. Nothing is deleted.
- Shared transfers back off up to five seconds while waiting instead of running every frame, and a journal that cannot open is retried from five seconds up to a minute.
- Reach counts are cached for the frame while staying live for payments, and a chest slot from the network outside the chest is ignored.

## 0.3.29

- Propagate timer cancellation without a false YAML IO error.
- Block headless hotkeys before client GUI or IME access while retaining graphical text-input suppression.

- Reuse stable loaded-container snapshots until membership changes.
- Ignore storage hotkeys while keyboard focus or IME composition is active.

## 0.3.28

- Preserve legal inventory cells during layout transitions.
- Keep stack capacity accounting separated by quality and world level.
- Refresh a viewed container when newer network inventory data is available.
- Reuse favourite overlays safely across refreshed or destroyed inventory cells.
- Ignore malformed fuel and unavailable inventory or world data during station refilling.

# Changelog

## 0.3.27 (2026-10-24)

- Release the shared-chest opening hold when keyboard or controller Use is released, restoring the inventory cursor without changing normal owner stacking or shared-transfer safety.
- Requires Crucible 0.3.32 and preserves the existing settings and item data.

## 0.3.25 (2026-10-21)

- Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- Shared Materials fails a reservation as soon as every nearby source is ready and short. A chest that is still loading, held, in use, remote, or briefly missing keeps the ten-second wait, and nothing is taken.

## 0.3.24 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Shared Materials waits up to ten seconds for permitted chests that are held, still loading or handing over ownership, retaining that wait if a source briefly disappears. Availability counts also wait for ownership and fresh data. A ready shortage still refuses with nothing held, and replaying a cancelled request preserves any existing payment decision.

- Chest salvage pays its materials only after the gear has left the chest. A take whose removal is still in recovery, with the gear still in the chest, waits instead of paying. An unfinished salvage from an earlier session leaves the gear in the chest.
- A reach pull whose stack fits in neither inventory nor the chest drops that leftover at your feet, and the chest keeps the removal.

- Reach counts materials only when their chest can be claimed now and requests a handover for remote stock. Crafting, upgrades and placement first pull missing materials through the owner-checked transfer path; a short transfer or lack of inventory room refuses the action before output is created. Any materials already pulled remain in the player's inventory.
- A borrowed station unit returns to the player if its chest rejects it, without removing another unit from a later chest.
- Trash reports success only after the inventory accepts the removal; a held stack stays without a false confirmation.

- A slow join is no longer refused at 20 seconds while the server's send queue is still full. Charter waits until that queue drops to 64 KB, then allows 60 seconds for the mod list, and never waits longer than 5 minutes from the greeting. A player who never answers is still refused, with the reason in English, Spanish, French, German, Russian, Chinese and Korean. One warning is written when the wait passes 20 seconds.

## 0.3.23 (2026-10-21)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- New optional capabilities for Kindle's salvage from nearby chests: `meadworks.storage.chest_items` 1.0 lists the items in the chests Reach crafting may use now (same range, wards, privacy, in-use and allow/deny rules; held chests left out), and `meadworks.storage.chest_take` 1.0 takes one listed item through the Shared Materials ledger: the chest is claimed the game's way, the slot must still hold the listed item, the removal is held, decided, applied and saved with a receipt, and only then are the materials delivered, once. A refused or aborted take changes nothing. Trash Uses Salvage and Shared Materials payments are unchanged.

## 0.3.22 (2026-10-20)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- Shared Materials: a held payment still goes through when its inventory or chest changed only outside the stacks it pays from, for example when Wildroot wears the equipped cultivator for the previous cell or an item lands in another slot. Before, any change refused the payment, so a stone grid paid partly from your inventory skipped about one cell in ten. The stacks it pays from must still hold exactly the reserved item and count; otherwise nothing is charged, as before. The payment is checked against the live inventory and recorded before anything changes, so it is still all or nothing and never charged twice.
- Shared Materials: a large payment is held longer before it must be committed: 10 seconds plus a quarter second per unit, at most 60 seconds (a 90-stone grid gets 32.5 seconds). Before, every hold ended after 10 seconds, so a slow Prevent Partial Planting grid could be returned whole. A hold that runs out still returns everything and charges nothing.
- Shared chest transfers are unchanged: they still require the exact inventory they reserved.

## 0.3.21 (2026-10-20)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- Item safety (Shared Materials and shared transfers): a payment now changes only the stacks it pays from. It lowers or removes exactly those stacks, and every other item keeps its instance, slot, durability, custom data and equipped state. Before, the payment rebuilt the whole item list from its saved image and put a fresh copy in place of any item that did not match (reported by Wildroot for an equipped cultivator). Burrow applies a payment only to an inventory that is unchanged since the reservation, and changes nothing otherwise.
- Item safety: an inventory or chest the game cannot load back unchanged (for example a stack above the current stack size after lowering a stack setting) is no longer paid from, escrowed or changed by a shared chest request. The request is refused with nothing held. Before, the payment cut or dropped that item.

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
