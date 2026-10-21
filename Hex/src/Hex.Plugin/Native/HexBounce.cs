// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexBounce
{
    private static Player? pending;
    private static long generation;

    internal static void Reset() => pending = null;
    private static bool Available(Character player) => HexMovement.On(player, "bouncy")
        && !player.InIntro() && !player.InCutscene() && !player.IsTeleporting() && !player.IsAttached()
        && !player.InLiquidSwimDepth() && !player.IsDebugFlying()
        && player.m_body != null && !player.m_body.isKinematic;

    internal static void Tick()
    {
        var player = pending;
        if (player == null) return;
        if (player != Player.m_localPlayer || generation != CruciblePlugin.Session.Generation
            || !Available(player) || !player.IsOnGround()) { Reset(); return; }
        if (!MeadworksInput.Allows(InputContext.Gameplay)) return;
        // Native Jump retains stamina, tired jumps, skills, attack and encumbrance rules, effects and other patches.
        player.Jump();
        if (!player.IsOnGround()) Reset();
    }

    [HarmonyPatch(typeof(Character), "UpdateGroundContact")]
    private static class Landing
    {
        private static void Prefix(Character __instance, out bool __state)
        {
            __state = Available(__instance) && __instance.m_groundContact && __instance.m_lastGroundTouch >= 0.2f;
        }
        private static void Postfix(Character __instance, bool __state)
        {
            if (!__state || !Available(__instance)) return;
            pending = (Player)__instance;
            generation = CruciblePlugin.Session.Generation;
            Tick();
        }
    }
}
