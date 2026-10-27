# Changelog

## 0.1.15 (2026-10-26)

- A dedicated server now registers the freeze acknowledgement, so freezes and unfreezes are no longer recorded as timed out.
- Unclaimed escrow pickups are never removed by drop cleanup or the game's one-hour despawn.
- An escrow record that cannot be recovered yet warns once instead of every two seconds, and a pickup that was not found is searched for again after 60 seconds instead of on every pass.

## 0.1.14 (prepared; not released)

- Both Sub-Admin confirmations identify the selected player by name and platform ID.
- Includes the confirmation identity placeholder and matching argument count in all seven languages.
- Targets Crucible 0.3.33 / Crucible.UI 0.3.22; final Thing builds against the frozen providers and qualification remain pending.

## 0.1.13 (2026-10-25)

- Requires Crucible 0.3.32 / Crucible.UI 0.3.21, matching the compiled dependency minimums.
- Adds the Sub-Admin grant for non-destructive tools. It does not grant shutdown, access-list changes, regeneration, item take/delete, or destructive world/configuration actions. Separate existing grants retain their permissions.
- Disabling Allow Sub-Admin Delegation blocks new sub-admin grants. Existing grants remain effective, and revocation stays available. Revoking your own sub-admin grant preserves your other grants.
- Sub-admins can view player inventories; take and delete still require the separate inventory grant.
- Keeps inventory recovery and teleport grant expiry running on dedicated servers without a local player.

## 0.1.12 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.
- Requires Crucible 0.3.28 / Crucible.UI 0.3.20 (Crucible 0.3.27 on Thunderstore is a source archive without DLLs).

## 0.1.11 (2026-10-21)

- Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- Inventory delete reserves the selected item's full identity, saves removal with a character receipt before confirming success, and recovers lost replies with the same token. Changed durability or custom data cancels the deletion without touching a replacement stack.
- The inventory take confirmation, in all seven languages, says the reserved stack goes into an empty inventory slot and does not merge with a stack you already have. It stays at your feet as a protected pickup only when no empty slot is free.
- Inventory take and delete use version 3.0 of the inventory owner request, so the server and the player whose inventory is opened both need Thing 0.1.11 for them.

## 0.1.10 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- An admin can freeze or unfreeze a connected player for this connection only. The server checks admin rights. The frozen player cannot move or use actions; menus and chat still work. The flag clears on unfreeze and on disconnect and is not saved.
- The host or a listed admin can grant per-player rights for world tools, spawn, teleport, freeze, inventory, and ward override. Grants are stored on the server for that world. Listed admins keep every right. Everyone else starts with none. The People page edits the rights, including a platform id that is offline.
- An admin with the inventory right can view a connected player's inventory and take or delete one stack. Take reserves the actual owner stack, validates its complete item package, and records a sealed server pickup before requesting removal. Collecting the pickup saves a distinct stack and a receipt together. Durable recovery records reconcile lost replies and restarts without client-requested refunds. Delete asks twice. Actions are written to the server log.
- Delayed permission replies stay bound to the queried player. Changing selection retires the previous player's controls immediately.
- Freeze and unfreeze reconcile every second and require a matching acknowledgment before success is audited. A four-second client lease clears a lost unfreeze; disconnect always clears freeze.
- Inventory transfers preserve item custom data, quality, durability, crafter and variant. They no longer create per-transfer player ZDO keys or leave transfer tags on items. Temporary character receipts are cleaned after server confirmation.

## 0.1.9 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.

### Batch 4D settings text

- Hotkeys / Ward Override Key and Remove Drops Key say what they do instead of naming the source tool.

### Simpler in-game settings

- The MeadWorks menu shows Thing's five everyday settings: admin window key, destructive-action confirmation, in-game admin list editing, message of the day and remote config editing.
- Server-owner policy (audit retention, locked player settings, remote config scope, allow list and backup count) and managed entries are marked file-only; nothing is removed from the config file.
- The World tab pickup radius and the Server config remote editor stay as they are: the editor still reaches server-side values and data files Almanac cannot edit.

## 0.1.8 (2026-10-18)

- Keep configuration fields and list actions on shared controls with current hover explanations.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

## 0.1.7 (2026-10-15)

- First release on Thunderstore.
