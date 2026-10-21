// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The tag that makes a creature a raider, in its own ZDO (features/raids.md section 5: raiders are tagged with the
    /// chest's id and their role). Written once on the machine that spawns it, in the same frame (<see cref="RaidWaves"/>,
    /// or <see cref="RaiderLineage"/> for a Splintering copy), so it travels with the creature to whichever machine owns
    /// it next: that owner steers it (<see cref="RaiderSteering"/>) and, when it dies, drops exactly the coins written
    /// here (<see cref="RaidCoins"/>). Only its owner changes it later: the building target and role
    /// (<see cref="RaiderGoal"/>), and the coins as a stop kills it or it walks off with them.
    /// </summary>
    public static class RaiderTag
    {
        private static readonly int RaidKey = RaidKeys.RaiderRaid.GetStableHashCode();
        private static readonly int RoleKey = RaidKeys.RaiderRole.GetStableHashCode();
        private static readonly int WarlordKey = RaidKeys.RaiderWarlord.GetStableHashCode();
        private static readonly int CoinsKey = RaidKeys.RaiderCoins.GetStableHashCode();

        /// <summary>Tags a freshly spawned raider for <paramref name="host"/>'s raid, with its coins. Owner of the raider
        /// only, in the frame it is made.</summary>
        public static void Write(ZDO raider, RaidRunner host, RaiderRole role, bool warlord, int coins) =>
            Write(raider, host.HostId, host.State.StartedAt, role, warlord, coins, host.State.Drops);

        /// <summary>Tags a fresh raider of raid <paramref name="raid"/> at <paramref name="host"/>, and keeps the raid's
        /// loot multiplier now beside it. Owner of the raider only, in the frame it is made.</summary>
        internal static void Write(ZDO raider, ZDOID host, long raid, RaiderRole role, bool warlord, int coins, float drops)
        {
            raider.Set(RaidKeys.RaiderHostPair, host);
            raider.Set(RaidKey, raid);
            raider.Set(RoleKey, (int)role);
            raider.Set(WarlordKey, warlord);
            raider.Set(CoinsKey, coins);
            RaiderPurse.Remember(raider, drops); // for an owner that cannot see the host when it dies
        }

        /// <summary>True for a tagged raider, of any raid.</summary>
        public static bool IsRaider(ZDO? zdo) => zdo != null && zdo.GetInt(RoleKey) != (int)RaiderRole.None;

        /// <summary>The ZDOID of the raid's host; <c>ZDOID.None</c> for a creature that is no raider.</summary>
        public static ZDOID Host(ZDO zdo) => zdo.GetZDOID(RaidKeys.RaiderHostPair);

        /// <summary>The id of the raid it was spawned for (the host's <see cref="RaidState.StartedAt"/> then).</summary>
        public static long Raid(ZDO zdo) => zdo.GetLong(RaidKey);

        public static RaiderRole Role(ZDO zdo) => (RaiderRole)zdo.GetInt(RoleKey);

        /// <summary>Changes the role: a chest raider whose Raiders Chest is gone becomes a plunderer. Raider's owner only.</summary>
        public static void SetRole(ZDO zdo, RaiderRole role) => zdo.Set(RoleKey, (int)role);

        public static bool IsWarlord(ZDO zdo) => zdo.GetBool(WarlordKey);

        /// <summary>The coins it carries, dropped as it dies (<see cref="RaidCoins"/>); -1 once a stop killed it.</summary>
        public static int Coins(ZDO zdo) => zdo.GetInt(CoinsKey);

        /// <summary>Empties its purse: paid out as it died, or taken with it as it walks off. Raider's owner only.</summary>
        public static void ClearCoins(ZDO zdo) => zdo.Set(CoinsKey, 0);

        /// <summary>The building piece it is going for now; <c>ZDOID.None</c> when it has none yet.</summary>
        public static ZDOID Target(ZDO zdo) => zdo.GetZDOID(RaidKeys.RaiderTargetPair);

        /// <summary>Sets the building piece it goes for. Raider's owner only.</summary>
        public static void SetTarget(ZDO zdo, ZDOID target) => zdo.Set(RaidKeys.RaiderTargetPair, target);
    }
}
