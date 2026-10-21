// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Ember.Config;
using ExtraPotions.Ember.Integration;
using ExtraPotions.Ember.Torches;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Ember.Runtime
{
    public static class FireComfortRuntime
    {
        static readonly Dictionary<ParticleSystem, bool> originalEmission = new Dictionary<ParticleSystem, bool>();
        static bool Smokeless => Plugin.Settings != null && EmberModules.Active(EmberModule.Torches)
            && Plugin.Settings.Get(EmberSettings.Torches.SmokelessFires);

        internal static bool Target(Fireplace fire)
        {
            if (!fire || fire.gameObject.layer == Piece.s_ghostLayer || !FireComfort.Placed(Utils.GetPrefabName(fire.gameObject))) return false;
            var view = fire.GetComponent<ZNetView>();
            return view && view.IsValid();
        }

        static bool Target(SmokeSpawner smoke)
        {
            var fire = smoke.GetComponentInParent<Fireplace>();
            if (fire) return Target(fire);
            return HeldTorch(smoke);
        }

        static bool HeldTorch(Component effect)
        {
            var vis = effect.GetComponentInParent<VisEquipment>();
            if (!vis) return false;
            int torch = "Torch".GetStableHashCode();
            return vis.m_rightItem == torch && vis.m_rightItemInstance && effect.transform.IsChildOf(vis.m_rightItemInstance.transform)
                || vis.m_leftItem == torch && vis.m_leftItemInstance && effect.transform.IsChildOf(vis.m_leftItemInstance.transform);
        }

        static bool CarriedSmoke(ParticleSystem system) => system && system.name == "Smoke"
            && system.transform.parent && system.transform.parent.name == "fx_Torch_Carried";

        static void SuppressParticles(GameObject root)
        {
            foreach (var system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!CarriedSmoke(system)) continue;
                var emission = system.emission;
                if (!originalEmission.ContainsKey(system)) originalEmission.Add(system, emission.enabled);
                emission.enabled = false;
                // Clear this instance's smoke only; its flame and ember siblings keep running.
                system.Clear(false);
            }
        }

        static void RestoreParticles(ParticleSystem system)
        {
            if (system)
            {
                var emission = system.emission;
                if (!emission.enabled) emission.enabled = originalEmission[system];
            }
            originalEmission.Remove(system);
        }

        public static void RefreshHeldSmoke()
        {
            bool suppress = Smokeless;
            foreach (var system in originalEmission.Keys.ToArray())
                if (!system || !suppress || !HeldTorch(system)) RestoreParticles(system);
            if (!suppress) return;
            int torch = "Torch".GetStableHashCode();
            foreach (var player in Player.GetAllPlayers())
            {
                if (!player) continue;
                var vis = player.GetComponent<VisEquipment>();
                if (!vis) continue;
                if (vis.m_rightItem == torch && vis.m_rightItemInstance) SuppressParticles(vis.m_rightItemInstance);
                if (vis.m_leftItem == torch && vis.m_leftItemInstance) SuppressParticles(vis.m_leftItemInstance);
            }
        }

        internal static void AttachedItem(int itemHash, bool enableEquipEffects, bool backAttach, GameObject instance)
        {
            if (instance && itemHash == "Torch".GetStableHashCode() && enableEquipEffects && !backAttach && Smokeless)
                SuppressParticles(instance);
        }

        public static void Shutdown()
        {
            foreach (var system in originalEmission.Keys.ToArray()) RestoreParticles(system);
        }

        public static bool SuppressSpawn(SmokeSpawner smoke, float time)
        {
            if (!smoke || !Smokeless || !Target(smoke)) return false;
            var player = Player.m_localPlayer;
            // Keep native blockage timing without creating a smoke cloud; disabling the spawner would extinguish the fire.
            if (!player || Vector3.Distance(player.transform.position, smoke.transform.position) > 64f || !smoke.TestBlocked())
                smoke.m_lastSpawnTime = time;
            return true;
        }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.AttachItem))]
    internal static class HeldTorchSmokePatch
    {
        [HarmonyPostfix]
        static void Postfix(int itemHash, bool enableEquipEffects, bool backAttach, GameObject __result)
            => FireComfortRuntime.AttachedItem(itemHash, enableEquipEffects, backAttach, __result);
    }

    [HarmonyPatch(typeof(SmokeSpawner), nameof(SmokeSpawner.Spawn))]
    internal static class FireSmokePatch
    {
        [HarmonyPrefix]
        static bool Prefix(SmokeSpawner __instance, float time) => !FireComfortRuntime.SuppressSpawn(__instance, time);
    }
}
