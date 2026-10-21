// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. AchievementSync is a behavior only reference; no source or assemblies accessed.
using System;
using BepInEx.Bootstrap;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexAchievements
{
    private static readonly Func<bool> forgiven = Forgiven;
    private static Func<bool>? previous;
    private static bool installed;

    internal static bool Available => !Chainloader.PluginInfos.ContainsKey(MeadworksAchievementGuard.AchievementSyncGuid);
    internal static bool Forgiven()
    {
        var player = Player.m_localPlayer;
        return Available && player != null && player.m_nview != null && player.m_nview.IsValid() && player.m_nview.IsOwner()
            && Plugin.Settings?.Effective("achievements_despite_cheats", false) == true;
    }

    internal static void Initialize()
    {
        if (!Available)
        {
            UnityEngine.Debug.LogWarning("Hex achievement features step aside while AchievementSync is installed.");
            return;
        }
        previous = MeadworksAchievementGuard.CheatsForgiven;
        MeadworksAchievementGuard.CheatsForgiven = forgiven;
        installed = true;
    }

    internal static void Shutdown()
    {
        if (installed && ReferenceEquals(MeadworksAchievementGuard.CheatsForgiven, forgiven))
            MeadworksAchievementGuard.CheatsForgiven = previous;
        previous = null;
        installed = false;
    }

    [HarmonyPatch(typeof(Achievements), nameof(Achievements.CanGetAchievements))]
    private static class Admission
    {
        private static bool Prepare() => Available;
        private static void Postfix(ref bool __result)
        {
            if (!__result && Forgiven()) __result = true;
        }
    }
}
