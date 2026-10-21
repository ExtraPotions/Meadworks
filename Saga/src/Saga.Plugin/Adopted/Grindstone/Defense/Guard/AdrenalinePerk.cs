// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Guard/AdrenalinePerk.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The adrenaline bonus. The game adds adrenaline for blocks and parries inside Humanoid.BlockAttack and takes some
    /// away (Player.m_nonBlockDamageAdrenaline) for a hit that was not blocked, inside Character.RPC_Damage; both call
    /// Player.AddAdrenaline on the player's own client. While a hit is reaching the local player
    /// (<see cref="IncomingHit"/>), a gain grows by the bonus and a loss shrinks by it.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.AddAdrenaline))]
    public static class AdrenalinePerk
    {
        [HarmonyPrefix]
        private static void Prefix(Player __instance, ref float v)
        {
            if (!IncomingHit.Active || !DefenseSkill.IsLocal(__instance))
                return;
            float share = DefenseSkill.LocalShare(DefenseGuardSettings.Adrenaline.Value);
            v *= v > 0f ? 1f + share : 1f - Mathf.Clamp01(share);
        }
    }
}
