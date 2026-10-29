# Wildblood

Creature stars that grow with the world, mutations, boss aspects, loot rules and bad-luck protection.

Version 0.5.12. Requires Crucible 0.3.37. Changes are in [CHANGELOG.md](CHANGELOG.md).

## Features

- Tamed Pressure is an optional host/server boss rule, off by default. For known bosses, every 5 seconds it selects up to 4 nearest living tamed MonsterAI creatures within 32 horizontal meters of each boss. Pressure schedules generic damage equal to 1% of maximum health per second. Tiny amounts accumulate into pulses that clear Valheim’s minimum-damage threshold; normal damage scaling still applies. Affected tames receive 25% more damage and deal 25% less damage. **This can kill tames.** Effects can last up to 5.5 seconds after leaving range or a boss dying; turning it off stops them immediately. Stars, max health and traits stay unchanged. This feature steps aside while BossRules is installed. Discovery is incremental: newly spawned or unloaded bosses can take longer to be found in large or changing worlds. Known-boss refresh is independent of discovery. Global discovery visits at most 128 objects and advances at most 128 ordinary/portal bucket steps per native Tick; a full empty-world pass needs at least 4096 ticks (about 68 seconds at 60 ticks/second), plus object visits. Local nearest-target selection covers a fixed area but its work depends on nearby creature density.

- Stars: creatures roll up to 8 stars from a difficulty you choose (per world), shown as small stars and large stars worth five. Health and damage can also rise with distance from the world center (off by default). Raid extra stars stop at that biome's difficulty cap.
- `4 - Traits / Tamed Stars` (on by default, set by the server) changes new births, and gives a parked tame its stored stars back while `1 - General / Stars Enabled` is on and `2 - Stars / Star Mode` is Wildblood. On, a newborn may inherit stars, and Shepherd follows this. A tamed creature that an older build left at the game's level, with a stored star roll, regains those stars at once if it is loaded, or the next time it loads. Its health stays the same share of the bar (a full bar stays full). Its size and color match the stars. The creature's owner writes the level and health, and other players see them. Bosses, fresh births without parking metadata, and creatures with no stored star roll stay as they are. Parked births can regain stored stars too; without a restore mark, the current level must match the saved parked level. The restore does not rewrite mutations. Off, a newborn keeps its parent's level, as the game does, and no bonus star is added. Turning it off does not remove stars a tame already has, including stars it just regained. Taming never removes stars, health, or mutations.
- World tier: the tier rises by one the first time anyone kills each listed boss, and each tier raises the odds of stars and mutations.
- Traits: creatures can carry a signature mutation and up to two modifiers, with colored stars and the mutation word before their name. Tamed creatures keep their traits. Flamebound burning ground burns through armor. Fire resistance and being wet still reduce it.
- Thieving and devouring creatures show an icon of what they hold.
- Bosses: boss stars, an aspect taken at the altar, a damage board for each player when a boss dies, hints toward the next point of interest, and no natural spawns near an active boss.
- Loot: server-tunable loot rules (extra rolls or more of each drop per star, optional trophy multiplying).
- `6 - Loot / Seasonal Meat` and `6 - Loot / Seasonal Wood` (on by default, host or server): while Solstice seasons are on, meat, neck tails, hides, pelts and leather from creatures, and Wood, Fine wood, Core wood, Ancient bark and Yggdrasil wood from felled trees, follow that season's rate. Spring and summer drop less. Fall and winter drop more. The rate stays between one quarter and three times the usual amount. A rate below one can turn a single drop into nothing: the owner rolls that fraction once, so a rate of one half keeps about half of those single drops. A rate above one rounds up, and a stack does not grow past the larger of 100 and the amount it already had. Trophies, feathers, fangs, scales, coins and other drops stay as they are. Off, or without Solstice, or with seasons off, drops stay as they are. Creature stars are unchanged.
- Bad-luck protection for rare drops, saved with your character per world and credited to the player who made the kill. Safely identified rare single-item drops share star-tier progress, weighted 1/2/4 for zero/one/two stars. Ambiguous old counters retain their existing behavior. Attributed multiplayer decisions require matching Wildblood 0.4.x peers; older peers cannot write the new decision protocol. After a restart, an exact retained result can finish recipient recovery without repeating a death or recreating drops. A distinct durable recovery receipt releases the recipient for a different source; it does not fabricate producer acknowledgment or clear the original source fence. Bounded committed counter history can reconcile an exact older saved image with the durable head. Unresolved outcomes, conflicting images, and saves outside retained proof remain held.
- Pressure: killing wild creatures raises Karma in the surrounding area, which adds stars to new spawns there and fades over time; a player dying there lowers it. High Karma can call an Enforcer, a strong creature with minions and extra loot. Creature health and damage also scale with nearby players.
- Crouch and look at a creature to see its resistances and weaknesses.
- Karma inside the minimap, and the world tier for Mantle's square beside it.
- Death recap: replay the seconds before your last deaths, from the Meadworks menu (F8 or Esc > Meadworks).
- Lore: one "Wildblood mutations" entry in the game's Texts list (Compendium) lists every mutation, then the modifiers, boss aspects and difficulty, with their current values. The Meadworks menu, `wildblood lore` and the optional lore key open the Compendium on that page; players can also inspect creatures (host setting Players May Inspect, on by default).

## Requirements

- BepInExPack Valheim 5.4.2360
- Jotunn 2.30.2
- Crucible 0.3.37 or a later 0.3.x, which includes Crucible.UI 0.3.25 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Wildblood with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Wildblood` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Wildblood on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.wildblood.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- Recovery receipts and counter checkpoints require Wildblood **0.4.2 or later** to read. An older version refuses these journals rather than treating them as empty. Keep the character, world and matching recovery files together when backing up or restoring; an arbitrary older character save is not automatically accepted.
- Each module (Stars, Traits, Bosses, Loot, Luck, Pressure) can be turned off on its own. Difficulty, star mode, loot mode and world tier can be set per world.
- In multiplayer the server's values win for server-synced settings, and admins can lock settings; locked settings show as read-only. Display and recap settings are your own.
- Cheats: `Drop Multipliers`, `Lucky Kills` and `Creature Commands` are off by default; in multiplayer they stay locked unless the server allows them.
- With `Creature Commands` on, confirmed admins get `wildblood spawn <prefab> <stars> [mutation... | boss aspect] [mod:<modifier>...]` (up to four native modifiers; one with the same effect as a mutation is dropped), `wildblood purge [radius]` and `wildblood karma [value]`; these commands run from a game client, not from a dedicated server console. `karma` works in single player and for the hosting player only.
- General console command root: `wildblood`. Everyone: `wildblood lore`, `reference`, `tier`, `theft [status|recover]` and `inspect` (admins only when the host turns Players May Inspect off). Admins: `wildblood effects <text>`.
- Protected Prefabs (default `FrozenKing_p2`, whose health drives its encounter) are never starred, traited or scaled, drop the game's own (vanilla) loot, and `wildblood spawn` makes them plain. Creatures rolled before a change keep what they rolled.
- Distance Scaling (off by default) makes creatures stronger the farther from the world center they are rolled.
- It applies a `distanceScaling: [damageStep, healthStep, stepMeters, maxSteps]` band once, where each creature is rolled. The default rules ship the example band `[0.03, 0.08, 1000, 5]` (+3% damage and +8% health per 1000 m from the world center, up to 5 steps) on every creature but bosses; write your own on a `biomes:` or `creatures:` rule to change it. `modifierDistanceScaling` raises modifier chances the same way.
- Respect Enemy Level Up Rate multiplies the odds of every star count by the world modifier's enemy level-up rate.
- Tamed Keep Traits (on by default) lets tamed creatures keep their traits; off, a tamed creature's mutations and modifiers do not act while it is tamed (they stay stored and come back when the setting does; its stars stay). Young born while it is off inherit no mutation. Traits already stored on a tame stay stored.
- `4 - Traits / Tamed Stars` (on by default) is set by the server. Shepherd reads that choice and does not use its own Tamed Stars value. On, while `1 - General / Stars Enabled` is on and `2 - Stars / Star Mode` is Wildblood, a tame an older build left at the game's level regains its stored stars. Health stays the same share of the bar. Size and color match the stars. Off does not remove stars a tame already has. Bosses, fresh births without parking metadata, and creatures with no stored star roll stay as they are. Parked births can regain stored stars too; without a restore mark, the current level must match the saved parked level.
- Scale Saddle-able Creatures off keeps creatures that take a saddle at their normal size at any star count.
- Health Per Extra Player, Damage Per Extra Player and Max Players For Scaling set the game's own multiplayer difficulty while the Pressure module is on. Karma and Enforcers are not affected.
- These three settings stand down, leaving the game's or the other mod's values alone, while CreatureManager (or a plugin GUID on the server's Yield Scaling To list) is installed, and for the rest of the session once another mod is seen changing those values; Wildblood says so once.
- Keys: the death recap and the lore open from the Meadworks menu. Their own keys are unbound by default; set them (or a lore gamepad chord) in the Recap and Display settings.

## Abandoned summoned bosses

`5 - Bosses / Abandoned Bosses` is off by default and controlled by the host or dedicated server. When enabled, a recorded summoned boss must remain at full health with no player inside `Abandon Range` (100 meters by default) for `Abandon Minutes` (10 by default). A nearby player or missing health resets the timer. The server then removes the abandoned summon and returns its original offering at the altar once. A normal kill gives no refund.

The altar refuses another summon while its recorded boss lives or the previous summon is recovering. Summon records and pending refunds survive owner changes and world reloads. Keep the world's matching Wildblood journal files when backing up or restoring. If recovery cannot prove the previous outcome, the altar stays held instead of producing another offering. Twins share the summon and its one original offering; phantom offspring do not create another refundable offering.

Pick up refunded offerings manually with room in your inventory. Automatic pickup stays off for these items. The first successful claim reserves that stack for that character, including after reconnecting. Wildblood saves the recipient's inventory before confirming delivery and keeps the consumed token so a world rollback cannot grant that stack again. Keep both the server's and each player's Wildblood journal folders with their matching world and character backups.

This applies to altar summons recorded by this version. Existing bosses and creatures created by commands are not adopted for refunds. The setting and ledger run on dedicated servers without a display. Native multiplayer, crash and reload checks remain required before release; this unreleased build has not been qualified in game.

## Paid raids

In single player or a player-hosted world, the hosting player can build the original Raid chest from the Hammer menu. It uses the native chest recipe, ward protection and inventory behavior. Place it in a base with at least three active base pieces, and put Coins inside. Nearby crafting stations can provide the base requirement.

Stand within five meters of the closed chest and run `wildblood_raid start <coins> [tier] [request-id]` in the console. The optional tier (0 leather to 7 Deep North) weighs the stake against that gear tier instead of the strongest player nearby. The stake is spent once before the raid starts. Reusing the same request ID retries that admission without a second payment or a new event. The command also works with an eligible native chest. Connected players can request a raid on a dedicated server. The server checks the authenticated player, distance, chest access and saved inventory before using the same payment journal. An idle chest owned by the requesting player can move to server ownership only after its saved inventory revision matches; a busy, held or active chest is not taken over. If chest data is still syncing, wait and retry with the same request ID.

On a dedicated server, the raid can load a bounded area around a distant base while the request waits. No coins are taken until the player, chest, terrain and nearby objects are ready. This leaves the server's global reference position unchanged. A server refusal because loading timed out takes no payment. If the client receives no reply, the outcome is unknown; retry a start with the same request ID. The area stays loaded during the raid and is released after it ends.

The raid has a countdown and three waves. Raiders drop their assigned Coins when they die; stopping or abandoning a raid does not refund the stake. An authorized nearby player can run `wildblood_raid stop`; connected clients send it to the server. Saving and reloading an interrupted raid preserves its paid identity and inventory and abandons the old event instead of charging or starting it again.

The original chest opens and closes automatically for the player using it and for observers, and retains its contents through native saves. Raids and new Raid chest construction stand aside while EliteCreaturesReborn is installed. Existing placed chests remain available. Original art is CC BY-NC-SA 4.0; corresponding source and full terms are in `assets/raids/source/`.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- A player who just joined does not roll nearby creatures, or apply their stats, from that player's own rules before the server's rules arrive. Those creatures stay as the game spawned them until the server's rules are in.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Wildblood turns off for that session and cheats stay locked.
- Players without Meadworks (vanilla clients) cannot join a server running Wildblood. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Optional creature defaults: [the `meadworks.creatures.rules` v1 contract](docs/creature-rules-capability.md) accepts Bestiary's declarations through Crucible and synchronizes the host's effective values.

- Steps aside, per module and only while the other mod's own feature is switched on:
  - EliteCreaturesReborn: Wildblood turns off its stars, traits, bosses, loot and death recap.
  - CreatureManager: Wildblood turns off its stars, traits, Karma and Enforcers, and loot.
  - SaveYourLuckBruh: Wildblood turns off its bad-luck protection.
  - DropNSpawn: Wildblood turns off its loot rules (setting `Defer To DropNSpawn`, on by default).
- Meadworks: with Cairn and Mantle installed, creature status and protected inventory cells use their displays.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Wildblood

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Stars, traits, aspects and loot rules adapt EliteCreaturesReborn by Gerald Glasgow (GPL-3.0-or-later); the loot amount, distance scaling, karma command and protected prefabs adapt CreatureManager by sighsorry1029 (GPL-3.0-only); the bad-luck counter adapts SaveYourLuckBruh by Pete Navarra (aka Vapok) (MIT).

Original code GPL-3.0-or-later; files adapted from CreatureManager are GPL-3.0-only, so the assemblies are distributed under GPL-3.0 (see `LICENSE-CODE.md`, `LICENSE_SCOPE.md`). Documentation CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`).

Author: expDARE.
