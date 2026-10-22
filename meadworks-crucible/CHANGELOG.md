# Changelog

## 0.3.27 (2026-10-21)

- Includes Crucible.UI 0.3.20.
- The MeadWorks notice stack sits below the game's unlock and notice cards. A notice wraps to two lines and the card grows with the text. "..." is used only when a third line would be needed.
- Notice cards measure the wrapped text in the game font. Chinese, Korean, and any other script get a second line when the message needs it, instead of a one-line card that cuts the rest off. A notice still stops at two lines, and "..." is used only when a third line would be needed.

## 0.3.26 (2026-10-21)

- Includes Crucible.UI 0.3.19.
- A required mod's version check uses the server's peer class. A joining client that marks that mod server-only in its answer is still refused when the major.minor does not match, and still admitted when it does. Strict admission, the Crucible hello, and the Jotunn mod-list check are unchanged. A slow join still waits on a busy send queue, then 60 seconds, and never longer than 5 minutes.
- The vanilla refusal and welcome notices now include German, Russian, Chinese, and Korean on the same literal line as English, Spanish, and French. A vanilla client still has no Meadworks localization tokens. The server sends one center message.
- A notice stack taller than the open gap between the compass and the hotbar shows only the cards that fit, and the rest wait with their timers held. The stack stays off the hotbar. A card wider than the screen shrinks to the side margins.

- A slow join is no longer dropped while its first data is still queued. The server waits until that peer's send queue drops to 64 KB, then allows 60 seconds for the Meadworks answer, and never waits longer than 5 minutes from the greeting. A peer that never answers is still refused. One warning is written when the wait passes the old 30 second limit. The check that a client sent Jotunn's mod list or Crucible's hello before it spawns is unchanged.
- Crucible.UI: MeadWorks TopLeft and Center notices stack in a Timber or Brown panel (newest on top, repeats collapsed with a count, overflow queued, each card fading on its own timer). The stack stays off the compass, the status-effect row and the hotbar, and it hides while a player window is open. Unlock and biome banners, the game's own messages, and other mods' UI are unchanged. Local settings, section Notices: Stacked Notices, Position, Duration and Maximum Visible. A mod that registers IKitNoticeProvider still owns presentation.

## 0.3.25 (2026-10-20)

- Includes Crucible.UI 0.3.18.
- Crucible.UI: faster inventory and crafting window. The screen layout no longer moves the crafting panel and the character header every frame. The inventory screen's animator puts their x back after every move, so the layout's x was never drawn and both panels, with every row and frame under them, were rebuilt each frame. An axis found back where it was is now left to the game until the inventory closes; the other axis is still placed. What is drawn is unchanged, apart from an occasional one-frame 24-unit jump at 1920x1080, which no longer happens.
- Test bridge: the FPS bench's dirty tracker also records transform writes on the inventory screen's panels with their callers, and where the crafting panel and header are when the canvases render.

## 0.3.24 (2026-10-20)

- Includes Crucible.UI 0.3.17.
- Crucible.UI: faster inventory. The game-panel theme's Image.OnEnable hook checks its cached wood-sprite list before the native-menu ownership checks, which walk the hierarchy; an image that is switched on (an equipped marker, a HUD panel) no longer costs about 0.5 ms. Which panels are themed is unchanged.
- Crucible.UI: the native-menu styling no longer reads the main menu's and pause menu's host fields every frame (twelve reflected reads plus a dictionary refill). It finds them in its 0.2 s refresh and, at most once a frame, when an image is switched on in a native menu. The open MeadWorks menu (F8 or Esc > MeadWorks) builds its product list fingerprint every 0.25 s instead of every frame (a few KB of garbage per frame); switching to a product's page still redraws at once. No visible change.
- Test bridge: `-FpsBenchPhases` (PlayerLoop phase timer), `-FpsBenchDirty <s>` (UI rebuild tracker window) and `-FpsBenchTimingGame` (game methods timed inclusive of mod patches) for the FPS bench.

## 0.3.23 (2026-10-20)

- Includes Crucible.UI 0.3.16.
- Crucible.UI: the pause menu's MeadWorks entry now has the same wood button face and font as Continue, Save, Settings, Log out and Quit. The copied entry drops the inert parts of the native look it was copied with and carries its own.
- Crucible.UI: closing the pause menu (Esc, Continue, the pad's B) also closes the MeadWorks menu left open over it. A window opened from the MeadWorks menu still stays open when the menu hides the pause menu for it.
- Crucible.UI: option lists in MeadWorks menus (the choice dialog for list and flag settings, file actions, filters) show 10 options at once instead of 8; longer lists still scroll.
- Crucible.UI: native settings toggle switches sit on the check box the game draws. Fullscreen, VSync and the other wide-box toggles no longer hide their switch behind the label.
- Crucible.UI: a top-left message without an icon ("Stacked 3 items", "You can't do that") starts at its plate's left edge. It no longer keeps an empty icon column on its left, which read as right-aligned text. Messages with an icon keep the shared icon and text columns.
- Crucible.UI: native menu switches and button faces write colors, sizes and positions only when they change. A disabled switch no longer changes color twice per refresh, and the shared fit helper no longer dirties an image when its scale is unchanged.

## 0.3.22 (2026-10-19)

- Includes Crucible.UI 0.3.15.

### Simpler in-game settings: setting tiers

- Crucible reads each MeadWorks product's embedded `<AssemblyName>.settings-tiers.json` (also for products bound through Charter) and exposes the tier of every setting: Essential, Advanced or File-only (`SettingTiers`, `SettingTierTable`, `SettingTierRules`). Keys are `Section/Key`; `*` wildcards cover generated per-item rows, an exact key wins and the longest wildcard wins next. A product without the resource keeps every setting Advanced.
- Crucible and Crucible.UI ship their own tiers. Crucible.UI's menu key, Styled Panels, Panel Theme, Screen Layout and Map Frames are Essential, so they appear on the Crucible page of the MeadWorks menu (F8). Logging, step-aside, schema and HUD layout records are File-only. No config key, default or authority changed.
- `StepAsideRegistry.Reports` and `MeadworksStepAside.Reports()` list whole-product and per-module step-asides with product and source names, for Almanac's Compatibility list.

### Styling leaves other mods' UI alone

- Crucible.UI: Timber and Brown restyle only Valheim's own panels and MeadWorks windows. Panels other mods make with the game's wood art (Jotunn panels, any third-party UI) keep their own look, even under the game's canvases. The ownership rule is `KitThemeRules.UiOwner`, read at runtime by `KitUiOwnership`.
- TestBridge: the Jotunn coexistence check now fails if the Jotunn panel's art changes under Timber or Brown.

### Notice layout

- Crucible.UI: while MeadWorks styling is on, achievement popups sit just under the compass. Popups that arrive together line up side by side; when the row is full, the rest wait in arrival order and show as places free up, so none is dropped or drawn over another. Their timers and unlock sound start when they appear.
- Crucible.UI: the top-left message and the unlock cards ("New item", recipe, piece) start below the hotbar and share one left edge, icon column and text column.
- Turning styling off puts every notice back where the game places it.

### Parity gaps

- Crucible.UI: the shared text editor can color its text (`KitAlmanacMultiline.Highlight`, drawn over the field's own text so caret, selection and wrapping are unchanged) and take one owner button through a new `KitAlmanacTextEditor.Open` overload with `KitTextEditorOptions`. The existing overloads are unchanged.
- Crucible.UI: new `KitAlmanacTable`, a multi-column row editor beside `KitAlmanacList`; only Apply submits.
- TestBridge: Kindle Virtual Workbench fixture (`kindle.virtual-begin`, `-open`, `-off`, `-cleanup`).

### Batch 4B weather events

- Add a cloud kit glyph, drawn like the snowflake, bolt and moon.
- Add the shared read capability `meadworks.weather.events.detail` 1.0: the weather event with an open kind number and a name token. `meadworks.weather.events` 1.0 is unchanged.

## 0.3.21 - batch 4E gameplay fixes (2026-10-19)

- Add admin-owned "Players may use" switches for cheat settings (default: admins only) and the shared check other mods use to honor them.
- Settings audit: more shared settings apply live and report their owner.
- Crucible.UI 0.3.14: native menu and tooltip appearance updates already in the workspace (achievements, feedback, variant and related native surfaces).

## 0.3.20 - current-build preview refresh

Release stamps and dependency floors for the tested current UI/settings work. This is an intermediate build; expanded 4D and queued 4E are not completed. Exact checks and known limits ship in installer build evidence.

## 0.3.19 (2026-10-18)

- Add typed notice and dialog services with owned providers and native fallback, preserving callbacks and validation.
- Add durable owned HUD layouts and an editor with drag, resize, nudge, undo, guarded attachments and automatic reset; preserve product visibility and background opacity.
- Support live setting dependency descriptions and native key-conflict diagnostics through Almanac, plus shared structured tooltip content.
- Apply the existing button look and reading font consistently, register HUD side plates centrally, and style native confirmation/text-entry hosts reversibly.
- Migrate existing product confirmations and local feedback through the typed services; keep native absence behavior, fresh action validation and one-shot callbacks.
- Expanded 4D qualification is pending. These shared services are independently implemented using existing MeadWorks art and native fonts.

## 0.3.18 (2026-10-18)

- Coordinate selection between added crafting tabs sharing the native row, preventing overlapping mod panels.
- Use the shared framed hover-tip presentation for mod controls, including pointer and gamepad focus.

## 0.3.17 (2026-10-17)

- Save accepted own-value setting edits by default, including when automatic saving was disabled. Validate the saved value and report write failures.
- Save only the edited setting against the existing disk configuration, preserving unrelated saved keys and keeping remote server values separate from local preferences.

## 0.3.16 (2026-10-15)

- First release on Thunderstore.
