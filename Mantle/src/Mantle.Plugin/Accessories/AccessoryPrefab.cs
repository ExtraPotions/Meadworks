// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Meadworks.OriginalArt;
using ExtraPotions.Mantle.Embedded.BundlePrefabs;
using HarmonyLib;
using Mantle.Crafting;
using UnityEngine;

namespace Mantle.Accessories
{
    /// <summary>
    /// Builds every ring's and belt's item when ZNetScene wakes, the same on the server and every client, and registers it with
    /// ZNetScene and ObjectDB (the BundlePrefabs library), so a dropped ring and one in a chest mean the same prefab everywhere.
    /// Original ring and belt geometry supplies its matching rendered icon, preserving native item lifecycle and shaders.
    /// An item whose build fails is logged and left out, the others still come.
    /// </summary>
    public static class AccessoryPrefab
    {
        public static void Install(Harmony harmony) => NetPrefabs.OnSceneAwake(harmony, scene => Setup(scene, harmony));

        private static void Setup(ZNetScene scene, Harmony harmony)
        {
            foreach (AccessoryKind kind in AccessoryKinds.All)
            {
                try
                {
                    if (kind.Item == null)
                        Build(scene, kind);
                    NetPrefabs.Register(scene, kind.Item);
                    ItemPrefabs.Register(harmony, kind.Item);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"the {kind.Name} could not be built and is not in the game: {e.Message}");
                }
            }
            CraftRecipes.Install(ObjectDB.instance);
        }

        private static void Build(ZNetScene scene, AccessoryKind kind)
        {
            var template = scene.GetPrefab(CraftedItem.BaseItem);
            if (!template || template.GetComponent<ItemDrop>() == null || template.transform.Find("model") == null)
                throw new InvalidOperationException("Ring and belt item template requires item and model.");
            Sprite icon = OriginalItemArt.Icon(kind.Id);
            GameObject model = PrefabBench.Copy(OriginalItemArt.Model(scene, kind.Id), "model");
            model.SetActive(true);
            kind.Item = CraftedItem.Build(template, model, kind);
            kind.Item.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons = new[] { icon };
            Plugin.Log.LogInfo($"{kind.Name} ready: {kind.Id}");
        }
    }
}
