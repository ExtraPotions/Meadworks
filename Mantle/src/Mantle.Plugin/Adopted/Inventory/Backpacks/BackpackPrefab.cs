using System;
using ExtraPotions.Meadworks.OriginalArt;
using ExtraPotions.Mantle.Embedded.BundlePrefabs;
using HarmonyLib;
using Mantle.Crafting;
using UnityEngine;

namespace Mantle.Backpacks
{
    /// <summary>
    /// Builds every backpack's prefabs when ZNetScene wakes, the same on the server and every client, and registers the
    /// items with ZNetScene and ObjectDB (the BundlePrefabs library), so a dropped pack and one in a chest mean the same
    /// prefab everywhere. Original model-rendered icons and worn geometry share the same authored source.
    /// The catalog, Spine2 attachment, item properties and recipe requirements remain unchanged.
    /// Built once per process; a pack whose model is missing or fails is logged and left out, the others still come.
    /// </summary>
    public static class BackpackPrefab
    {
        public static void Install(Harmony harmony) => NetPrefabs.OnSceneAwake(harmony, scene => Setup(scene, harmony));

        private static void Setup(ZNetScene scene, Harmony harmony)
        {
            foreach (BackpackKind kind in BackpackCatalog.All)
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

        private static void Build(ZNetScene scene, BackpackKind kind)
        {
            var template = scene.GetPrefab(CraftedItem.BaseItem);
            if (!template || template.GetComponent<ItemDrop>() == null || template.transform.Find("model") == null)
                throw new InvalidOperationException("Backpack item template requires item and model.");
            GameObject model = OriginalItemArt.Model(scene, kind.Id);
            Sprite icon = OriginalItemArt.Icon(kind.Id);
            GameObject worn = PrefabBench.Copy(model, "Mantle_worn_" + kind.Word);
            worn.SetActive(true);
            foreach (Collider collider in worn.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            GameObject dropped = PrefabBench.Copy(model, "model");
            dropped.SetActive(true);
            kind.Item = CraftedItem.Build(template, dropped, kind);
            kind.Worn = worn;
            kind.Item.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons = new[] { icon };
            Plugin.Log.LogInfo($"{kind.Name} ready: {kind.Id}");
        }
    }
}
