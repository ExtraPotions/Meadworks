## 0.1.26 (2026-10-26)

- The Craft button caption is written only when it changes.
- A tracker panel that fails to rebuild no longer throws every frame, and a Salvage tab that cannot be made is not retried every frame.
- Repeating craft-hook warnings appear at most once a minute, and the cart crafting-station warning appears once.

## 0.1.25

- Refresh Repair All tooltips when counts, eligibility, ownership or translations change.

## 0.1.24

- Guard recipe, station and salvage paths when item or inventory data is unavailable.
- Leave Virtual Workbench Key unbound for new entries while preserving saved keys.
- Refresh tracker counts every 1.25 seconds while keeping explicit invalidation immediate.

# Changelog

## 0.1.23 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.

## 0.1.22 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Salvage materials that have already merged into the inventory stay there if adding the stack fails part way. Only the units that did not land drop at your feet.

- German, Russian, Chinese and Korean text on the crafting screen uses the game's own words for recipes, stations, repair, inventory, chests and the related controls.

## 0.1.21 (2026-10-21)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- Salvage tab: also lists gear in nearby chests, marked "From a chest" in Burrow's container color (needs Burrow). It uses the chests Burrow's Reach crafting uses: the same range, wards, privacy, chest allow/deny lists and per-player Reach toggle; chests another player has open and chests held by another transfer are skipped. Favorites, equipped items and every other salvage rule are refused as for carried gear; the hotbar rule still applies only to your own inventory. Salvaging a chest item takes it out through Burrow's durable chest hold (the chest is claimed or handed over by its owner first, the slot must still hold the listed item, and nothing changes if it moved). The materials go into your inventory, and what does not fit drops at your feet. Without Burrow, Kindle lists only your inventory, as before.
- New setting Salvage / Nearby Chests (default on; host value applies on a server, your own in single player). English, Spanish and French text.

## 0.1.20 (2026-10-20)

- Requires Crucible 0.3.24 / Crucible.UI 0.3.17.
- Salvage tab: favoriting or unfavoriting an item in Burrow shows in the open tab within a quarter second. A favorite is listed dimmed with "Favorite items are not salvaged", and an unfavorited item can be salvaged again. Before, the list kept the old state until the inventory changed. Favorites are still read only through Burrow's optional capability; without Burrow nothing is a favorite, as before.
- Faster inventory: with the recipe search and category rows shown, the recipe list is no longer moved and moved back every frame while the inventory is open (that rebuilt the recipe list's layout and the inventory canvas each frame). The list is laid out again only when a row, a setting or the list itself changes. No visible change.
- Salvage tab: the chosen item's rarity backdrop on the large detail icon stays shown. With the slower redraw it flashed for one frame each second, because Inscribe's crafting-panel update cleared it every frame and Kindle only put it back on a redraw.

## 0.1.19 (2026-10-20)

- Requires Crucible 0.3.23 / Crucible.UI 0.3.16.
- Burrow's Trash Uses Salvage works: Kindle now provides the two salvage capabilities Burrow asks for (meadworks.crafting.salvage_can and meadworks.crafting.salvage_item), so a stack dropped on the trash can is salvaged through the Salvage tab's own rules instead of destroyed. Before, Kindle never registered them and the trash can always destroyed.
- Faster inventory: away from a repairing station, the grayed Repair button no longer turns off and on every frame while the inventory is open (it rebuilt the crafting panel each frame). Its tooltip is refreshed with the repairable count instead of every frame. No visible change.
- Faster Salvage tab: the chosen item's name, description, tooltip, returns and Salvage button are redrawn only when the item, its stack, the inventory, the station, a setting or the second changes. Before, the game's recipe update hid them every frame and Kindle redrew them, rebuilding the full item tooltip each time.
- The crafting amount row fits its text: Craft and the -, amount and + controls are as tall as the Craft caption plus padding (at least 30), centered where the game's taller button was. The game's size comes back when batch crafting is off.
- Salvage of found gear: Require Known Recipe now counts gear nobody crafted (Inscribe loot and chest gear, trader stock, drops) as known, and every recipe when the world unlocks all recipes or no-cost is on. Paws of the Bear and other found gear of a recipe you never learned were hidden from the Salvage tab. Returns stay the recipe's real materials; Deep North upgrade-station tokens are never refunded.
- The Salvage tab lists gear that cannot be salvaged right now (equipped, favorite, excluded, recipe unknown, another mod's data, no station) dimmed, with the reason in its tooltip and on the Salvage button, instead of leaving it out.
- New setting Crafting Stations / Use Distance Multiplier (default 1.5, range 1 to 3, host value applies on a server, your own in single player): you can use a crafting station from that many times the game's distance. It applies at once. English, Spanish and French text.

## 0.1.18 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.
- Simpler in-game settings: the MeadWorks menu (F8) shows Kindle's eight essential settings; everything else stays in Almanac and the config file, and the OpenKeep import rows and salvage data lists are marked as file settings. Favorite Recipe Key (was F) and Salvage Key (was Backspace) are unbound by default; middle click, the star button and the Salvage button do the same jobs. Gamepad Sort Button now defaults to Back instead of the left stick press, which the game uses as its multi-craft modifier. A saved value that still equals the old default moves to the new default once (the cfg is backed up first); any value you chose is kept. No setting keys change.

### Batch 3

- Salvage returns socketed gems by Inscribe's Gem Recovery chance for the item's tier (`meadworks.crafting.salvage_gems`); a message says how many were lost. Without Inscribe nothing changes.
- Virtual Workbench (host setting, off by default): Virtual Workbench Key (your own key, default J) reopens the crafting station you used last from anywhere it is still loaded. It is that real station, so its level, extensions, roof and fire rules apply; crafting takes only carried materials, as at the station. Nothing is built or copied, only the use-distance check is relaxed for that station, and turning the host setting off closes the menu. Cart stations are not remembered. English, Spanish and French text.

## 0.1.17 - batch 4E gameplay fixes (2026-10-19)

- Crafting window: the vertical Enhance/Salvage panel is removed; Repair All and the horizontal tabs stay, and Salvage no longer shows leftover Enhance rows.
- Settings audit: affected settings apply live.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.1.16 - current-build preview refresh

Release stamps and dependency floors for the tested current UI/settings work. This is an intermediate build; expanded 4D and queued 4E are not completed. Exact checks and known limits ship in installer build evidence.

## 0.1.15 (2026-10-18)

- Use typed salvage confirmations with fresh inventory validation and shared notice presentation for existing actions.

- Use the shared typed confirmation service for clearing recipe favorites while retaining the existing validation and native fallback. The shared HUD layout editor can position and resize the recipe tracker without changing its enabled setting.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12 for the shared UI/API integration. Broader 4D qualification is pending.

## 0.1.14 (2026-10-18)

- Keep recipe search, category, sorting and batch controls on the native crafting tabs when another mod's crafting tab is selected. Uses the shared Crucible 0.3.18 tab selection contract. Qualification evidence and limits are recorded in the combined failure-batch report.

- Added Repair / Repair On Station Open, a default-off local option that honors existing server locks. A successful usable station opening runs the same free repair-all action as the Repair button for eligible carried and equipped gear. Repeated use while the same station remains open, failed interactions and other actors do not trigger it. Focused local native qualification passed.

## 0.1.13 (2026-10-15)

- First release on Thunderstore.
