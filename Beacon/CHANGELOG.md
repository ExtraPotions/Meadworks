# Changelog

## 0.1.11 (2026-10-20)

- Requires Crucible 0.3.23 / Crucible.UI 0.3.16.
### Playtest fixes (2026-10-20)

- The Beacon button on the Join Game screen sits inside the frame, in the bottom row between Back and Join. It used to sit right of Add server, outside the frame. No game button moves or changes size.
- While the Beacon panel is open over the server list, the game no longer reads keys for the list underneath. Typing in Beacon's fields could refresh the list, change its selection and move favorites (W/S).
- A favorite move with nothing selected is skipped instead of failing. The game's own S key on the Favorites tab with no selection threw ArgumentOutOfRangeException in LocalServerList.Swap.

## 0.1.10 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.

### Batch 4D settings text

- Passwords / Linux Fallback says when a restart is needed.

### Simpler in-game settings

- The MeadWorks menu shows four everyday settings: Skip Password Prompt, Offer To Save Passwords, Auto Join and Reconnect On Disconnect. Almanac still lists every setting.
- The servers panel no longer has an Import button. Import from AutoConnector in Almanac's Convert tools (Preview, then Apply) or with `beacon import preview` and `beacon import apply` in the console. Blanking AutoConnector's passwords stays on the console: `beacon import blank-source confirm`.

## 0.1.9 - current-build preview refresh

Release stamps and dependency floors for the tested current UI/settings work. This is an intermediate build; expanded 4D and queued 4E are not completed. Exact checks and known limits ship in installer build evidence.

## 0.1.8 (2026-10-18)

- Keep saved-server controls and nested editors on the shared UI and explain their actions on hover; preserve connection and storage validation.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

- Explain saved-server controls, editor fields, password actions, import confirmations and join cancellation on hover. Existing storage and connection checks are unchanged.

## 0.1.7 (2026-10-15)

- First release on Thunderstore.
