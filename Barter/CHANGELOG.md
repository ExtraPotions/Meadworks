# Changelog

## 0.1.2 (2026-10-22)

- 0.1.2. Barter is now a MeadWorks plugin everywhere: its plugin ID is `com.meadworks.barter` (was `com.extrapotions.barter`). Almanac lists it under Meadworks, and Crucible treats it like every other MeadWorks product.
- Your settings carry over. On the first start, `com.extrapotions.barter.cfg` is copied to `com.meadworks.barter.cfg` and the old file is kept as `com.extrapotions.barter.cfg.bak`. An existing `com.meadworks.barter.cfg` is never overwritten. Trade lists, purse data and trade receipts keep their files.
- Update every player and the server together: a 0.1.1 client and a 0.1.2 server (or the reverse) do not see the same plugin ID and cannot join.

## 0.1.1 (2026-10-21)

- 0.1.1. The sell list stays on screen at 800x600, 1280x720, 1600x900 and 1920x1080. It sits to the right of the store when that fits, otherwise to the left, otherwise below, and it does not cover the purse. It moves again when the resolution changes while the store is open. Requires Crucible 0.3.28 / Crucible.UI 0.3.20.

## 0.1.0 (2026-10-21)

- First release. Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- Per-trader buy and sell lists in YAML, JSON or CSV (TradersExtended lists read unchanged), custom currencies, a trader purse that moves prices and refills each morning, and a sell list beside the trader's window. Default lists for Haldor, Hildir and the Bog Witch.
- Server-checked trades with a server purse ledger, a trade journal that survives a crash or disconnect, Resolve pending trade, buyback, paid repairs, price tips, an in-game list editor for hosts and admins, per-trader policies and controller navigation.
- Checked in the game: hosted two-player 17/17 and dedicated server with two clients 15/15 (trades, full inventory, concurrent trades, editor, reconnect, store close and reload, lost sale and buy reports, crash recovery, old character profile restore, a trader spawned by a client, Hildir and the Bog Witch), then receipt-marker pruning on a hosted game and a dedicated server.
- The trader purse stays on screen. It keeps its place to the left of the store when that fits, and moves to the right of the sell list, or below the store, when the left side would run off the screen.

Development history:

- Prune character receipt markers after the authority has acknowledged the trade and its journal entry is gone, once the authority session or the sale's buyback day no longer needs them. Keep at most the newest 64 retired markers, and prune markers already stored on existing profiles when the character loads.
- Use trusted cooperative purse accounting for remote trades at server-verified prices: credit successful buys, keep one sale debit and refund a reported failure once. Preserve duplicate receipt handling and timeout quarantine; remote buyback and repairs still require authority inventory checks.
- Save completed trade receipts with the character before returning, bind markers to the authority quote, and retain consumed evidence on save failure. Old or ambiguous profiles never receive the same payout again through recovery.
- Stop unresolved receipts from serializing inventory every frame. Remove the 4,096-reservation session cutoff, rotate settled receipts from prior sessions, and count recovery capacity separately for each world.
- Verify trader identities against server-known, allowlisted spawn anchors before reading authority state. Keep authenticated state alongside the world, migrate existing records, and recover interrupted writes from valid pending files or backups.
- Translate all purse, list and interface settings in all seven languages. Refuse list drafts that exceed the encoded transport limit before sending, and explain unavailable remote buyback before a sale.
- Validate dedicated-server trades against the trader ZDO position and registered prefab stock, using that ZDO's authoritative purse for current prices without requiring a live trader instance.
- Track Barter's store open/close state through logout and isolate UI refresh failures from host list publication.
- Check sale payout room before reserving the trader's purse and disable Sell when payment cannot fit. Preserve space freed by sold stacks and show the existing recovery reason on blocked trades.
- Populate native offer names, icons, tooltips and reward/key fields so StoreGui can open and reload its list safely. Keep native full item tooltips and add deletion/default-regeneration coverage.
- Version item snapshots with a byte header and preserve Int32 world levels, including existing version-1 snapshots and their inventory evidence comparisons. Preserve world levels above 255 when creating purchased items.
- Finalize authenticated remote finish reports immediately and unlock the trader without waiting for timeout. Retain the existing restriction against unverified purse credits, refunds or buyback escrow; only missing reports need the 120-second quarantine.
- Add Resolve pending trade in Trader Services. Ask the authority to reconcile the receipt and retry proven outcomes without repeating item changes; keep uncertain mutation records and character-save evidence.
- Name Burrow inventory holds in trade refusals and add recovery text in all seven languages. Include full reload exception traces for any remaining native failure. Keep the plugin ID unchanged to preserve configurations.
- Declare reserve, finish and list-edit operations at Crucible's remote protocol 1.0 so Barter modules can start. Test the production descriptors against the built Crucible.Core registry with the real peer transport version, including rejection of the old declarations.
- Reserve and hide canonical buyback escrow before a permit instead of deleting it. Consume it only after verified success; verified rollback clears the reservation, while unknown outcomes keep item evidence past expiry. Explain the local-host-only service limit and disable remote offers, with text in all seven languages.
- Correct authority settlement: a remote success/failure report cannot credit/refund the shared purse or mint buyback escrow. Retain remote sale debits; confirmed reports release trader serialization immediately, and genuinely missing reports release it after 120 seconds without inventing an outcome. Remote purchases do not replenish the purse, and remote buyback/paid repair remain unavailable pending a verifiable inventory participant.
- Store canonical world/trader purse, escrow and receipt state in server files authenticated with a server-only HMAC key. Ignore unsigned ZDO balances/escrow as authority input and preserve legacy bytes as migration evidence.
- Retain completed journals until loaded character-save evidence confirms persistence; reconcile only exact before-images or matching loaded markers, retain uncertain mutation records and treat receipt mismatch as nonterminal. Persist per-token settlement outcomes to prevent replay after intervening trades.
- Run native store close checks before custom focus handling, keep a single Barter window/input owner and use the established kit art for sell rows. Preflight sale payloads against the 16,384-byte operation cap, including finish overhead, with an oversized-data refusal translated in all seven languages.
- Add trader/player-bound buyback escrow, paid repairs, server-priced item tips and a Crucible.UI list editor with server authorization, revision checks and durable file replacement. These paths remain development candidates pending native acceptance.
- Add validated per-trader policies and explicit configuration conversion, optional inventory-slot protection and controller navigation for the sell list and trader services. Preserve existing configuration keys and local preferences under server overlays.
- Persist interrupted trade evidence before inventory mutation and validate settlement against the reserved quote fingerprint. Ambiguous outcomes remain blocked for reconciliation; independent player/world saves are not atomic.
- Route list and native item trades through checked stack exchanges and authenticated server purse reservations. Refused removals, partial deliveries and full inventories compensate exact owned changes; uncertain inventory results block further exchanges for recovery.
- Replace client purse ownership claims with a canonical server ledger and a legacy display mirror. Revalidate price, currency, lot, quality, world requirements and sender proximity; serialize concurrent trades and reject replayed reservations.
- Preserve the native first-valuable-stack action when the sell panel is hidden. Native key/inventory-row rewards and remote personal discovery/key requirements fail closed until their authority and recovery paths are qualified.
- Add a translated trade refusal message in all seven languages and an explicit dry-run list importer that preserves source files and refuses overwrites or invalid lists.

Scaffold (2026-10-09):

- First scaffold, 2026-10-09: repository, Barter.Core with 183 portable tests, Barter.Plugin that builds against Crucible 0.3.12 and Crucible.UI 0.3.7, seven localization catalogs (English plus six translations), README, notices and provenance record.
- Adopted from TradersExtended 2.0.5 (shudnal, Unlicense): the list model and file names, CSV reading, availability rules, currency rule, price curve, purse refill, lot arithmetic and sell offer grouping; the trader hooks, purchase and sale flow and panel layout were written with the upstream as reference.
- New: lists synchronized from the server through Crucible and hot reloaded, YAML and JSON reading, the list set rejected as a whole on a mistake, quality and currency kept in a side table, the purse card and the sell panel on the Crucible.UI kit, step-aside when TradersExtended is installed.
- Default lists written on first run for Haldor, Hildir and the Bog Witch (buy and sell), checked by a test that they parse cleanly and name only vanilla items (against the game's item manifest when `VALHEIM_DIR` is set). The trader purse is on by default for every trader with a Barter list; unlisted modded traders keep vanilla prices.
- Not run in the game yet. The package icon is a coin purse, drawn in code in the Meadworks icon style (approved 2026-10-10).
