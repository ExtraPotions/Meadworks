// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Recap;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Every hit that lands on this machine's own player goes into the death recap's hit log. <c>ApplyDamage</c> is where
    /// a hit lands (a creature's swing, an arrow, a burning or poison tick, a fall, drowning, the game's other causes and
    /// the mod's own storms): the prefix notes the health before, the postfix records the hit when health was lost.
    /// Anything else is one reference check. Inside the game's hit, so a failure is reported and swallowed.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    public static class RecapHitPatch
    {
        private static void Prefix(Character __instance, out float __state) =>
            __state = ExtraPotions.Wildblood.Runtime.WildbloodRecap.Recording && __instance == Player.m_localPlayer && !__instance.IsDead() ? __instance.GetHealth() : -1f;

        private static void Postfix(Character __instance, HitData hit, float __state)
        {
            if (ExtraPotions.Wildblood.Runtime.WildbloodRecap.Recording && __state > 0f && __instance.GetHealth() < __state)
            {
                SafeCall.Run("Character.ApplyDamage death recap", static (player, blow) => HitLog.Record((Player)player, blow),
                    __instance, hit);
            }
        }
    }

    /// <summary>
    /// The creature behind later burning and poison ticks: <c>RPC_Damage</c> takes a hit's fire, spirit and poison off
    /// before it lands and turns them into ticks with no attacker, so before the hit (a step of <see cref="HitPatch"/>)
    /// this notes who carried them.
    /// </summary>
    public static class RecapSourcePatch
    {
        internal static void Note(Character victim, HitData hit)
        {
            if (ExtraPotions.Wildblood.Runtime.WildbloodRecap.Recording && hit != null && victim == Player.m_localPlayer)
            {
                SafeCall.Run("Character.RPC_Damage death recap", static blow => HitLog.NoteSource(blow), hit);
            }
        }
    }
}
