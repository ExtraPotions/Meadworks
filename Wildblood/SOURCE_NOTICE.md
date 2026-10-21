# Source availability notice

Product author: expDARE.

Wildblood.Core.dll and Wildblood.dll contain logic adapted from EliteCreaturesReborn 3.20.0 (pinned at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GPL-3.0-or-later; the boss-hint RPC, the Hotkey modifier read and the Echoing boss aspect follow EliteCreaturesReborn 3.21.0 and ValheimModLibs at 9da28eb58adbe065be1e4baef779a448acd38e0f), CreatureManager 1.2.5 (pinned at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473, GPL-3.0) and SaveYourLuckBruh 1.0.2 (pinned at ece802da9f806f3ee9d6e50552beec479abae4ce, MIT). The Flamebound, Piercing, Howling and Binding mutations and Mending's current-health heal follow the described behaviour of EliteCreaturesReborn 3.22.0 only; no 3.22 source was read or adopted. Never-adopt files (ECR ServerLock.cs and globe.png, CreatureManager ServerSync.dll and textures, SaveYourLuckBruh images) and other images are not included.

The Corresponding Source for this version is at https://github.com/ExtraPotions/Meadworks/tree/main/Wildblood. No copyright transfer is inferred. Original upstream copyright and license notices remain intact.

The native closure additionally adopts ValheimModLibs at the ECR pin, with its GPL-3.0-or-later notice retained. YamlDotNet 16.3 is privately internalized under its retained MIT notice.

The wired settings adopt CreatureManager 1.2.5 at its pin (GPL-3.0-only): `distanceScaling` stat bands (Core `DistanceScaling`, from CreatureLevelManager.cs GetDistanceScalingMultiplier and the spawn policy sources), the Protected Prefabs rule (CreatureManager's FrozenKing_p2 check, now read from the setting) and the `wildblood karma` verb (CreatureConsoleCommands.cs ExecuteKarma). The Respect Enemy Level Up Rate odds multiplier (the game's `Game.m_enemyLevelUpRate`) and the `wildblood spawn|purge|effects` verbs (previously `elite`) follow EliteCreaturesReborn 3.20.0; the `elite` console root and its admin RPC (EliteCommands.cs, CommandAccess.cs, CommandAccessPatch.cs) are no longer in the tree.

The local theft recovery phases reuse expDARE's Timber baseline `8b9c5adfbc346adca1e3acae110aeecc9b0fd068` under GPL-3.0-or-later: InventoryDecisionJournal, DurableInventoryParticipant, SiteDeliveryClientLedger, NativeDeliveryInventory and DeliveryInventoryGuards. Their original Burrow baseline `14f92c83fdfced32a0439fac7164718306a09e6e` notices are retained. The journal and participant copies change only the namespace; the other files are player-only adaptations. No Burrow chest allocator or optional resource dependency is copied into the theft path.

The 2026-10-15 correction overlay adapts CreatureManager 1.3.1 dungeon placement from `e0a216a1ab73fc9fbec45060ed46dae031859b5f` (GPL-3.0-only) and EliteCreaturesReborn death-brood exclusions from `f331465b3ed2551c89decc42ba81565a7b689543` (GPL-3.0-or-later). The original base adoption pins and notices remain. Unrelated newer features are outside this correction overlay.

### 2026-10-15 Horde slice

Natural-spawn Horde logic is adapted from EliteCreaturesReborn at `f331465b3ed2551c89decc42ba81565a7b689543` (GPL-3.0-or-later): six Horde files, OwnDrops, fractional Dice, and the existing health, attack, spawn, loot and death integration seams. The original adoption pins and notices remain. Adaptations use Wildblood server authority, step-aside, existing controller lifecycle and one loot postfix. That Horde overlay did not include raid code or assets. The later raid adoption is recorded separately below.


## 2026-10-15 boss progression and materials

ECR at `f331465b3ed2551c89decc42ba81565a7b689543` supplies the boss reward table, star-boost policy and two YAML fields. Wildblood retains upstream GPL notices and its existing trophy formula. The world/owner receipt adapter and native death-only emission scope are local integration.

Loaded crossbow continuity adopts the two behavior files from EliteCreaturesReborn commit 51abb440b89313d2130519116b83f9c65a7c11de (GPL-3.0-or-later). Owner-scoped weak memory, native guards and existing lifecycle integration are local changes by expDARE. No item custom-data or save format is added.

### 2026-10-15: CreatureManager player target switching

Wildblood adapts the owner-local player target selector and the narrowly scoped `MonsterAI.UpdateTarget` wrapper from CreatureManager 1.3.1, pinned at `e0a216a1ab73fc9fbec45060ed46dae031859b5f`. The three adopted files retain GPL-3.0-only notices; the original license remains in `THIRD_PARTY_NOTICES/CreatureManager-GPL-3.0.txt`. Adaptations use effective Wildblood settings and authority gates, preserve Relentless quarry/Devouring provocation, and clear owner-local state with Wildblood lifecycle.

### 2026-10-15: newer Flamebound visuals

The creature Burning cosmetic and shared ground flame renderer adapt EliteCreaturesReborn at `f331465b3ed2551c89decc42ba81565a7b689543`, GPL-3.0-or-later. Wildblood uses its existing owned component/visual lifecycle, a portable emission policy and live ember fallback; missing flame systems release their cosmetic copy. The fallback/default rule shape is 7 seconds and 1 metre radius while explicit existing YAML is retained. Existing strongest-patch/once-per-second fire damage is unchanged; the older FlameDemist feature is outside this slice.


## 2026-10-15 BossProgress authenticated request transport

The seven-family boss progression behavior and retained encounter receipts keep their existing ECR attribution. Only the newly integrated BossProgress transport changes from native routed sender claims to the existing Crucible Operations connection-authenticated request path. Its original immutable packet/phase-coalescing helper is GPL-3.0-or-later, copyright2026 expDARE. Owner admission uses the actual connection peer UID; player ID and source ZDO creator ID do not authenticate creature ownership. Server-owned deaths retain synchronous internal validation. Other native RPCs, material rewards, terminal source requirements and receipt semantics are unchanged.

## 2026-10-16 weighted pity

The weighted-pity helper, conservative named-counter conversion, scoped native integration and protocol advertisement are original MeadWorks changes by expDARE. SaveYourLuckBruh 1.1.0/1.1.1's public behavior and current source at `49fba3f3c46e838b552164007be7775efc224c60` were inspected; no new upstream implementation was copied. Existing MIT and GPL notices and pinned adopted source remain intact.

## October 16, 2026: raid adoption and owned adaptations

The 58 adopted raid files derive from EliteCreaturesReborn commit `f331465b3ed2551c89decc42ba81565a7b689543` (GPL-3.0-or-later), not the older `ad278ecf` baseline. Their existing headers still name 3.20.0; that historical header text does not identify the later adoption pin stated here. The small original RaidDropPatch is not represented as an upstream adoption.

The owned BossProgress terminal-proof adaptation was made in `b8f4dccda4e36f46e7f860ec2e0c8d6df2333cb2` and the Splitter/DeathPatch raid-lineage adaptations in `2cd0287e97b2ef6137b42a83460ae98f836fa60e`.
