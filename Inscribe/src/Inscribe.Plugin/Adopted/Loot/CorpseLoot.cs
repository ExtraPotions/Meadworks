// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Our creature drops appear when the game's own do (drops.md section 1): a creature whose ragdoll drops items has
    /// its loot stored on the ragdoll and spawned when the ragdoll dissolves, at the ragdoll's body; with no such ragdoll
    /// the game drops at the moment of death. The death hook opens a window (<see cref="Begin"/>) in which
    /// <see cref="LootSpawner.Drop"/> holds items instead of spawning them. The ragdoll's setup, inside the same death
    /// handling, writes them into the ragdoll's ZDO as <c>ecf_corpse_loot</c> (each item's full saved data, custom data
    /// included, so pre-rolled gear keeps its inscriptions); the end of the death drops whatever no ragdoll took. When
    /// the ragdoll's owner dissolves it, the game spawns its loot and ours is spawned beside it, once: the data lives in
    /// the ZDO, so a ragdoll that changes owner or is unloaded and loaded again still carries it. Main thread.
    /// </summary>
    internal static class CorpseLoot
    {
        /// <summary>Byte array on a ragdoll's ZDO: the item version, the count, then per item its prefab hash and saved data.</summary>
        public const string Key = "ecf_corpse_loot";

        private static readonly int KeyHash = Key.GetStableHashCode();
        private const float LootOffset = 0.75f;   // the game's own lift above the ragdoll's body for its loot

        private static readonly List<ItemDrop.ItemData> Held = new List<ItemDrop.ItemData>();
        private static bool _open;
        private static Vector3 _center;

        /// <summary>Opens the window for one death; <paramref name="center"/> is where held items drop if no ragdoll takes them.</summary>
        public static void Begin(Vector3 center)
        {
            Held.Clear();
            _open = true;
            _center = center;
        }

        /// <summary>Holds a copy of the item (its stack set to <paramref name="amount"/>) while a window is open; false: spawn it now.</summary>
        public static bool Hold(ItemDrop.ItemData item, int amount)
        {
            if (!_open)
            {
                return false;
            }
            ItemDrop.ItemData copy = item.Clone();
            copy.m_stack = amount;
            Held.Add(copy);
            return true;
        }

        /// <summary>Moves the held items onto a ragdoll that drops loot (the game's own rule, <c>m_dropItems</c>).</summary>
        public static void AttachTo(Ragdoll ragdoll)
        {
            if (!_open || Held.Count == 0 || !ragdoll.m_dropItems || ragdoll.m_nview == null || !ragdoll.m_nview.IsValid())
            {
                return;
            }
            ragdoll.m_nview.GetZDO().Set(KeyHash, Encode(Held));
            Held.Clear();
        }

        /// <summary>Closes the window: whatever no ragdoll took drops now, at the creature's center, as the game's own does.</summary>
        public static void End()
        {
            if (!_open)
            {
                return;
            }
            _open = false;
            foreach (ItemDrop.ItemData item in Held)
            {
                LootSpawner.Drop(item, item.m_stack, _center);
            }
            Held.Clear();
        }

        /// <summary>Spawns the items a dissolving ragdoll carries at the game's own loot spot for it, then clears them.</summary>
        public static void SpawnFrom(Ragdoll ragdoll, Vector3 center)
        {
            ZDO zdo = ragdoll.m_nview.GetZDO();
            byte[] data = zdo.GetByteArray(KeyHash);
            if (data == null || data.Length == 0)
            {
                return;
            }
            zdo.Set(KeyHash, Array.Empty<byte>());   // once, even if the game's own spawn throws and the ragdoll retries
            foreach (ItemDrop.ItemData item in Decode(data))
            {
                LootSpawner.Drop(item, item.m_stack, center + Vector3.up * LootOffset);
            }
        }

        private static byte[] Encode(List<ItemDrop.ItemData> items)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write((int)global::Version.c_ItemDataVersion);
            pkg.Write(items.Count);
            foreach (ItemDrop.ItemData item in items)
            {
                pkg.Write(item.m_dropPrefab.name.GetStableHashCode());
                item.Save(pkg);
            }
            return pkg.GetArray();
        }

        private static List<ItemDrop.ItemData> Decode(byte[] data)
        {
            ZPackage pkg = new ZPackage(data);
            global::Version.Item version = (global::Version.Item)pkg.ReadInt();
            int count = pkg.ReadInt();
            List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(count);
            for (int i = 0; i < count; i++)
            {
                ItemDrop.ItemData? item = Read(pkg, version);
                if (item != null)
                {
                    items.Add(item);
                }
            }
            return items;
        }

        // Always reads the whole entry, so a missing prefab (a mod removed meanwhile) skips that item and not the rest.
        private static ItemDrop.ItemData? Read(ZPackage pkg, global::Version.Item version)
        {
            int hash = pkg.ReadInt();
            GameObject prefab = ZNetScene.instance.GetPrefab(hash);
            ItemDrop? template = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            ItemDrop.ItemData item = template != null ? template.m_itemData.Clone() : new ItemDrop.ItemData();
            ItemDrop.ItemData.Load(pkg, item, version);
            if (template == null)
            {
                Log.Warn($"corpse loot: item prefab {hash} is not registered; it cannot drop");
                return null;
            }
            item.m_dropPrefab = prefab;
            return item;
        }
    }

    /// <summary>
    /// The ragdoll a dying creature leaves takes the held drops: the game sets it up inside the creature's death
    /// handling, on the creature's owner, after our death hook rolled. A failure here keeps the drops for the end of the
    /// death rather than breaking it (a throw would leave the creature undestroyed).
    /// </summary>
    [HarmonyPatch(typeof(Ragdoll), nameof(Ragdoll.Setup))]
    internal static class RagdollSetupPatch
    {
        private static void Postfix(Ragdoll __instance)
        {
            try
            {
                CorpseLoot.AttachTo(__instance);
            }
            catch (Exception e)
            {
                Log.Error($"corpse loot: storing drops on the ragdoll failed, they drop now instead: {e}");
            }
        }
    }

    /// <summary>
    /// The game spawns a dissolving ragdoll's loot here, on the ragdoll's owner; ours spawns at the same spot. A failure
    /// must not stop the game's own loot or the ragdoll's removal.
    /// </summary>
    [HarmonyPatch(typeof(Ragdoll), nameof(Ragdoll.SpawnLoot))]
    internal static class RagdollSpawnLootPatch
    {
        private static void Prefix(Ragdoll __instance, Vector3 center)
        {
            try
            {
                CorpseLoot.SpawnFrom(__instance, center);
            }
            catch (Exception e)
            {
                Log.Error($"corpse loot: spawning the ragdoll's drops failed: {e}");
            }
        }
    }
}
