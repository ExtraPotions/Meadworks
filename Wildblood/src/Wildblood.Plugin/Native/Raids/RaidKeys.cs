// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// Every ZDO key and RPC name the raids use, in one place, all prefixed "ecr_raid_" so they never meet the game's keys
    /// or another feature's. Three kinds of object carry them: the raid host (a Raiders Chest, or the invisible marker of a
    /// test raid) holds the raid itself, read by <see cref="RaidState"/>; each raider carries its tag, read by
    /// <see cref="RaiderTag"/>; each player carries its published gear tier (<see cref="GearTier"/>). The hashes beside
    /// the names are computed once, for the paths that read every frame (the HUD line) or every tick.
    /// </summary>
    public static class RaidKeys
    {
        // ---- The raid, on the host's ZDO. Written by the host's owner only. ----

        /// <summary>The <see cref="RaidPhase"/>, as an int. 0 (Idle) on a chest that has never raided.</summary>
        public const string Phase = "ecr_raid_phase";

        /// <summary>The wave running or last run, 1 to <see cref="RaidTable.Waves"/>; 0 during the countdown.</summary>
        public const string Wave = "ecr_raid_wave";

        /// <summary>The heat fixed at the start: the stake divided by the fair stake of the gear <see cref="Tier"/>.</summary>
        public const string Heat = "ecr_raid_heat";

        /// <summary>The <see cref="RaidBand.Index"/> the heat fell in at the start (0 Trivial to 4 Deadly).</summary>
        public const string Band = "ecr_raid_band";

        /// <summary>The gear tier (0-6) the heat was fixed for: the strongest player near at the start, or the command's.</summary>
        public const string Tier = "ecr_raid_tier";

        /// <summary>The coins the raid cost: every coin taken out of the chest, or the command's amount.</summary>
        public const string Stake = "ecr_raid_stake";

        /// <summary>Every coin the raiders carry between them: the stake times the band's coins back.</summary>
        public const string CoinsBack = "ecr_raid_coins_back";

        /// <summary>The coins still to hand out to raiders not yet spawned. Starts at <see cref="CoinsBack"/>; each raider's
        /// share is taken out of it as the share is written into the raider's tag (<see cref="RaidPurse"/>).</summary>
        public const string CoinsUnspent = "ecr_raid_coins_left";

        /// <summary>The raiders' loot multiplier now: the band's at the start, lowered when a stronger player arrives
        /// (<see cref="RaidDrops"/>). Read on each raider's owner as it dies (<see cref="RaiderPurse.DropsFor"/>).</summary>
        public const string Drops = "ecr_raid_drops";

        /// <summary>Server clock, whole milliseconds: when the raid was sounded. Also the raid's id in its raiders' tags,
        /// so a raider left over from an earlier raid of the same host knows it is not this one's.</summary>
        public const string StartedAt = "ecr_raid_start";

        /// <summary>Server clock, ms: the time limit (<see cref="RaidTable.TimeLimitSeconds"/> after the start).</summary>
        public const string Deadline = "ecr_raid_deadline";

        /// <summary>Server clock, ms: when the countdown or the break between waves ends.</summary>
        public const string PhaseUntil = "ecr_raid_next";

        /// <summary>The game event the raid was drawn from (<c>RandomEvent.m_name</c>), whose spawn list it brings.</summary>
        public const string Event = "ecr_raid_event";

        /// <summary>The raid's name as players read it, "Fuling raid".</summary>
        public const string Name = "ecr_raid_name";

        /// <summary>The raiders of this raid alive now, as <see cref="RaidRoster"/> counted them on the last tick.</summary>
        public const string Alive = "ecr_raid_alive";

        /// <summary>The raiders of the current wave still to beat: alive plus still to arrive. The HUD's "5 left".</summary>
        public const string Left = "ecr_raid_left";

        /// <summary>Server clock, ms: the last time a player was within <see cref="RaidTable.RaidRadius"/>, stamped
        /// every few seconds. The abandonment clock.</summary>
        public const string SeenAt = "ecr_raid_seen";

        /// <summary>The peer id (<c>ZNet.GetUID</c>) of the machine that ran the last tick, to tell a logout from an
        /// ordinary hand-over when another machine takes the raid over.</summary>
        public const string Runner = "ecr_raid_runner";

        /// <summary>True when, on the last tick, no player but the runner's own (or none) was within range.</summary>
        public const string Alone = "ecr_raid_alone";

        /// <summary>The server's peer id when the raid started: a different one now means the server restarted.</summary>
        public const string Server = "ecr_raid_server";

        /// <summary>Server clock, ms: before this no new raid may be sounded at this chest (one per in-game day).</summary>
        public const string CooldownUntil = "ecr_raid_cooldown";

        /// <summary>How the last raid ended, a <see cref="RaidEnd"/> as an int; 0 while one runs.</summary>
        public const string Ended = "ecr_raid_ended";

        /// <summary>Server clock, ms: when the last raid ended.</summary>
        public const string EndedAt = "ecr_raid_ended_at";

        /// <summary>The current wave's direction from the host, degrees (<see cref="RaidWaves"/>).</summary>
        public const string WaveDirection = "ecr_raid_wave_dir";

        /// <summary>The current wave's raiders still to arrive (<see cref="Raids.WavePlan"/>), so a new owner carries the
        /// wave on.</summary>
        public const string WavePlan = "ecr_raid_wave_plan";

        // ---- The raider tag, on each raider's ZDO. Written at its spawn (RaiderTag.Write); the target and role later on
        // the raider's owner (RaiderGoal), the coins as it is stopped or walks off. ----

        /// <summary>The ZDOID of the raid's host (chest or marker).</summary>
        public const string RaiderHost = "ecr_raid_host";

        /// <summary>The raid's id (<see cref="StartedAt"/> of the raid it was spawned for).</summary>
        public const string RaiderRaid = "ecr_raid_of";

        /// <summary>The <see cref="RaiderRole"/>, as an int.</summary>
        public const string RaiderRole = "ecr_raid_role";

        /// <summary>True on the last wave's Warlord.</summary>
        public const string RaiderWarlord = "ecr_raid_warlord";

        /// <summary>The coins this raider carries and drops as it dies: 0 once it walks off with them, -1 when a stop
        /// killed it (<see cref="RaiderKill.NoDrops"/>).</summary>
        public const string RaiderCoins = "ecr_raid_coins";

        /// <summary>The ZDOID of the building piece it is going for now (the Raiders Chest, a chest, a station).</summary>
        public const string RaiderTarget = "ecr_raid_target";

        // ---- The player. ----

        /// <summary>The player's gear tier 0-6, written by that player's own client into its player's ZDO on change.</summary>
        public const string GearTier = "ecr_raid_gear";

        // ---- RPC names. ----

        /// <summary>Routed, to one peer: a raid message (string) for its player's screen.</summary>
        public const string MessageRpc = "ecr_raid_message";

        /// <summary>On the host's ZNetView, to its owner: a player asks to stop the raid (the chest's hold E).</summary>
        public const string StopAskRpc = "ecr_raid_stop_ask";

        /// <summary>On the host's ZNetView, to its owner: "my player is within the raid", with its gear tier (int), every
        /// few seconds from each client near a raid it does not run, so the owner counts players it does not hold.</summary>
        public const string HereRpc = "ecr_raid_here";

        /// <summary>Routed to the owners of a stopped raid's raiders; each kills its own with no drops
        /// (<see cref="RaiderOrders"/>).</summary>
        public const string StopRaidersRpc = "ecr_raid_stop";

        /// <summary>On the Raiders Chest's ZNetView, to each player within the message range: the horn sounds
        /// (<see cref="RaidHorn"/>).</summary>
        public const string HornRpc = "ecr_raid_horn";

        /// <summary>The ZDOID key pair of <see cref="RaiderHost"/>, made once.</summary>
        public static readonly KeyValuePair<int, int> RaiderHostPair = ZDO.GetHashZDOID(RaiderHost);

        /// <summary>The ZDOID key pair of <see cref="RaiderTarget"/>, made once.</summary>
        public static readonly KeyValuePair<int, int> RaiderTargetPair = ZDO.GetHashZDOID(RaiderTarget);

        /// <summary>The hash of <see cref="GearTier"/>, made once: read for every player near a raid on every tick.</summary>
        public static readonly int GearTierHash = GearTier.GetStableHashCode();
    }
}
