# Changelog

## 0.1.17 (2026-10-28)

- Dependencies updated to the latest versions (Crucible 0.3.37, BepInExPack_Valheim 5.4.2360, Jotunn 2.30.2).

## 0.1.16 (2026-10-28)

- Throttle gameplay-idle menu and join work while continuing to poll pending secret stores, secret actions and the post-join save offer until they complete.

## 0.1.15 (2026-10-28)

- A damaged line in the saved-password file is kept (its text unchanged; only the line ending is normalized) instead of being erased the next time you save or forget a password.

## 0.1.14 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.

## 0.1.13 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- The server list refreshes when the saved list, the password store, or the language changes, including a lock icon after a password is stored. It no longer writes the whole list to text four times a second to decide.

## 0.1.12 (2026-10-20)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- Moving a favorite up or down on the Join Game list while a filter is typed now swaps the selected server with the one shown next to it. The game used the row number in the filtered list on the full favorites list, so it could swap two other servers. Favorites hidden by the filter keep their places; unfiltered moves are unchanged.

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
