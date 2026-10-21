// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Mutations;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Relentless's hold on its quarry, run right after the game's own target update on the creature's owner (the game
    /// runs a monster's AI only there). The game has just picked, kept or dropped a target; the hunter adopts that pick
    /// when it holds no quarry, keeps the one it holds, and answers whether the game should treat the quarry as heard.
    /// Any other monster is a single dictionary lookup. These patches sit inside the game's loop over every monster's
    /// AI, so a failure is reported and swallowed rather than rethrown: rethrowing would stop every monster after this
    /// one from thinking that frame.
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), "UpdateTarget")]
    public static class RelentlessTargetPatch
    {
        private static void Postfix(MonsterAI __instance, Humanoid humanoid, ref bool canHearTarget, ref bool canSeeTarget)
        {
            if (!RelentlessHunters.TryGet(__instance, out RelentlessBehaviour hunter))
            {
                return;
            }
            try
            {
                bool able = humanoid != null && !humanoid.m_aiCannotTargetOthers; // the game's own "may target" switch
                canHearTarget = hunter.Pursue(able, canHearTarget, canSeeTarget);
            }
            catch (Exception e)
            {
                Guard.Report(e, "MonsterAI.UpdateTarget relentless");
            }
        }
    }

    /// <summary>
    /// The game re-picks a monster's target every few seconds, taking the nearest enemy it senses - so a closer player,
    /// or one that just hit it, would pull it off its quarry, and a quarry the game had let go of (for a wall in the way,
    /// a fire it fears, a pheromone it flees) would stay lost. While a Relentless creature holds a quarry it can go
    /// after, the pick is that quarry and the game's search is skipped.
    /// </summary>
    [HarmonyPatch(typeof(BaseAI), "FindEnemy")]
    public static class RelentlessRetargetPatch
    {
        private static bool Prefix(BaseAI __instance, ref Character? __result)
        {
            if (!RelentlessHunters.TryGet(__instance, out RelentlessBehaviour hunter))
            {
                return true;
            }
            try
            {
                Character? quarry = hunter.Resume();
                if (quarry == null)
                {
                    return true; // no quarry to go after right now: the game searches as usual
                }
                __result = quarry;
                return false;
            }
            catch (Exception e)
            {
                Guard.Report(e, "BaseAI.FindEnemy relentless");
                return true;
            }
        }
    }

    /// <summary>
    /// A monster with a target but no attack it may use right now - most often one with no attack that works while
    /// swimming, once it is in the water - is left to wander by the game, which is how the water stops a chase. A hunting
    /// Relentless creature keeps closing on its quarry instead; anything else wanders as the game has it.
    /// </summary>
    [HarmonyPatch(typeof(BaseAI), "IdleMovement")]
    public static class RelentlessUnarmedPatch
    {
        private static bool Prefix(BaseAI __instance, float dt)
        {
            if (!RelentlessHunters.TryGet(__instance, out RelentlessBehaviour hunter) || !hunter.Hunting)
            {
                return true;
            }
            try
            {
                return !hunter.ChaseUnarmed(dt);
            }
            catch (Exception e)
            {
                Guard.Report(e, "BaseAI.IdleMovement relentless");
                return true;
            }
        }
    }
}
