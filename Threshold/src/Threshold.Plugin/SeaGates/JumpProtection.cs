// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/JumpProtection.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>After landing, the local player takes no damage of any kind for a few seconds. Every hit reaches a
    /// character through the owner's <c>Character.RPC_Damage</c> (verified in the decompiled assembly):
    /// <c>Character.Damage</c> sends it there, and fall damage (<c>UpdateGroundContact</c>, a drop over 4 m), drowning
    /// (<c>Player.OnSwimming</c> with no stamina), the wet and cold ticks (<c>SE_Wet</c>, <c>SE_Stats</c>) all call
    /// <c>Damage</c>. Burning, poison and smoke ticks call <c>Character.ApplyDamage</c> directly, which
    /// <c>RPC_Damage</c> also ends in. Both are stopped for the local player, which owns its own character; skipping
    /// <c>RPC_Damage</c> whole also drops the hit's push, stagger and status effect. Not gated on the settings: it only
    /// starts at the end of a jump, and finishing a jump safely comes first.</summary>
    public static class JumpProtection
    {
        private static float until;

        public static bool Active => Time.time < until;

        public static void Start(float seconds)
        {
            until = Mathf.Max(until, Time.time + Mathf.Max(0f, seconds));
        }

        public static void Clear() => until = 0f;

        internal static bool Shields(Character character) =>
            Active && character != null && character == Player.m_localPlayer;
    }

    [HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
    public static class JumpProtectionHitPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Character __instance) => !JumpProtection.Shields(__instance);
    }

    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    public static class JumpProtectionDamagePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Character __instance) => !JumpProtection.Shields(__instance);
    }
}
