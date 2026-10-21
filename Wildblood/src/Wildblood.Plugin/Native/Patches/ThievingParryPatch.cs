// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Remembers the hit just parried on this machine, for ThievingHitPatch: a parried blow steals nothing. "The hit
    /// dealt no damage" cannot be the test, because the game's block works like armour (HitData.ApplyArmor scales a
    /// hit down but never to zero), so even a parry lets a sliver of damage through. The parry is read where the game
    /// decides it, at the start of Humanoid.BlockAttack: a hit from the front, a blocker with a timed block bonus, and a
    /// block raised less than 0.25 s ago (m_blockTimer, -1 while not blocking). Priority.Last so the timer is read after
    /// any other mod's prefix has widened that window for this one call.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.BlockAttack))]
    public static class ThievingParryPatch
    {
        private const float ParryWindow = 0.25f;

        private static HitData? _parried;

        /// <summary>True when <paramref name="hit"/> is the hit last parried here; matched by reference, so only the
        /// hit in flight ever matches.</summary>
        public static bool Parried(HitData hit) => ReferenceEquals(_parried, hit);

        [HarmonyPriority(Priority.Last)]
        private static void Prefix(Humanoid __instance, HitData hit) =>
            Guard.Run("Humanoid.BlockAttack thieving", static (blocker, blow) => _parried = IsParry(blocker, blow) ? blow : null,
                __instance, hit);

        private static bool IsParry(Humanoid blocker, HitData hit)
        {
            if (hit == null || Vector3.Dot(hit.m_dir, blocker.transform.forward) > 0f)
            {
                return false; // from behind: the game does not block it at all
            }
            ItemDrop.ItemData? shield = blocker.GetCurrentBlocker();
            float timer = blocker.m_blockTimer;
            return shield != null && shield.m_shared.m_timedBlockBonus > 1f && timer >= 0f && timer < ParryWindow;
        }
    }
}
