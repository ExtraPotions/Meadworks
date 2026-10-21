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
    /// Opens the Relentless senses for exactly one monster update: everything the game's AI senses in it - noticing a
    /// player, keeping sight of one, turning alert, waking from sleep to a footstep - runs inside this call, so for a
    /// Relentless creature every player counts as standing (<see cref="RelentlessSenses"/>). The finalizer closes it
    /// whatever happens, so nothing outside the update ever sees a player through it. Any other monster is one lookup.
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    public static class RelentlessThinkPatch
    {
        private static void Prefix(MonsterAI __instance) => RelentlessSenses.Enter(__instance);

        private static Exception? Finalizer(Exception? __exception)
        {
            RelentlessSenses.Exit();
            return __exception;
        }
    }

    /// <summary>Sneaking's stealth factor, which shrinks how far a monster sees a player and how near it must come to alert.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetStealthFactor))]
    public static class RelentlessStealthPatch
    {
        private static void Postfix(ref float __result)
        {
            if (RelentlessSenses.Active)
            {
                __result = 1f;
            }
        }
    }

    /// <summary>Crouching, which moves the point a monster needs a clear line to from the eyes down to the body's middle.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.IsCrouching))]
    public static class RelentlessCrouchPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (RelentlessSenses.Active)
            {
                __result = false;
            }
        }
    }

    /// <summary>Footsteps: a player moving crouched makes none, where one walking upright is heard at the game's walking noise.</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.GetNoiseRange))]
    public static class RelentlessNoisePatch
    {
        private static void Postfix(Character __instance, ref float __result)
        {
            if (!RelentlessSenses.Active || !(__instance is Player player))
            {
                return;
            }
            try
            {
                __result = RelentlessSenses.StandingNoise(player, __result);
            }
            catch (Exception e)
            {
                Guard.Report(e, "Character.GetNoiseRange relentless"); // inside the monster loop: never rethrow
            }
        }
    }
}
