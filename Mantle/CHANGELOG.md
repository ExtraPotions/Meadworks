## 0.1.36 (2026-10-29)

- Package files now carry this release's date, so BepInEx reloads updated mods instead of using stale cached plugin details (fixes 'missing dependency Crucible' after updating).

## 0.1.35 (2026-10-28)

- Dependencies updated to the latest versions (Crucible 0.3.37, BepInExPack_Valheim 5.4.2360, Jotunn 2.30.2).

## 0.1.34 (2026-10-28)

- Route partial incoming stacks through legal equipment slots while preserving the remainder for the caller or main inventory.
- Clarify Food Key behavior in all seven supported languages.
- Preserve the release 8.14 slow-join behavior, including progress-sensitive renewal, duplicate-greeting protection and the original five-minute ceiling.
- No art changes are included in this release.

## 0.1.33 (2026-10-28)

- Take All from a tombstone or another container moves only the amount that reached the destination, including partial stack space.
- Backpacks and tackleboxes returning from a tombstone now leave the source copy in place unless the item reaches its reserved slot.
- If Mantle adds items to your inventory but cannot remove the full amount from the tombstone or container, it removes only the extra items it just added and leaves other inventory changes alone.

## 0.1.32 (2026-10-26)

- Tacklebox and pop-up counts are written only when they change.
- Requirement spacing does no work while the inventory is hidden.
- When another mod leaves the trinket data invalid, the warning appears once per item database and the check retries every 30 seconds instead of every quarter second.

## 0.1.28

- Preserve legal dedicated-slot routing and quality/world-level stack accounting for crafting and inventory transfers.
- Reconcile trinket peers on a one-second cadence while handling roster identity changes and departures immediately.
- Reuse back-gear clearance results only while item and parent identities match; restore and reapply after release.
- Leave Food Key unbound for new entries while preserving saved key values.
- Guard malformed trinket item records during equipment tuning.

# Changelog

## 0.1.27 (2026-10-24)

- Right-click a tacklebox in its slot to take it off into a free main inventory cell. With no room it stays equipped; left-click still opens the tackle inventory.


## 0.1.26 (2026-10-21)

- 0.1.26. Requires Crucible 0.3.28 / Crucible.UI 0.3.20.
- Trinket Preset shows the translated names for Native, Conservative, and Custom, including the setting help, and switches with the game's language. The config file still stores those English names.

## 0.1.25 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.
- Requires Crucible 0.3.28 / Crucible.UI 0.3.20 (Crucible 0.3.27 on Thunderstore is a source archive without DLLs).

## 0.1.24 (2026-10-21)

- Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- Adds server-owned trinket tuning with native game data as its baseline. Tuning is off and the Native preset is selected by default; Conservative optionally awards 5% of the effective adrenaline threshold per credited hostile death, weighted by the target's native multiplier. Custom tables are validated atomically. Native recipes, item identities and saved items remain unchanged.
- Adds live tuning, seven-language setting descriptions and effective-value reporting. Unsupported peers use native tuning without new admission requirements.
- Kill credit uses the last hit this peer can see, including when another player owns the creature. Without a visible last hit, only the player who owns the creature can receive the award.

## 0.1.23 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Dedicated servers register backpacks, tackleboxes, rings and belts without loading item art or reading materials. If art fails on a client, the item remains registered with a warning.
- A slow join is no longer refused at 20 seconds while the server's send queue is still full. Charter waits until that queue drops to 64 KB, then allows 60 seconds for the mod list, and never waits longer than 5 minutes from the greeting. A player who never answers is still refused, with the reason in English, Spanish, French, German, Russian, Chinese and Korean. One warning is written when the wait passes 20 seconds.

## 0.1.22 (2026-10-20)

- Requires Crucible 0.3.24 / Crucible.UI 0.3.17.
- Key ring: switching the look (Timber, Brown or off) while the key ring is open re-skins it at once. Before, Brown's round disc showed as a square brown panel until the keys changed or the inventory reopened.
- HUD: while Mantle moves the status effect row left for its stat column, the row wraps onto a second line before another MeadWorks product's HUD panel (meadworks.hud.occupied, Cairn's compass plank). It uses the game's spacing and the same rule as Cairn, so with Cairn installed both place the row the same way, and nothing overlaps with an older Cairn either. With nothing in the way the game's single row is unchanged.
- HUD: opening the inventory or crafting window no longer moves the status effect icons or the minimap. The map and the row keep their place, and the row its wrap, while the window is open; if it would reach into the inventory, container or crafting panel, it is hidden until the window closes and then shown exactly as before. The same hold applies under the Esc menu, the large map and MeadWorks windows. When Mantle stops, the map and the row go back to the game's layout.

## 0.1.21 (2026-10-20)

- Requires Crucible 0.3.23 / Crucible.UI 0.3.16.
- A left click (gamepad A) on the tacklebox in its slot opens or shuts its pop-up; right click (gamepad X) still does too. Shift click picks the box up to move it; dropping another box on the slot still swaps them. Right click on a box in the grid still puts it in the slot.
- The armor, weight and trash boxes (and the world tier box) in the stats column beside the inventory are 36 units high, the height of Burrow's Quick stack / Store all / Top up / Sort buttons, instead of 48. With Burrow's buttons in the column (100 units wide) the icon sits at the left and the value beside it at up to 14 pt; in the narrow column the value stays over the icon. The HUD copies keep their 48-unit squares.
- Faster inventory: the purse's count ("51") is now what the game itself writes into the purse cell, so the label no longer changes twice every frame while the inventory is open (it rebuilt the inventory's canvas each frame). No visible change.
- The crafting panel and the character header wear the forged set (calm wood and the iron frame, KitSkin.GamePanel) in Timber like the inventory panel; before, Mantle dressed them with the plain panel and the frame showed only when Crucible's theme reached them first.
- The tacklebox and key ring pop-ups wear the gear panel's look (Crucible.UI KitSkin.GamePanel: forged wood and iron frame in Timber). Brown keeps the key ring's disc; the look off keeps the game's panel.

## 0.1.20 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.
- Simpler in-game settings: the F8 MeadWorks view for Mantle shows only the eight essential settings. Every setting stays in Almanac and the config file; no keys or values change.

### Batch 3

- With Inscribe, rings and belts roll rarities and inscriptions: two item classes (ring, belt) with pools of existing inscriptions (ring: health, stamina and eitr and their recovery, skill gain, cleansing, coin find; belt: carrying, movement, stamina on the move, pickup range, harvest). The items in the Ring and Belt cells count for their inscriptions, as for their bonus. They may drop pre-rolled and take runes. Inscribe's 1 - General / Accessory Rarity turns this off. Existing rings and belts stay Common.

### Batch 4D settings text

- Clearer setting text for Slots / Slots Per Group and Food Slots Follow Eating.
- Clients one or more patch versions behind the host (same major.minor) can join: synced-config floor major.minor.0 and Jotunn version strictness Minor (was Patch).
- With AAA Crafting installed (plugin GUID Azumatt.AzuAntiArthriticCrafting), the crafting panel keeps AAA Crafting's size, as in PackPanel 0.14.0: Crafting Panel Width and Height do nothing then, and the log says so once.

## 0.1.19 - batch 4E gameplay fixes (2026-10-19)

- Loadouts are removed permanently. Existing items and saves are untouched; old saved loadout records stay inert.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.1.18 - current-build preview refresh

Release stamps and dependency floors for the tested current UI/settings work. This is an intermediate build; expanded 4D and queued 4E are not completed. Exact checks and known limits ship in installer build evidence.

## 0.1.17 (2026-10-18)

- Use shared notice presentation for existing inventory feedback while preserving slot behavior.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

## 0.1.16 (2026-10-18)

- Use the shared framed hover-tip presentation for inventory stat-row breakdowns.

## 0.1.15 local art candidate (2026-10-17)

- Original backpack, tacklebox, ring and belt models with matching icons; backpack clearance for sheathed native gear.
- Native visual and fit qualification is pending; this entry does not record a publication.

## 0.1.14 (2026-10-15)

- First release on Thunderstore.
