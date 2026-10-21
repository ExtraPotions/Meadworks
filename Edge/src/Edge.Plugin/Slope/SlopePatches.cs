// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room (design B1 to B3). Written from the game's own
// members (verified against assembly_valheim.dll metadata by the hooks suite) and Edge.Core's SlopeAssist rules.
using System;
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Integration;
using ExtraPotions.Edge.Slope;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime
{
    /// <summary>
    /// For the local player's melee calls only, <c>Attack.DoMeleeAttack</c> runs with Edge's height, offset and (Adaptive)
    /// pitch limit; the finalizer always puts the attack's own values back, also when the call throws. Every melee attack,
    /// Edge secondaries included, goes through this path (B3).
    /// </summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.DoMeleeAttack))]
    internal static class SlopePatches
    {
        [HarmonyPrefix]
        private static void Prefix(Attack __instance, out SlopeRestore? __state)
        {
            __state = null;
            try
            {
                var settings = Plugin.Settings;
                var attacker = __instance?.m_character;
                if (settings == null || attacker == null || !ReferenceEquals(attacker, Player.m_localPlayer) || !EdgeModules.Active(EdgeModule.Slope)) return;
                var slope = new SlopeSettings(settings.Get(EdgeSettings.Slope.Mode), settings.Get(EdgeSettings.Slope.AttackHeight),
                    settings.Get(EdgeSettings.Slope.AttackOffset), settings.Get(EdgeSettings.Slope.AdaptiveSlopeThreshold), settings.Get(EdgeSettings.Slope.AdaptiveMaxPitch));
                var normal = attacker.m_lastGroundNormal;
                var groundDegrees = normal.sqrMagnitude > 0.0001f ? Vector3.Angle(normal, Vector3.up) : 0f;
                var facts = new SlopeFacts(localPlayer: true, melee: true, groundDegrees, AimedTargetHeight(attacker, __instance!));
                var own = new AttackShape(__instance!.m_attackHeight, __instance.m_attackOffset, __instance.m_maxYAngle);
                var shape = SlopeAssist.Decide(slope, facts, own);
                if (!shape.HasValue) return;
                __state = new SlopeRestore();
                __state.Save(own);
                __instance.m_attackHeight = shape.Value.Height;
                __instance.m_attackOffset = shape.Value.Offset;
                __instance.m_maxYAngle = shape.Value.MaxYAngle;
            }
            catch (Exception)
            {
                __state = null;
            }
        }

        private static float? AimedTargetHeight(Humanoid attacker, Attack attack)
        {
            // The first solid on the look ray blocks targets behind terrain or structures. No upstream slope code.
            var mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain",
                "character", "character_net", "character_ghost");
            var reach = Mathf.Max(0f, attack.m_attackRange + Mathf.Abs(attack.m_attackOffset)) + 1f;
            if (!Physics.Raycast(attacker.GetEyePoint(), attacker.GetLookDir(), out var hit, reach, mask, QueryTriggerInteraction.Ignore)) return null;
            var target = hit.collider.GetComponentInParent<Character>();
            return target != null && target != attacker ? target.GetCenterPoint().y - attacker.GetCenterPoint().y : (float?)null;
        }

        [HarmonyFinalizer]
        private static Exception? Finalizer(Attack __instance, SlopeRestore? __state, Exception? __exception)
        {
            if (__state != null && __state.Pending && __instance != null)
            {
                var own = __state.Take();
                __instance.m_attackHeight = own.Height;
                __instance.m_attackOffset = own.Offset;
                __instance.m_maxYAngle = own.MaxYAngle;
            }
            return __exception;
        }
    }
}
