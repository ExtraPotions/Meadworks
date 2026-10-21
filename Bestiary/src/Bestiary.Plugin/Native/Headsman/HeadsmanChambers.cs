// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// Where the Crypt Executioner waits: in the settings' share of the Black Forest's burial chambers (the dungeons of
    /// the ForestCrypt theme), one per chamber, in its largest room (not the entrance, a dead end or a doorway). The
    /// chamber's own seed decides, once per chamber, on the machine that generates it or, for chambers made before the
    /// mod, on the first owner to load it; the choice is kept in the chamber's ZDO. The room gets a spawner of the game's
    /// kind (<see cref="Spawner"/>, saved with the world) that raises the Executioner when a player comes near and again
    /// the settings' days after it dies, and none while the Executioner is off.
    /// </summary>
    public static class HeadsmanChambers
    {
        public const string Spawner = "ECP_Headsman_spawner";
        private const string RolledKey = "ecp_hs_chamber", Off = "ecp_headsman_off";
        private const int Salt = 0x48534d4e;
        private const float Trigger = 25f;

        private static GameObject? prefab;

        public static GameObject BuildSpawner(GameObject boss)
        {
            prefab = new GameObject(Spawner);
            prefab.transform.SetParent(PrefabBench.Root, false);
            ZNetView view = prefab.AddComponent<ZNetView>();
            (view.m_persistent, view.m_type) = (true, ZDO.ObjectType.Default);
            CreatureSpawner spawner = prefab.AddComponent<CreatureSpawner>();
            (spawner.m_creaturePrefab, spawner.m_minLevel, spawner.m_maxLevel, spawner.m_levelupChance) = (boss, 1, 1, 0f);
            (spawner.m_triggerDistance, spawner.m_spawnInterval, spawner.m_setPatrolSpawnPoint) = (Trigger, 5, true);
            Apply(spawner);
            return prefab;
        }

        /// <summary>The return time; while the Executioner is off, a global key nobody has keeps it from spawning.</summary>
        private static void Apply(CreatureSpawner spawner)
        {
            spawner.m_respawnTimeMinuts = HeadsmanSettings.RespawnMinutes;
            spawner.m_requiredGlobalKey = HeadsmanSettings.On ? "" : Off;
        }

        /// <summary>After a settings change: the prefab and every spawner loaded.</summary>
        public static void Reapply()
        {
            if (prefab != null)
            {
                Apply(prefab.GetComponent<CreatureSpawner>());
            }
            foreach (CreatureSpawner live in CreatureSpawner.m_creatureSpawners.Where(s => s != null && Utils.GetPrefabName(s.gameObject) == Spawner))
            {
                Apply(live);
            }
        }

        /// <summary>A chamber being generated (on its generating machine): its rooms are placed, not yet saved.</summary>
        [HarmonyPatch(typeof(DungeonGenerator), "GenerateRooms")]
        public static class Generated
        {
            private static void Postfix(DungeonGenerator __instance, ZoneSystem.SpawnMode mode)
            {
                if (mode != ZoneSystem.SpawnMode.Client)
                {
                    SafeCall.Run("DungeonGenerator.GenerateRooms crypt executioner", static (dungeon, ghost) =>
                    {
                        if (Pending(dungeon))
                        {
                            Roll(dungeon, DungeonGenerator.m_placedRooms.ToList(), ghost);
                        }
                    }, __instance, mode == ZoneSystem.SpawnMode.Ghost);
                }
            }
        }

        /// <summary>A saved chamber's rooms placed as it loads: chambers from before the mod roll on their owner.</summary>
        [HarmonyPatch(typeof(DungeonGenerator), "Spawn")]
        public static class Loaded
        {
            private static void Postfix(DungeonGenerator __instance)
            {
                if (__instance.m_nview != null && __instance.m_nview.IsOwner())
                {
                    SafeCall.Run("DungeonGenerator.Spawn crypt executioner", static dungeon =>
                    {
                        if (Pending(dungeon))   // every dungeon of every theme loads here: rooms are gathered only for ours
                        {
                            Roll(dungeon, dungeon.GetComponentsInChildren<Room>().ToList(), false);
                        }
                    }, __instance);
                }
            }
        }

        /// <summary>Whether this is a burial chamber not rolled yet, while the Executioner is on.</summary>
        private static bool Pending(DungeonGenerator dungeon)
        {
            ZDO? zdo = dungeon.m_nview != null ? dungeon.m_nview.GetZDO() : null;
            return prefab != null && zdo != null && HeadsmanSettings.On && (dungeon.m_themes & Room.Theme.ForestCrypt) != 0
                && !zdo.GetBool(RolledKey);
        }

        /// <summary>Once per chamber (<see cref="Pending"/>): by its seed, a spawner in its largest room (only its ZDO kept when generated as a ghost).</summary>
        private static void Roll(DungeonGenerator dungeon, List<Room> rooms, bool ghost)
        {
            dungeon.m_nview.GetZDO().Set(RolledKey, true);
            Room? hall = Hall(rooms);
            if (hall == null || new System.Random(dungeon.GetSeed() ^ Salt).NextDouble() >= HeadsmanSettings.Chambers)
            {
                return;
            }
            GameObject spawner = Object.Instantiate(prefab!, Floor(hall), hall.transform.rotation);   // Pending checked it
            Log.Info($"Crypt Executioner waits in {dungeon.name}'s {hall.name}.");
            if (ghost)
            {
                Object.Destroy(spawner);
            }
        }

        private static Room? Hall(List<Room> rooms) =>
            rooms.Where(r => r != null && !r.m_entrance && !r.m_endCap && !r.m_divider).OrderByDescending(r => r.m_size.x * r.m_size.z).FirstOrDefault();

        /// <summary>The room's floor under its middle.</summary>
        private static Vector3 Floor(Room room)
        {
            Vector3 from = room.transform.position + Vector3.up;
            int mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            return Physics.Raycast(from, Vector3.down, out RaycastHit hit, room.m_size.y + 2f, mask) ? hit.point : room.transform.position;
        }
    }
}
