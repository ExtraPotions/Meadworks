// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using HarmonyLib;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexKnockback
{
    private struct PushState
    {
        internal HitData? Hit;
        internal OwnedScale Force;
    }

    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    private static class OutgoingCreatureHit
    {
        private static void Prefix(Character __instance, HitData hit, out PushState __state)
        {
            __state = default;
            if (__instance == null || __instance.IsPlayer() || __instance.IsDead()
                || __instance.m_nview == null || !__instance.m_nview.IsValid() || hit == null
                || !HexMovement.On(hit.GetAttacker(), "super_knockback")
                || hit.m_pushForce <= 0f || float.IsNaN(hit.m_pushForce) || float.IsInfinity(hit.m_pushForce)) return;
            __state = new PushState { Hit = hit, Force = new OwnedScale(hit.m_pushForce, 10f) };
            if (float.IsInfinity(__state.Force.Applied)) { __state = default; return; }
            hit.m_pushForce = __state.Force.Applied;
        }
        // Native Damage serializes HitData to its owner's routed-RPC package before returning.
        // Restore the caller's object even on exceptions; a later force writer keeps its change.
        private static void Finalizer(PushState __state)
        {
            if (__state.Hit != null) __state.Hit.m_pushForce = __state.Force.Restore(__state.Hit.m_pushForce);
        }
    }
}
