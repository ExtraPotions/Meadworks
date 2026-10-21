// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Mutations;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Every knockback a character takes goes through Character.ApplyPushback: the push of each hit that lands (RPC_Damage
    /// hands it the hit's force on the owner - weapons, arrows, area blasts, a Bloated blast, the deflection a player's
    /// block or parry sends back into the attacker), an area's extra knockback, and Warding's shove on a melee attacker.
    /// A <see cref="Juggernaut"/> takes none of them. Its own attacks keep their recoil (<see cref="Juggernaut.OwnRecoil"/>),
    /// so it moves through a fight as its kind does. Only the owner moves a character, but the check holds wherever it is
    /// asked. It runs inside a hit, so a failure is reported and the game's own push goes ahead.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyPushback), new[] { typeof(Vector3), typeof(float) })]
    public static class JuggernautPushbackPatch
    {
        private static bool Prefix(Character __instance, Vector3 dir, float pushForce)
        {
            if (pushForce == 0f)
            {
                return true; // nothing to push: the game does nothing either
            }
            try
            {
                return !Juggernaut.Holds(__instance) || Juggernaut.OwnRecoil(__instance, dir, pushForce);
            }
            catch (Exception e)
            {
                Guard.Report(e, "Character.ApplyPushback juggernaut");
                return true;
            }
        }
    }
}
