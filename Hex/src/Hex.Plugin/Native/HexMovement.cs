// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions from aedenthorn/ValheimMods 83690e8ef9a37c4694604416667d252b4ff5f645 (public domain):
// Regeneration/Regeneration.cs UseStamina_Patch; JumpRunDodgeSneakWalk/BepInExPlugin.cs jog/run factors,
// jump ground-contact allowance, jump/swim field multipliers and fall-altitude adjustment.
// Changed by expDARE: actual local owner/C13 gates, personal settings, scoped conditional restoration, no sneak
// or global Awake mutations, original stamina-availability hooks and per-owned-body moon gravity compensation.
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexMovement
{
    internal static bool Owned(Character actor) => actor != null && actor == Player.m_localPlayer && !actor.IsDead()
        && actor.m_nview != null && actor.m_nview.IsValid() && actor.m_nview.IsOwner();
    internal static bool On(Character actor, string key) => Owned(actor) && Plugin.Settings?.Effective(key, false) == true;
    internal static float Factor(Character actor, string key) => Owned(actor) && Plugin.Settings != null
        ? key == "jump_power" ? HexCatalog.JumpFactor(Plugin.Settings.Effective(key, 100))
            : HexCatalog.SpeedFactor(Plugin.Settings.Effective(key, 100)) : 1f;
    internal static void ApplyGravity()
    {
        var player = Player.m_localPlayer;
        if (!On(player, "moon_gravity") || player.IsTeleporting() || player.IsAttached()) return;
        var body = player.m_body;
        if (body != null && !body.isKinematic && body.useGravity)
            body.AddForce(-Physics.gravity * 0.8f, ForceMode.Acceleration);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.UseStamina))]
    private static class UseStamina
    {
        private static void Prefix(Player __instance, ref float v) { if (On(__instance, "unlimited_stamina")) v *= 0f; }
    }
    [HarmonyPatch(typeof(Player), nameof(Player.HaveStamina))]
    private static class HaveStamina
    {
        private static void Postfix(Player __instance, ref bool __result) { if (On(__instance, "unlimited_stamina")) __result = true; }
    }
    [HarmonyPatch(typeof(Player), nameof(Player.GetStamina))]
    private static class GetStamina
    {
        private static void Postfix(Player __instance, ref float __result) { if (On(__instance, "unlimited_stamina")) __result = __instance.GetMaxStamina(); }
    }
    // Player overrides both factors without calling base, so a patch on the Character method never reaches the local player.
    [HarmonyPatch(typeof(Player), "GetJogSpeedFactor")]
    private static class Walk
    {
        private static void Postfix(Character __instance, ref float __result) { __result *= Factor(__instance, "walk_speed"); }
    }
    [HarmonyPatch(typeof(Player), "GetRunSpeedFactor")]
    private static class Run
    {
        private static void Postfix(Character __instance, ref float __result) { __result *= Factor(__instance, "run_speed"); }
    }
    [HarmonyPatch(typeof(Character), "UpdateSwimming")]
    private static class Swim
    {
        private static void Prefix(Character __instance, out OwnedScale __state)
        {
            __state = new OwnedScale(__instance.m_swimSpeed, Factor(__instance, "swim_speed"));
            __instance.m_swimSpeed = __state.Applied;
        }
        private static void Finalizer(Character __instance, OwnedScale __state) { __instance.m_swimSpeed = __state.Restore(__instance.m_swimSpeed); }
    }
    private struct JumpState { public OwnedScale Force; public float Ground, Altitude; public bool AirJump; }
    [HarmonyPatch(typeof(Character), nameof(Character.Jump))]
    private static class Jump
    {
        private static void Prefix(Character __instance, out JumpState __state)
        {
            __state = new JumpState { Force = new OwnedScale(__instance.m_jumpForce, Factor(__instance, "jump_power")),
                Ground = __instance.m_lastGroundTouch, Altitude = __instance.m_maxAirAltitude };
            __instance.m_jumpForce = __state.Force.Applied;
            if (On(__instance, "infinite_jumps") && !__instance.IsOnGround() && !__instance.IsAttached())
            {
                __state.AirJump = true;
                __instance.m_lastGroundTouch = 0.1f;
                __instance.m_maxAirAltitude = __instance.transform.position.y;
            }
        }
        private static void Finalizer(Character __instance, JumpState __state)
        {
            __instance.m_jumpForce = __state.Force.Restore(__instance.m_jumpForce);
            if (__state.AirJump && __instance.m_lastGroundTouch == 0.1f)
            {
                __instance.m_lastGroundTouch = __state.Ground;
                __instance.m_maxAirAltitude = __state.Altitude;
            }
        }
    }
    [HarmonyPatch(typeof(Character), "UpdateGroundContact")]
    private static class NoMoonFallDamage
    {
        private static void Prefix(Character __instance)
        {
            if (On(__instance, "moon_gravity"))
                __instance.m_maxAirAltitude = __instance.transform.position.y + (__instance.m_maxAirAltitude - __instance.transform.position.y) * 0f;
        }
    }
}
