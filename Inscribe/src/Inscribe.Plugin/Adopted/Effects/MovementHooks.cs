// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Pathfinder's ground check: whether the local player stands on terrain painted as a dirt path or paved road (the
    /// hoe's paint: red and blue channels of the heightmap's paint mask; cultivated green does not count). Building
    /// floors are not roads. Sampled four times a second, only while the channel is non-zero; the aggregate's speed
    /// override reads the cached flag. Local player only.
    /// </summary>
    internal static class PathGround
    {
        private const float Interval = 0.25f;
        private static float _next;

        public static bool OnPath { get; private set; }

        public static void Tick(Player player)
        {
            if (AggregateHost.Current[EffectKind.MoveSpeedPaved] <= 0f)
            {
                OnPath = false;
                return;
            }
            if (Time.time < _next)
            {
                return;
            }
            _next = Time.time + Interval;
            OnPath = Sample(player);
        }

        private static bool Sample(Player player)
        {
            Collider? ground = player.IsOnGround() ? player.GetLastGroundCollider() : null;
            Heightmap? map = ground != null ? ground.GetComponent<Heightmap>() : null;
            if (map == null)
            {
                return false;
            }
            Vector3 at = player.transform.position;
            at.x -= 0.5f;
            at.z -= 0.5f;
            Color paint = map.GetPaintMask(at);
            return paint.r > 0.5f || paint.b > 0.5f;
        }
    }

    /// <summary>
    /// Momentum: a dodge roll starts the 5 s window. Player.Dodge only queues the press; Player.UpdateDodge starts the
    /// roll when the stamina is there and marks it by zeroing the queue timer, so the window follows real rolls only (a
    /// press on an empty stamina bar gives nothing). Runs on the dodging player's own client.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.UpdateDodge))]
    internal static class DodgePatch
    {
        private static void Prefix(Player __instance, out float __state) => __state = __instance.m_queuedDodgeTimer;

        private static void Postfix(Player __instance, float __state)
        {
            if (__state > 0f && __instance.m_queuedDodgeTimer == 0f && ReferenceEquals(__instance, Player.m_localPlayer))
            {
                CombatWindows.OnDodge();
            }
        }
    }

    /// <summary>
    /// Mountain Goat: on a slope steeper than the slide angle the game blends the player's velocity toward a slide
    /// down the slope; X% of that blend is taken back, so steep slopes slow you X% less (cap 75: some slide stays).
    /// Local player only (movement runs on the owner).
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplySlide))]
    internal static class SlopePatch
    {
        private static void Prefix(ref Vector3 currentVel, out Vector3 __state) => __state = currentVel;

        private static void Postfix(Character __instance, ref Vector3 currentVel, Vector3 __state)
        {
            if (!ReferenceEquals(__instance, Player.m_localPlayer))
            {
                return;
            }
            float keep = AggregateHost.Current[EffectKind.SlopePenalty];
            if (keep > 0f && __instance.m_slippage > 0f)
            {
                currentVel = Vector3.Lerp(currentVel, __state, keep);
            }
        }
    }

    /// <summary>
    /// Marshstrider, water and tar: the game's wading slowdown (Character.ApplyLiquidResistance) is X% smaller. The
    /// Tared status's own slow is handled in <see cref="StatusEffectTweaks"/>. Local player only.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyLiquidResistance))]
    internal static class LiquidSlowPatch
    {
        private static void Prefix(ref float speed, out float __state) => __state = speed;

        private static void Postfix(Character __instance, ref float speed, float __state)
        {
            if (speed < __state && ReferenceEquals(__instance, Player.m_localPlayer))
            {
                speed = __state - (__state - speed) * (1f - AggregateHost.Current[EffectKind.TerrainSlow]);
            }
        }
    }

    /// <summary>
    /// Coldblood: the frost status's slow on the local player is X% smaller (100% = none). SE_Frost.ModifySpeed runs in
    /// the owner's movement update; the prefix remembers the speed, the postfix scales what frost took.
    /// </summary>
    [HarmonyPatch(typeof(SE_Frost), nameof(SE_Frost.ModifySpeed))]
    internal static class FrostSlowPatch
    {
        private static void Prefix(ref float speed, out float __state) => __state = speed;

        private static void Postfix(Character character, ref float speed, float __state)
        {
            if (speed < __state && ReferenceEquals(character, Player.m_localPlayer))
            {
                speed = __state - (__state - speed) * Mathf.Max(0f, 1f - AggregateHost.Current[EffectKind.FrostSlowTaken]);
            }
        }
    }

    /// <summary>
    /// Ashen Skin: heat builds up X% slower. The game subtracts this equipment modifier from its heat gain
    /// (Character.UpdateHeat...), so it is added to the local player's own modifier. Local player only.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetEquipmentHeatResistanceModifier))]
    internal static class HeatResistPatch
    {
        private static void Postfix(Player __instance, ref float __result)
        {
            if (ReferenceEquals(__instance, Player.m_localPlayer))
            {
                __result += AggregateHost.Current[EffectKind.HeatResist];
            }
        }
    }
}
