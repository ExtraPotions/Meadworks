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
    /// Character.ApplySlippery is the step of the game's ground movement where it settles the velocity it is about to
    /// push a body to (the game's own ice uses it); it runs for every character this machine moves, each physics step,
    /// while it is on the ground and not riding a ship. While this machine's player stands on a Frostbound patch, only
    /// the grip's share of that push lands (<see cref="IceFooting.Slide"/>), so they slide. Anyone else, or anyone
    /// while nobody here is on ice, costs one float check. A failure is reported and leaves the game's push as it was.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplySlippery))]
    public static class IceSlipPatch
    {
        private static void Postfix(Character __instance, ref Vector3 currentVel, Vector3 bodyVel)
        {
            if (!IceFooting.Slipping || !ReferenceEquals(__instance, Player.m_localPlayer))
            {
                return;
            }
            try
            {
                IceFooting.Slide(ref currentVel, bodyVel);
            }
            catch (Exception e)
            {
                Guard.Report(e, "Character.ApplySlippery ice");
            }
        }
    }
}
