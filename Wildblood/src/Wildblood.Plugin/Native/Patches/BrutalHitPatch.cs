// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Aspects;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// A Brutal throw, decided where a hit on a player is applied: <c>Character.RPC_Damage</c> on the player's own client,
    /// which owns their body; two steps of <see cref="HitPatch"/>. Before the hit it notes whether the hit is a marked one
    /// aimed at this machine's own player and not rolled through - read first, because the game drops a dodged hit
    /// without applying it and the steps after the hit still run after that early return. After the hit it throws the
    /// player (<see cref="BrutalHit.Judge"/>) once the game has dealt the damage and decided any block. Any unmarked hit
    /// is one float check. It runs inside the game's hit, so a failure is reported and swallowed, and the hit stays an
    /// ordinary one.
    /// </summary>
    public static class BrutalHitPatch
    {
        internal static bool Marked(Character victim, HitData hit) =>
            hit != null && BrutalBlow.Carries(hit) && BrutalHit.Aimed(victim, hit);

        internal static void Throw(Character victim, HitData hit, bool marked)
        {
            if (marked)
            {
                SafeCall.Run("Character.RPC_Damage brutal", static (player, blow) => BrutalHit.Judge(player, blow), victim, hit);
            }
        }
    }

    /// <summary>
    /// A block that held against a marked hit. Inside <c>Humanoid.BlockAttack</c> the game takes the blocked damage off
    /// the hit (<c>HitData.BlockDamage</c>) exactly when the block holds - stamina left, the guard not broken, a parry
    /// included - so that call is the signal (<see cref="BrutalHit.Withstood"/>). Any unmarked hit is one float check; a
    /// failure is reported and swallowed, and the block counts as broken.
    /// </summary>
    [HarmonyPatch(typeof(HitData), nameof(HitData.BlockDamage))]
    public static class BrutalBlockPatch
    {
        private static void Postfix(HitData __instance)
        {
            if (BrutalBlow.Carries(__instance))
            {
                SafeCall.Run("HitData.BlockDamage brutal", static blow => BrutalHit.Withstood(blow), __instance);
            }
        }
    }
}
