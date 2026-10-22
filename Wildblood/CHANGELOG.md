# Changelog

## 0.5.5 (2026-10-21)

- Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- Wildblood publishes the optional `meadworks.creatures.rules` v1 contract. Creature defaults from Bestiary follow the host's synchronized rules, including live Cloaked reveal distances, mutation and aspect exclusions, body size, devouring protection and loot classification. Explicit creature YAML values retain precedence. Clients keep their saved local preferences.
- Due births wait for Wildblood's server rules before star or mutation inheritance, including births managed by Shepherd. Their pregnancies remain due until the rules arrive.
- Wildblood publishes the optional `meadworks.wildblood.breeding_ready` v1 capability. Shepherd reads it to hold a due birth until the server's rules have arrived.
- Raid start and stop requests from a connected player can run on a dedicated server through the host's existing stake journal. The server validates the player, chest access, distance, stake and request before admission.
- On a dedicated server, a raid request can load a bounded area around a distant base while it waits. No coins are taken until the player, chest, terrain and nearby objects are ready, and a refusal because loading timed out takes no payment. While a raid runs or holds its chest, requests to open, stack into or take all from that chest are refused, and so is a claim of its ownership.
- Parked offspring, hatched eggs and grown creatures can regain stored stars when Tamed Stars is turned on. A saved parked level must match the current level unless an unfinished restore is marked; fresh births and existing legitimate stars are preserved.
- Seasonal Wood scales only wood entries after a tree's drop list is rolled. Resin, seeds and other entries in mixed tree tables keep their original rolls and amounts.
- Seasonal meat, hide and wood rates below one can now reduce a single drop to zero. The creature or tree owner rolls each fractional remainder once, so a rate of one half keeps half of single drops on average. Season increases keep their existing rounding and stack caps; trophies and other creature drops are unchanged.
- Tamed Stars on: a tamed creature that an older build left at the game's level, and that still has a stored star roll, gets those stars back. Health stays the same fraction of its bar (a full bar stays full), and its size and color match the stars. A loaded creature changes with the setting. One that is not loaded changes the next time it loads. The creature's owner writes the level and health, and other players see them. Turning Tamed Stars off does not remove stars a tame already has, including stars just restored. Taming still does not remove stars, health, or mutations. A creature with no stored star roll stays as it is. Mutations are not rewritten. One log line names how many creatures were restored together.
- The Tamed Stars setting help matches that restore in all seven languages. Health stays the same share of the health bar. A full bar stays full, and an empty bar stays empty. Size and color match the stars. Bosses, fresh births without parking metadata, and creatures with no stored star roll stay as they are. The restore still needs Stars Enabled and Star Mode Wildblood.

## 0.5.4 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Seasonal Meat and Seasonal Wood (on by default, set by the host or server): while Solstice seasons are on, meat and hide from creatures, and wood from trees, follow that season's published rate. Spring and summer drop less, fall and winter drop more, between one quarter and three times the usual amount. One item stays one item. Trophies and other drops stay as they are. Off, or without Solstice, drops stay as they are. Creature stars are unchanged.
- Raids: extra stars stop at that biome's difficulty cap. They no longer stop at a fixed 8.
- A player who just joined no longer rolls nearby creatures, or applies their stats, from that player's own rules before the server's rules arrive. The creatures stay as the game spawned them until that package is in, then use the server's rules.
- Flamebound burning ground burns through armor. Fire resistance and being wet still reduce it.
- Tamed Stars (on by default, set by the server) changes new births only. On: a newborn may inherit stars, and Shepherd follows this. Off: a newborn keeps its parent's level, as the game does, and no bonus star is added. Taming never removes stars, health, or mutations. A creature an older build left at the game's level gets its stored stars back the next time it loads.
- Tamed Keep Traits off: young born while it is off inherit no mutation. Traits already stored on a tame stay stored.

## 0.5.3 (2026-10-20)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- Theft history no longer grows forever. When a character loads into a world with more than 64 settled thefts, Wildblood keeps the newest 32 and removes the older ones from its theft journal, their two saved receipts from the character, and any leftover world row. It removes nothing while any theft is still waiting for an answer, a refund or a confirmed character save. Journal records go first and receipts after, through a verified copy, so a pass interrupted by a crash or power loss is finished safely on the next load. One log line reports the counts. Before, each character could record about 4,096 thefts per world; after that, thieves could no longer steal from that character.

## 0.5.2 (2026-10-20)

- Requires Crucible 0.3.24 / Crucible.UI 0.3.17.
- Item safety: a chest or character inventory that Wildblood holds (a theft debit or a raid stake settling) now reloads completely. The game reloads an inventory by clearing it and adding every item back; Wildblood's guard refused those adds, so a reload during the hold left the inventory empty in memory, and a save at that moment could write the empty inventory. The adds made by the game's own inventory load now always run, so the reload replaces the items exactly once; outside a load, the hold applies as before. With Burrow installed, both guards let the load through and each item is added once.
- Raid chests: Wildblood's own hold, receipt and identity keys no longer make an up-to-date chest re-read its items, so a raid stake is no longer refused because the chest re-created its items during the hold.
- Provenance tools: `adopt-cm-pressure.py`, `adopt-cm-spawn.py` and `adopt-ecr-native.py` have a `--check` mode that writes nothing and works with the reference snapshots that have no git metadata.

## 0.5.1 (2026-10-20)

- Requires Crucible 0.3.23 / Crucible.UI 0.3.16.
- The theft hold guard enforces only Wildblood's own holds; Burrow's holds stay with Burrow's guard. Before, it also refused the item adds of the game's chest reload on a chest Burrow held for Shared Materials, which left that chest empty in memory.
- Item safety: while Wildblood holds an inventory (a theft or raid chest settling), other mods' item writes are no longer silently dropped. Writes that answer (adding items, removing a specific item) are refused with false and nothing changes, as before. Removals that cannot answer (remove by name, remove all) are now kept and applied in order once the hold ends, so the caller's payment still happens; at most 256 wait, beyond that they run at once. Moves without an amount are still skipped on both sides. Loading an inventory (character load, a chest's data from its owner) is never blocked.
- With Burrow installed, Wildblood's guard runs after Burrow's identical guard and leaves alone a write Burrow already refused or kept, so a kept removal is applied exactly once. If Wildblood stops while removals are waiting, they are applied before its guard is removed.
- Spawning no longer holds the player inventory because of settled theft history: the hold starts only for a theft that still needs reconciliation or whose saved character receipt is not confirmed.

## 0.5.0 (2026-10-19)

- Update required to join servers running this version: players on older versions must update first.
- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.
- Simpler in-game settings: Wildblood marks its essential settings (the product switch, Difficulty, Show Trait Names, both personal display switches for boss hints and the Karma line, Record Deaths and Recap Key) for the MeadWorks menu, and marks rarely changed ones (protected prefabs, boss keys, loot roll tables, luck bookkeeping, scaling step-aside, the star palette, the lore pad chord and the rule snapshot) as normally edited in the config file. Almanac's all-mods view still lists every setting. The lore now lives on one Wildblood page of the game's Compendium, which gains the Difficulty section; the Meadworks menu, `wildblood lore`, Lore Window Key and Lore Window Pad Chord open the Compendium on that page instead of a separate lore window. Config keys, values and scopes are unchanged.
- Keep crossbow loaded has one owner: with Edge installed, Wildblood's copy steps aside and Edge's Keep Crossbow Loaded setting decides, as it already did for EliteCreaturesReborn.
- `wildblood inspect` is open to every player by default. The new host setting Traits / Players May Inspect (on by default) controls it; off, only admins can inspect. Inspect only reads a creature's synced stars and traits, so it is a host rule, not a cheat, and the Players May Use cheat list does not apply to it.
- `wildblood_raid start <coins> [tier] [request-id]` takes an optional raid gear tier, 0 (leather) to 7 (Deep North), that weighs the stake against that tier instead of the strongest nearby player. Other values are refused. Retrying a request ID with a different tier is refused as a conflict.
- `wildblood spawn` can force native modifiers: `mod:<modifier>`, or a bare modifier name that is not a mutation (for example `wildblood spawn Troll 3 Thieving mod:Swift Armored`). It accepts up to four. They are applied after the creature's traits, so a modifier that has the same effect as one of its mutations is dropped and reported; the mutation wins.
- Raids: Deep North gear (Bloodgold, or a Deep North armor set) is now raid tier 7 with its own fair stake of 2400 coins, one step past Flametal's 1800, instead of counting as Flametal. Heat bands, waves and rewards are unchanged; Deep North raids come from the game's own events (the Elaking and Jotun armies) once the world unlocks them. Raid records of tiers 0-6 keep their old format; a tier 7 record uses a new format that older Wildblood builds hold for recovery instead of misreading it.
- Deep North: Kall Fimbulbringer's final defeat (`defeated_frozenking_p3`) is a new default world tier boss key, so tiers now run 0 to 8; tier 8 has its own preset row and Override accepts 8. A saved Boss Keys list that is exactly the old seven-key default updates to the new default; a customized list is kept and the log names the key to add.
- Kall's final phase (FrozenKing_p3) counts toward boss progress, pays 5-10 Petrified Tissue (GoldOre) per share, offers the Inscribe Legendary reward and leaves a boss hint toward the Bog Witch. His first two phases never count, pay or hint.
- Summoner can call Krigen and Hexen (JotunWarrior, JotunWitch) for FrozenKing and FrozenKing_p3. Portalbound stays limited to The Elder and Bonemass.
- Protected Prefabs now defaults to `FrozenKing, FrozenKing_p2`; a saved `FrozenKing_p2` (the old default) updates to it, and any other saved list is kept. An altar whose boss is protected no longer shows stars or an aspect it cannot give.
- Boss kills and won raids offer Inscribe a Legendary item through `meadworks.inscribe.legendary_reward`; Inscribe's host settings decide whether one drops. Nothing changes without Inscribe.
- Settings help: module toggles, Karma Mode, modifier caps, Modifier Icon Layout, star sizes, Horde attack and AI targeting describe their effect; the two Boss Hints settings now name their server and personal roles. Keys, defaults and values are unchanged.
- Swift creatures now move, accelerate and turn faster by their configured power; the bonus joins the other movement bonuses once and is removed cleanly on reload or handoff.
- Traits that shared a display name now read differently in every language (for example, the Enraged modifier is now Furious and the Adaptive modifier is now Hardened in English); config keys are unchanged.
- A creature no longer carries a mutation and a modifier with the same effect (Leeching/Vampiric, Warding/Reflection, Plated/Armored, Mad/Swift, Mad/Attack Speed, Juggernaut/Unflinching, Juggernaut/Crushing, Flamebound/Fire, Frostbound/Frost); the mutation wins, and saved creatures with both lose the modifier on load.
- Sneak Inspect (formerly Show Sneak Hover Resistances) shows the resistance lines under a creature's nameplate again while crouching.

## 0.4.6 - batch 4E follow-up (2026-10-19)

- Source provenance records repaired so the shipped source passes its adoption check. No gameplay change.

## 0.4.5 - batch 4E gameplay fixes (2026-10-19)

- Creature Manager notifications (CM_Enforcer and others) show their text again, and the debuff status effects are registered.
- Settings audit: affected settings apply live.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.4.4 (2026-10-18)

- Use typed local notices and keep status/boss HUDs visible during HUD layout editing while preserving ordinary blocking-window behavior.

- Route existing recap notices through the shared typed notice service with native fallback, and keep the status and boss HUDs available to the shared layout editor. Creature rules, loot and death outcomes remain unchanged.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12 for the shared UI/API integration. Broader 4D qualification is pending.

## 0.4.3 (2026-10-18)

- Correct the corrupted star characters in death notifications and avoid splitting a Unicode character at the name-length limit.
- Rotate three localized attacker messages and three environmental messages without changing gameplay randomness.
- Show existing lore fallback descriptions when native localization returns an unresolved token, without changing creature rules.

## 0.4.2 (2026-10-18)

- Record recipient recovery separately from producer acknowledgment. Exact retained outcomes can release the recipient for a different source while preserving the original source fence.
- Keep bounded counter-history proof through durable journal checkpoints so an exact recorded older character image can reach the durable head without repeating native deaths or drops. Unknown images and incomplete decisions remain held.
- Require 0.4.2 or later to read the new local recovery records. Preserve legacy stores; older versions refuse the new records rather than resetting them.
- All 390 Core checks and six focused native restart jobs passed, covering recipient release, a genuine later source, lagged counter saves, no-result holds, counter conflicts and foreign pending publication. Aggregate evidence and delivery are recorded separately.

- Recover retained results for attributed luck after an interrupted session: reconcile an exact durable outcome with the same local player/world counter image, and publish the pending operation. Preserve conflicting images, no-result holds, source fences and recipient exclusion. Five focused native restart controls passed; recovery does not replay physical drops or invent producer acknowledgment.

- Serialize attributed rare-drop decisions at the credited player owner before native death starts; require matching 0.4.x peers.
- Retain uncertain decisions and fence marked creatures across ownership changes while bounding acknowledged journal storage.

- Share safely identified rare-drop pity across native star tiers, with weighted progress and native quantity handling.
- Preserve ambiguous or opaque legacy counters on their existing path; retain world/character isolation and original saved records.
- Preserve legacy counter decoding while requiring matching peers for attributed owner decisions.

## 0.3.9 (2026-10-15)

- First release on Thunderstore.
