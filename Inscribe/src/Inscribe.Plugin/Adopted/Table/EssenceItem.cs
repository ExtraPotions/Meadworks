// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Inscribe.Adopted.Tables
{
    /// <summary>
    /// Pure essence as an item, <c>ECF_Essence</c> (user decision 2026-10-07: "some kind of whiteish looking item ...
    /// think of this as the essence of the creature"): what a Rune Table's pool is made of, dropped when the table breaks,
    /// stored back with Store all, and paid from the table first and then the inventory. A copy of the game's Wisp (a
    /// soft pale glowing orb) with its own name, stack and icon (the embedded <c>ecf_essence_item.png</c> when there is
    /// one, else the Wisp's), and without anything that clears the mist. Made on every peer before any inventory or ZDO
    /// loads: added to the net scene's prefab list before its Awake and to every object database, as the runes are.
    /// </summary>
    [HarmonyPatch]
    internal static class EssenceItem
    {
        public const string PrefabName = "ECF_Essence";
        private const string Base = "Wisp";
        private const int Stack = 100;

        private static GameObject? _holder;

        public static GameObject? Prefab { get; private set; }

        public static bool Is(ItemDrop.ItemData? item) => item?.m_dropPrefab != null && item.m_dropPrefab.name == PrefabName;

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        [HarmonyPrefix]
        private static void SceneAwake(ZNetScene __instance) => Guard(() => AddTo(__instance.m_prefabs, null));

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void DatabaseAwake(ObjectDB __instance) => Guard(() => AddTo(__instance.m_items, __instance));

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void DatabaseCopy(ObjectDB __instance) => Guard(() => AddTo(__instance.m_items, __instance));

        private static void AddTo(List<GameObject> list, ObjectDB? database)
        {
            if (Prefab == null)
            {
                Prefab = Build(list.Find(p => p != null && p.name == Base));
            }
            if (Prefab == null || list.Contains(Prefab))
            {
                return;
            }
            list.Add(Prefab);
            database?.UpdateRegisters();
        }

        private static GameObject? Build(GameObject? wisp)
        {
            if (wisp == null || wisp.GetComponent<ItemDrop>() == null)
            {
                return null;
            }
            _holder = new GameObject("EliteCrafting_Essence");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);
            GameObject copy = Object.Instantiate(wisp, _holder.transform, false);
            copy.name = PrefabName;
            StripMistClearing(copy);
            Configure(copy.GetComponent<ItemDrop>(), copy);
            Log.Info("pure essence item built");
            return copy;
        }

        private static void Configure(ItemDrop drop, GameObject prefab)
        {
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            shared.m_name = "$ecf_essence_item";
            shared.m_description = "$ecf_essence_item_desc";
            shared.m_maxStackSize = Stack;
            shared.m_weight = 0.1f;
            shared.m_teleportable = true;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
            Sprite? icon = Window.EssenceIcons.Item();
            if (icon != null)
            {
                shared.m_icons = new[] { icon };
            }
            drop.m_itemData.m_dropPrefab = prefab;
        }

        // The Wisp's own job (pushing the Mistlands mist away) stays with the Wisp.
        private static void StripMistClearing(GameObject copy)
        {
            foreach (Component part in copy.GetComponentsInChildren<Component>(true))
            {
                if (part != null && (part.GetType().Name == "Demister" || part.GetType().Name == "ParticleSystemForceField"))
                {
                    Object.DestroyImmediate(part);
                }
            }
        }

        private static void Guard(Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                // Never out of ZNetScene.Awake or ObjectDB.Awake: a throw there stops the world loading.
                Log.Error($"the pure essence item could not be made: {e}");
            }
        }
    }
}
