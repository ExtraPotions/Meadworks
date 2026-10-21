// OpenKeep. Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Adapted from OpenKeep/OpenKeep/src/Homestead/BedStandPatch.cs at 684671f98de54db6ea6652092ec041f44016c37d.
// Changed by expDARE, 2026: default-off Ember lifecycle, local-owner and skipped-original guards, scoped source coexistence.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Ember.Config;
using ExtraPotions.Ember.Integration;
using HarmonyLib;

namespace ExtraPotions.Ember.Runtime
{
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    internal static class RespawnStanding
    {
        [HarmonyPostfix]
        static void AfterSpawn(Player __instance, bool spawnValkyrie, bool __runOriginal)
        {
            if (!__runOriginal || spawnValkyrie || !Plugin.AllowsUi || !__instance
                || __instance != Player.m_localPlayer || !EmberModules.Active(EmberModule.Sleep)
                || Plugin.Settings?.Get(EmberSettings.Sleep.StandUpOnRespawn) != true
                || !Game.instance || !Game.instance.m_respawnAfterDeath || !SourceAllows()) return;
            var view = __instance.m_nview;
            if (!view || !view.IsValid() || !view.IsOwner() || !__instance.m_animator) return;
            var zdo = view.GetZDO();
            if (zdo == null) return;
            __instance.m_wakeupTimer = -1f;
            __instance.m_animator.SetBool("wakeup", false);
            zdo.Set(ZDOVars.s_wakeup, false);
        }

        static bool SourceAllows()
        {
            if (!Chainloader.PluginInfos.TryGetValue("milkyteam.openkeep", out var source)) return true;
            var plugin = source.Instance as BaseUnityPlugin;
            return plugin != null && plugin.Config.TryGetEntry<bool>(
                new ConfigDefinition("8. Homestead", "Stand Up On Respawn"), out var enabled) && !enabled.Value;
        }
    }
}
