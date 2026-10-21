// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Blocking by the local player (Humanoid.BlockAttack runs inside RPC_Damage, on the blocker's own client):
    /// Keen Guard widens the perfect-block window of the shield that blocks (the game calls a block perfect while its
    /// block timer is under 0.25 s, so a timer inside the widened window is presented as 0.249 s for this one call and
    /// restored after); Seidr Riposte restores its resource on a perfect block; Anchored Guard marks the hit so the
    /// stagger and knockback hooks below skip it.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.BlockAttack))]
    internal static class BlockPatch
    {
        private const float PerfectWindow = 0.25f;
        private static bool _perfect;

        private static void Prefix(Humanoid __instance, HitData hit, out float __state)
        {
            __state = __instance.m_blockTimer;
            _perfect = false;
            ItemDrop.ItemData? blocker = ReferenceEquals(__instance, Player.m_localPlayer) ? __instance.GetCurrentBlocker() : null;
            if (blocker == null)
            {
                return;
            }
            ItemLocalSums? sums = ItemLocalCache.Get(blocker);
            if (sums != null && sums.Get(EffectKind.BlockSteadfast) > 0f && Vector3.Dot(hit.m_dir, __instance.transform.forward) <= 0f)
            {
                IncomingHits.Steadfast = true;
            }
            float extra = sums != null ? sums.Get(EffectKind.PerfectBlockWindow) / 1000f : 0f;
            float timer = __instance.m_blockTimer;
            if (extra > 0f && timer >= PerfectWindow && timer < PerfectWindow + extra)
            {
                __instance.m_blockTimer = PerfectWindow - 0.001f;
            }
            _perfect = blocker.m_shared.m_timedBlockBonus > 1f && __instance.m_blockTimer >= 0f && __instance.m_blockTimer < PerfectWindow;
        }

        private static void Postfix(Humanoid __instance, Character attacker, bool __result, float __state)
        {
            if (!ReferenceEquals(__instance, Player.m_localPlayer))
            {
                return;
            }
            __instance.m_blockTimer = __state;
            if (__result && _perfect && attacker != null && __instance.HaveStamina())
            {
                Restore(__instance, AggregateHost.Current.ParryRestore);
            }
            _perfect = false;
        }

        private static void Restore(Humanoid player, float[] amounts)
        {
            if (amounts[AggregateValues.Health] > 0f)
            {
                player.Heal(amounts[AggregateValues.Health]);
            }
            if (amounts[AggregateValues.Stamina] > 0f)
            {
                player.AddStamina(amounts[AggregateValues.Stamina]);
            }
            if (amounts[AggregateValues.Eitr] > 0f)
            {
                player.AddEitr(amounts[AggregateValues.Eitr]);
            }
        }
    }

    /// <summary>
    /// Stagger build-up on the local player: none while an Anchored Guard block resolves or Steel Rhythm's window
    /// runs (the stagger-immunity half of it). Runs on the owner (the only place stagger damage is added).
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.AddStaggerDamage))]
    internal static class StaggerImmunityPatch
    {
        private static bool Prefix(Character __instance, ref bool __result)
        {
            if (ReferenceEquals(__instance, Player.m_localPlayer) && (IncomingHits.Steadfast || CombatWindows.RhythmActive))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Knockback on the local player (Ironroot: X% less; Anchored Guard: none from a blocked hit). Every pushback the
    /// game gives a character ends in this overload, on the character's owner.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyPushback), new[] { typeof(Vector3), typeof(float) })]
    internal static class KnockbackPatch
    {
        private static void Prefix(Character __instance, ref float pushForce)
        {
            if (!ReferenceEquals(__instance, Player.m_localPlayer))
            {
                return;
            }
            pushForce = IncomingHits.Steadfast ? 0f : pushForce * Mathf.Max(0f, 1f - AggregateHost.Current[EffectKind.KnockbackTaken]);
        }
    }
}
