// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room: adapts the tested shoreline state machine to vanilla game members.
// No KeepWeaponsAfterSwim binary or Slope Combat Assistance source was opened.
using System;
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Equipment;
using ExtraPotions.Edge.Integration;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Equipment;

internal static class EdgeSwimReequip
{
    private static readonly SwimReequip state = new(SwimMode.Off, 0.5f);
    private static Player? owner;
    private static int equipmentDepth;
    internal static bool Restoring => state.Restoring;
    internal static bool Local(Humanoid player) => player != null && player == Player.m_localPlayer;

    internal static void Tick()
    {
        var player = Player.m_localPlayer;
        if (owner != player) { Reset(); owner = player; }
        var settings = Plugin.Settings;
        if (player == null || settings == null || !EdgeModules.Active(EdgeModule.SwimReequip) || player.IsDead() || player.IsTeleporting())
        { state.Reset(); return; }
        state.Configure(settings.Get(EdgeSettings.Equipment.SwimMode), settings.Get(EdgeSettings.Equipment.RestoreDelay));
        if (state.Update(Time.time, player.IsSwimming(), player.m_hiddenLeftItem != null || player.m_hiddenRightItem != null) != SwimAction.Restore) return;
        state.Restoring = true;
        try { player.ShowHandItems(false); }
        finally { state.Restoring = false; }
    }

    internal static bool Hide(Humanoid player)
    {
        if (!Local(player) || !EdgeModules.Active(EdgeModule.SwimReequip)) return true;
        // Vanilla UpdateEquipment is the swim caller. Input, crafting and attachment hides keep their own intent.
        if (equipmentDepth == 0 || !player.IsSwimming()) { state.PlayerShowed(); return true; }
        var settings = Plugin.Settings!;
        state.Configure(settings.Get(EdgeSettings.Equipment.SwimMode), settings.Get(EdgeSettings.Equipment.RestoreDelay));
        return state.AllowHide(true, player.m_leftItem != null || player.m_rightItem != null);
    }

    internal static void Shown(Humanoid player) { if (Local(player) && !Restoring) state.PlayerShowed(); }
    internal static void EnterEquipment() => equipmentDepth++;
    internal static void ExitEquipment() => equipmentDepth = Math.Max(0, equipmentDepth - 1);
    internal static void Reset() { state.Reset(); owner = null; equipmentDepth = 0; }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UpdateEquipment), typeof(float))]
internal static class SwimEquipmentScopePatch
{
    [HarmonyPrefix] private static void Prefix(Humanoid __instance, out bool __state)
    { __state = EdgeSwimReequip.Local(__instance); if (__state) EdgeSwimReequip.EnterEquipment(); }
    [HarmonyFinalizer] private static Exception? Finalizer(bool __state, Exception? __exception)
    { if (__state) EdgeSwimReequip.ExitEquipment(); return __exception; }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.HideHandItems), typeof(bool), typeof(bool))]
internal static class SwimHidePatch
{
    [HarmonyPrefix] private static bool Prefix(Humanoid __instance, ref bool __result)
    { if (EdgeSwimReequip.Hide(__instance)) return true; __result = false; return false; }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.ShowHandItems), typeof(bool), typeof(bool))]
internal static class SwimShowPatch
{
    [HarmonyPostfix] private static void Postfix(Humanoid __instance) => EdgeSwimReequip.Shown(__instance);
}
