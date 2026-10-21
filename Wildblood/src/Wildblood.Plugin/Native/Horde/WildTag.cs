// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Horde adoption adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood authority, lifecycle and existing stat/loot seams.
namespace ExtraPotions.Wildblood.Adopted.Horde
{
    /// <summary>
    /// The tag that makes a creature a wild one, in its own ZDO: it came from the world's own spawning (the game's
    /// <c>SpawnSystem</c>, not one of its raids), or split from one that did (Splintering). Written whatever the horde
    /// setting, on the machine that spawns it, in the frame it wakes (<see cref="WildSpawn"/>), so it travels with the
    /// creature to every machine; the horde weakens tagged creatures only while it is on (<see cref="HordeFactors"/>), so
    /// turning it on reaches the wild creatures already out there and turning it off gives them back their strength.
    /// A creature spawned before this version carries no tag and is never weakened.
    /// </summary>
    internal static class WildTag
    {
        public const string Key = "wildblood.wild";

        private static readonly int KeyHash = Key.GetStableHashCode();

        /// <summary>Tags a fresh creature as wild. Its owner only.</summary>
        public static void Mark(ZDO zdo) => zdo.Set(KeyHash, true);

        /// <summary>True for a creature of the world's own spawning.</summary>
        public static bool Is(ZDO? zdo) => zdo != null && zdo.GetBool(KeyHash);
    }
}
