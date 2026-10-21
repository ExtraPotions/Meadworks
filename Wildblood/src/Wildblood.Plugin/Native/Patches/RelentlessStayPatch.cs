// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Mutations;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// A night creature leaves in daylight: whenever it has no target in sight the game walks it away from the nearest
    /// player and removes it once nobody is within 40 m, and it stops hunting players. While a Relentless creature holds
    /// a quarry the day does not count, so it neither turns away nor vanishes mid-chase; once the quarry is lost, it
    /// leaves as usual. Read on the owner; any creature not set to leave in daylight is a single bool check.
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.DespawnInDay))]
    public static class RelentlessDayPatch
    {
        private static void Postfix(MonsterAI __instance, ref bool __result)
        {
            if (__result && RelentlessHunters.IsHunting(__instance))
            {
                __result = false;
            }
        }
    }

    /// <summary>
    /// A creature that sleeps drops its target and dozes off once no player is within its fall-asleep distance. A
    /// hunting Relentless creature stays awake while its quarry is within chase distance, however far that is.
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), "Sleep")]
    public static class RelentlessSleepPatch
    {
        private static bool Prefix(MonsterAI __instance) => !RelentlessHunters.IsHunting(__instance);
    }

    /// <summary>
    /// A summoned creature is unsummoned the moment it is further than its unsummon distance from the player it follows.
    /// A Relentless summon runs its quarry down first; the check applies again as soon as the hunt ends. Anything that
    /// is not a summon is a single float check.
    /// </summary>
    [HarmonyPatch(typeof(Tameable), "UpdateSummon")]
    public static class RelentlessSummonPatch
    {
        private static bool Prefix(Tameable __instance)
        {
            return __instance.m_unsummonDistance <= 0f || __instance.m_monsterAI == null
                || !RelentlessHunters.IsHunting(__instance.m_monsterAI);
        }
    }
}
