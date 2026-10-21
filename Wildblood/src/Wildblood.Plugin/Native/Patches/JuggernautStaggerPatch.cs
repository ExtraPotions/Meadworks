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
    /// Where every stagger lands. Character.Stagger runs RPC_Stagger at once when it is asked on the creature's owner
    /// and sends it to the owner from anywhere else, so a player's parry (decided on the blocking player's own client),
    /// a hit that staggers outright, a trap, the stagger meter and any other mod's call all end here, on the one machine
    /// whose animator every other machine follows. A <see cref="Juggernaut"/> skips it and shows its tell instead. It
    /// runs inside a hit, so a failure is reported and the game's own stagger goes ahead rather than the hit breaking.
    /// </summary>
    [HarmonyPatch(typeof(Character), "RPC_Stagger")]
    public static class JuggernautStaggerPatch
    {
        private static bool Prefix(Character __instance)
        {
            try
            {
                if (!Juggernaut.Holds(__instance))
                {
                    return true;
                }
                Juggernaut.Shrug(__instance);
                return false;
            }
            catch (Exception e)
            {
                Guard.Report(e, "Character.RPC_Stagger juggernaut");
                return true;
            }
        }
    }

    /// <summary>
    /// The stagger meter, filled on the owner by every hit that lands (ApplyDamage) and by a hit a blocking creature
    /// takes on its guard (Humanoid.BlockAttack). At the top the game staggers the creature, breaks its guard and pays
    /// the attacking player stagger adrenaline; a <see cref="Juggernaut"/> runs <see cref="Juggernaut.Absorb"/> instead
    /// and answers "no stagger", so none of that follows and its guard holds. RPC_Stagger alone would stop the stumble
    /// but leave the meter pinned at the top, paying adrenaline on every later hit. A failure is reported and the game's
    /// own meter runs.
    /// </summary>
    [HarmonyPatch(typeof(Character), "AddStaggerDamage")]
    public static class JuggernautStaggerDamagePatch
    {
        private static bool Prefix(Character __instance, float damage, HitData hit, ref bool __result)
        {
            try
            {
                if (!Juggernaut.Holds(__instance))
                {
                    return true;
                }
                Juggernaut.Absorb(__instance, damage, hit);
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                Guard.Report(e, "Character.AddStaggerDamage juggernaut");
                return true;
            }
        }
    }
}
