// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using ExtraPotions.Bestiary.Config;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using ExtraPotions.Bestiary.Spawning;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Adapters.Kit
{
    /// <summary>Wild shares, the Ashlands region entry, and the Deep North spawner roll. Levels are 1 to 1.</summary>
    internal static class KitSpawns
    {
        private static SpawnSystemList? list;
        private static readonly MethodInfo SpawnOne = AccessTools.Method(typeof(SpawnSystem), "Spawn", new[] { typeof(SpawnSystem.SpawnData), typeof(Vector3), typeof(bool) });
        private static int pending = -1;
        private static long pendingSince;
        private static int pass = -1;
        private static SpawnSystem? pendingSystem;
        private static readonly SpawnSystem.SpawnData Entry = new SpawnSystem.SpawnData
        {
            m_name = KitCreatures.SlagCharred,
            m_biomeArea = Heightmap.BiomeArea.Median,
            m_maxSpawned = 1,
            m_spawnDistance = SlagRegions.Apart,
            m_groupSizeMin = 1,
            m_groupSizeMax = 1,
            m_huntPlayer = true,
            m_groundOffset = 0.5f,
            m_minLevel = KitCreatures.SpawnLevel,
            m_maxLevel = KitCreatures.SpawnLevel,
        };

        public static void Join(SpawnSystem system)
        {
            if (Entry.m_prefab == null) return;
            list = list != null ? list : Make();
            if (system.m_spawnLists.Contains(list)) return;
            system.m_spawnLists.Add(list);
            NativeRuntime.SpawnList(system, list);
        }

        public static bool Allow(SpawnSystem system, Vector3 point)
        {
            if (pass >= 0) return SectorRegion(point) == pass;
            int region = Waiting(point);
            if (region < 0 || ZoneSystem.instance == null) return false;
            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                ZoneSystem.instance.SetGlobalKey(SlagRegions.ClaimKey(region));
                Log.Info("Slag Charred: region " + region + " is claimed.");
                return true;
            }
            if (ZRoutedRpc.instance == null) return false;
            if (pending >= 0)
            {
                // shortcut: 10 s, lengthen if a slow server drops a grant that the client then asks for again
                long now = ZNet.instance != null ? ZNet.instance.GetTime().Ticks : 0L;
                if (pendingSince == 0L || now < pendingSince + 10L * TimeSpan.TicksPerSecond) return false;
                pending = -1;
                pendingSystem = null;
            }
            pending = region;
            pendingSince = ZNet.instance != null ? ZNet.instance.GetTime().Ticks : 0L;
            pendingSystem = system;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), BestiaryKeys.SlagClaimAsk, region, point);
            return false;
        }

        public static void Asked(long sender, int region, Vector3 point)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null || ZRoutedRpc.instance == null) return;
            bool take = SlagClaim.Take(Waiting(point) == region, SenderNear(sender, point));
            if (take)
            {
                ZoneSystem.instance.SetGlobalKey(SlagRegions.ClaimKey(region));
                Log.Info("Slag Charred: region " + region + " is claimed.");
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, BestiaryKeys.SlagClaimAnswer, take, region, point);
        }

        public static void Answer(long sender, bool granted, int region, Vector3 point)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer() || ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID()) return;
            if (region != pending) return;
            var system = pendingSystem;
            pending = -1;
            pendingSystem = null;
            if (!granted || system == null || SpawnOne == null) return;
            pass = region;
            try { SpawnOne.Invoke(system, new object[] { Entry, point, false }); }
            catch (Exception error) { Log.Warn("Slag Charred spawn after the claim failed: " + error.GetType().Name + ": " + error.Message); }
            finally { pass = -1; }
        }

        public static void Refresh()
        {
            Entry.m_prefab = KitPrefabs.Slag;
            Entry.m_enabled = KitSettings.SlagOn;
            Entry.m_spawnChance = KitSettings.SlagChance;
            Entry.m_spawnInterval = KitSettings.SlagInterval;
            Entry.m_biome = KitSettings.SlagBiomes;
            Entry.m_minLevel = KitCreatures.SpawnLevel;
            Entry.m_maxLevel = KitCreatures.SpawnLevel;
            float health = KitSettings.SlagHealth;
            var prefab = KitPrefabs.Slag;
            var body = prefab != null ? prefab.GetComponent<Character>() : null;
            if (body != null) body.m_health = health;
            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || character.m_nview == null || !character.m_nview.IsValid() || !character.m_nview.IsOwner()) continue;
                if (!character.name.StartsWith(KitCreatures.SlagCharred, StringComparison.Ordinal)) continue;
                character.m_health = health;
                if (character.GetHealth() > health) character.SetHealth(health);
            }
        }

        public static SpawnSystem.SpawnData? Wild(SpawnSystem.SpawnData critter, Vector3 point, bool eventSpawner)
        {
            if (critter?.m_prefab == null || WorldGenerator.instance == null) return null;
            string name = critter.m_prefab.name;
            var here = (BiomeMask)(int)WorldGenerator.instance.GetBiome(point);
            double roll = UnityEngine.Random.value;
            GameObject? swap = null;
            if (KitPrefabs.BogHook != null && WildShare.Replace(KitSettings.BogOn, KitCreatures.Draugr, name, false, eventSpawner,
                (BiomeMask)(int)KitSettings.BogBiomes, here, KitSettings.BogShare, roll))
                swap = KitPrefabs.BogHook;
            else if (KitPrefabs.Javelineer != null && WildShare.Replace(KitSettings.SpearOn, KitCreatures.Fuling, name, false, eventSpawner,
                (BiomeMask)(int)KitSettings.SpearBiomes, here, KitSettings.SpearShare, roll))
                swap = KitPrefabs.Javelineer;
            if (swap == null) return null;
            var copy = critter.Clone();
            copy.m_prefab = swap;
            copy.m_minLevel = KitCreatures.SpawnLevel;
            copy.m_maxLevel = KitCreatures.SpawnLevel;
            return copy;
        }

        public static bool Marrow(CreatureSpawner spawner)
        {
            var prefab = spawner.m_creaturePrefab;
            if (prefab == null || KitPrefabs.Marrow == null || WorldGenerator.instance == null) return false;
            int x = Mathf.FloorToInt(spawner.transform.position.x);
            int z = Mathf.FloorToInt(spawner.transform.position.z);
            bool holds = MarrowPlaces.PlaceHolds(WorldGenerator.instance.GetSeed(), x, z, KitSettings.MarrowPlaces);
            return MarrowPlaces.ThisSpawn(KitSettings.MarrowOn, prefab.name, holds, KitSettings.MarrowChance, UnityEngine.Random.value);
        }

        private static int SectorRegion(Vector3 point)
        {
            var data = WorldGenerator.instance?.m_world?.m_biomeData;
            if (data == null || !data.IsReady || WorldGenerator.instance == null) return -1;
            return data.Sectors.IndexOf(WorldGenerator.instance.GetBiomeSector(point));
        }

        private static int Waiting(Vector3 point)
        {
            if (ZoneSystem.instance == null || WorldGenerator.instance == null) return -1;
            var data = WorldGenerator.instance.m_world?.m_biomeData;
            if (data == null || !data.IsReady) return -1;
            var sector = WorldGenerator.instance.GetBiomeSector(point);
            int region = data.Sectors.IndexOf(sector);
            bool holds = SlagRegions.Holds(WorldGenerator.instance.GetSeed(), region, KitSettings.SlagRegions);
            bool claimed = region >= 0 && ZoneSystem.instance.GetGlobalKey(SlagRegions.ClaimKey(region));
            var biome = (BiomeMask)(int)sector.Biome;
            return SlagRegions.Waiting(region, sector.EdgeCount, biome, (BiomeMask)(int)KitSettings.SlagBiomes, holds, claimed) ? region : -1;
        }

        private static bool SenderNear(long sender, Vector3 point)
        {
            var peer = ZNet.instance?.GetPeer(sender);
            if (peer == null) return false;
            float dx = peer.m_refPos.x - point.x;
            float dz = peer.m_refPos.z - point.z;
            return SlagClaim.Near(Mathf.Sqrt(dx * dx + dz * dz));
        }

        private static SpawnSystemList Make()
        {
            var holder = new GameObject("MW_SlagCharred_spawns");
            holder.transform.SetParent(PrefabBench.Root, false);
            var made = holder.AddComponent<SpawnSystemList>();
            made.m_spawners.Add(Entry);
            return made;
        }
    }

    [HarmonyPatch(typeof(SpawnSystem), "Awake")]
    internal static class KitSlagJoin
    {
        private static void Postfix(SpawnSystem __instance) =>
            SafeCall.Run("SpawnSystem.Awake slag charred", static system => KitSpawns.Join(system), __instance);
    }

    [HarmonyPatch(typeof(SpawnSystem), "Spawn")]
    internal static class KitSlagGate
    {
        private static bool Prefix(SpawnSystem __instance, SpawnSystem.SpawnData critter, Vector3 spawnPoint)
        {
            if (critter?.m_prefab == null || KitPrefabs.Slag == null || critter.m_prefab != KitPrefabs.Slag || SpawnSystem.m_nospawn)
                return true;
            return SafeCall.Run("SpawnSystem.Spawn slag charred", static (system, point) => KitSpawns.Allow(system, point), __instance, spawnPoint, false);
        }
    }

    [HarmonyPatch(typeof(SpawnSystem), "Spawn")]
    internal static class KitShareSpawn
    {
        private static bool Prefix(SpawnSystem __instance, SpawnSystem.SpawnData critter, Vector3 spawnPoint, bool eventSpawner, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            var swap = SafeCall.Run("SpawnSystem.Spawn kit share",
                static (data, point, raid) => KitSpawns.Wild(data, point, raid), critter, spawnPoint, eventSpawner, null);
            if (swap == null) return true;
            __instance.Spawn(swap, spawnPoint, eventSpawner);
            return false;
        }
    }

    [HarmonyPatch(typeof(CreatureSpawner), "Spawn")]
    internal static class KitMarrowSpawn
    {
        private struct Hold
        {
            public bool Swap;
            public GameObject Prefab;
            public int Min;
            public int Max;
            public bool LocHeld;
            public Location Location;
            public int LocMin;
            public int LocMax;
            public float LocUp;
        }

        private static void Prefix(CreatureSpawner __instance, out Hold __state)
        {
            __state = default;
            if (!SafeCall.Run("CreatureSpawner.Spawn marrow lantern", static spawner => KitSpawns.Marrow(spawner), __instance, false))
                return;
            __state.Swap = true;
            __state.Prefab = __instance.m_creaturePrefab;
            __state.Min = __instance.m_minLevel;
            __state.Max = __instance.m_maxLevel;
            __instance.m_creaturePrefab = KitPrefabs.Marrow;
            __instance.m_minLevel = KitCreatures.SpawnLevel;
            __instance.m_maxLevel = KitCreatures.SpawnLevel;
            var location = __instance.m_location;
            if (location == null) return;
            __state.LocHeld = true;
            __state.Location = location;
            __state.LocMin = location.m_enemyMinLevelOverride;
            __state.LocMax = location.m_enemyMaxLevelOverride;
            __state.LocUp = location.m_enemyLevelUpOverride;
            location.m_enemyMinLevelOverride = -1;
            location.m_enemyMaxLevelOverride = -1;
            location.m_enemyLevelUpOverride = -1f;
        }

        private static void Finalizer(CreatureSpawner __instance, Hold __state)
        {
            if (__state.LocHeld && __state.Location != null)
            {
                __state.Location.m_enemyMinLevelOverride = __state.LocMin;
                __state.Location.m_enemyMaxLevelOverride = __state.LocMax;
                __state.Location.m_enemyLevelUpOverride = __state.LocUp;
            }
            if (!__state.Swap) return;
            __instance.m_creaturePrefab = __state.Prefab;
            __instance.m_minLevel = __state.Min;
            __instance.m_maxLevel = __state.Max;
        }
    }
}
