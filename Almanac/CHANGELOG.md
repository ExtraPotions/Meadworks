## 0.1.23

- Guard pause handling when the settings window or game instance is unavailable.

# Changelog

## Unreleased

- Make the Advanced switch reveal specialist settings on mod pages and in Almanac, including world settings. Matching searches can reveal specialist rows while the switch is off; hidden settings and file-only entries keep their restrictions.
- Hide the obsolete imported dock preference from settings menus while retaining its saved value and ConfigDrawers import target. Window layout is unchanged.
- Add Debug-only Stopwatch and GC diagnostics for queued discovery refreshes, settings model rebuilds and durable journal writes. Accepted settings, authority and focused drafts keep their existing behavior.

## 0.1.19 (2026-10-22)

- Requires Crucible 0.3.30 / Crucible.UI 0.3.21.
- 0.1.19. The choice list for an enum or preset setting uses the same translated setting name as its row, and that title follows a language change while the list stays open. A mod without translated setting metadata keeps the raw name.

## 0.1.18 (2026-10-22)

- Requires Crucible 0.3.29 / Crucible.UI 0.3.21.
- 0.1.18. Barter and Solstice now use MeadWorks plugin IDs (`com.meadworks.barter`, `com.meadworks.solstice`), so Almanac lists them under Meadworks with the other products.
- Saved profiles, pinned mods and the remembered mod that still name the old IDs (`com.extrapotions.barter`, `com.extrapotions.solstice`) or their old settings files apply to Barter and Solstice under either ID. Your profile files are not rewritten.
- A choice list opened from a setting's value button shows each choice by its translated name when the mod provides one (for example Mantle's Trinket Preset). The saved value stays the same.

## 0.1.17 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.

## 0.1.16 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- German, Russian, Chinese and Korean text reviewed: the settings pages use the game's own words.

## 0.1.15 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.

### Simpler in-game settings

- MeadWorks menu (F8) > a mod now opens that mod's page: its Essential settings, its actions and an "All settings in Almanac" button that shows every setting of the mod. The Crucible page also lists Crucible UI's Essential settings.
- The all-mods view still lists everything. With Advanced off it shows Essential and Advanced settings; File-only settings appear only with Advanced on and are labeled "Normally edited in the config file".
- A connected player who is not an admin sees each mod's host-owned settings folded into one "Set by the server (N settings)" line; clicking it shows them read-only. Hosts, admins and single player see the rows as before.
- English, Spanish and French text.
- Tools > Logging starts with a read-only Compatibility list: each MeadWorks mod or module that stays off because another installed mod covers it, naming the module and the other mod (step-aside reports from Crucible, which no longer show center-screen notices for Keel and Tempest).

### Parity gaps

- Text editors color keys, values, sections and comments for configuration files, YAML data files and JSON values (Display / Syntax Highlighting, local, default on). Line numbers stay.
- Live structure checks while typing: YAML data files report tabs, unclosed quotes and brackets by line before the product's own validation; JSON-shaped setting values report the first JSON error by line.
- Table editor: list settings whose entries share columns (such as "Wood:5" pairs or "Boar|TrophyBoar|1|0.15" drop rows) open as a table with Add, Remove and List view; multi-line values and data files with such rows get a Table button in the text editor. Untouched rows are written back unchanged; cells cannot contain the column or list separator.
- Requires the matching Crucible.UI (text editor options and table editor). English, Spanish and French text.

### Batch 4D settings text

- Setting help drops the "[Synced with Server]" suffix, owner and scope lines use plainer wording, restart and world re-entry notes are recognized from more product wording, and three of Almanac's own settings have clearer text.
- A key setting bound to the same chord as another mod's key setting now says "Shared chord" and names the other mod and setting on its row in every view, not only under the Keybinds filter. The warning updates within a third of a second when either key changes.
- New Window / Window Scale setting (80%, 90%, 100%, 115%, 130%; default 100%, the current size) resizes the Almanac settings and tools windows as soon as it changes and is saved with Almanac's other local settings. The settings window refits so it stays on screen; the tools window grows only as far as the screen allows. English, Spanish and French text.

## 0.1.14 - batch 4E gameplay fixes (2026-10-19)

- Owner rows for cheat settings show "Admins only" or "Players may use"; players see admin-only cheats locked.
- Opening Almanac from its own MeadWorks menu entry shows its settings again instead of reopening the all-mods browser.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.1.13 - current-build preview refresh

Release stamps and dependency floors for the tested current UI/settings work. This is an intermediate build; expanded 4D and queued 4E are not completed. Exact checks and known limits ship in installer build evidence.

## 0.1.12 (2026-10-18)

- Explain live setting dependencies, revoke unavailable editors, and compare shortcuts with current native key bindings.

- Show live setting dependencies and their reasons, close editors when their dependency becomes unavailable, and explain conflicts with current native key bindings. Existing saved values and setting permissions remain authoritative.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12 for the shared UI/API integration. Broader 4D qualification is pending.

## 0.1.11 (2026-10-17)

- Save accepted own-value edits by default, including local preferences in files that also contain synchronized server settings.
- Preserve the saved server baseline and unrelated keys instead of saving live remote overlays. Remote admin edits still use the owning synchronization system and require server confirmation.
- Report persistence failures without marking the edit as successfully saved.

- Explain settings, editors and toolbar actions through hover help, including defaults, ranges and current permission reasons.
- Clarify filter and pin state, utility fields and display preferences while preserving saved keys, defaults and write validation.
- Keep the Advanced header switch as the main control, hide remembered internal selection, and retain specialist preference editing under Advanced.

## 0.1.10 (2026-10-15)

- First release on Thunderstore.
