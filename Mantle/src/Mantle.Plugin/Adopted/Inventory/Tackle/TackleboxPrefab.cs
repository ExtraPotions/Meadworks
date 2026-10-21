using System;
using ExtraPotions.Meadworks.OriginalArt;
using ExtraPotions.Mantle.Embedded.BundlePrefabs;
using HarmonyLib;
using Mantle.Crafting;
using UnityEngine;

namespace Mantle.Tackle
{
    /// <summary>
    /// Builds every tacklebox's item when ZNetScene wakes, the same on the server and every client, and registers it with
    /// ZNetScene and ObjectDB (the BundlePrefabs library), so a dropped box and one in a chest mean the same prefab
    /// everywhere. Original geometry and its model-rendered icon retain native item lifecycle and material shaders.
    /// A box is never worn on the body, so the item is all there is. Built once per process;
    /// art failures leave the native item registered. A box listed in <see cref="TackleboxCatalog.Tinted"/>
    /// wears another box's model and icon with its own frost tint on copied materials and a copied icon (no new art).
    /// </summary>
    public static class TackleboxPrefab
    {
        public static void Install(Harmony harmony) => NetPrefabs.OnSceneAwake(harmony, scene => Setup(scene, harmony));

        private static void Setup(ZNetScene scene, Harmony harmony)
        {
            foreach (TackleboxKind kind in TackleboxCatalog.All)
            {
                try
                {
                    bool built = kind.Item == null;
                    if (built)
                        Build(scene, kind);
                    NetPrefabs.Register(scene, kind.Item);
                    ItemPrefabs.Register(harmony, kind.Item);
                    if (built) Dress(scene, kind);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"the {kind.Name} could not be built and is not in the game: {e.Message}");
                }
            }
            CraftRecipes.Install(ObjectDB.instance);
        }

        private static void Build(ZNetScene scene, TackleboxKind kind)
        {
            var template = scene.GetPrefab(CraftedItem.BaseItem);
            if (!template || template.GetComponent<ItemDrop>() == null)
                throw new InvalidOperationException("Tacklebox item template requires an item.");
            kind.Item = CraftedItem.Build(template, kind);
            Plugin.Log.LogInfo($"{kind.Name} ready: {kind.Id}");
        }

        private static void Dress(ZNetScene scene, TackleboxKind kind)
        {
            if (ExtraPotions.Meadworks.Crucible.MeadworksHeadless.IsHeadless) return;
            try
            {
                bool tinted = TackleboxCatalog.Tinted.TryGetValue(kind.Id, out var tint);
                string art = tinted ? tint.Art : kind.Id;
                Sprite icon = OriginalItemArt.Icon(art);
                GameObject model = PrefabBench.Copy(OriginalItemArt.Model(scene, art), "model");
                if (tinted)
                {
                    var frost = new Color(tint.R, tint.G, tint.B, 1f);
                    TackleboxTint.Model(model, frost);
                    icon = TackleboxTint.Icon(icon, frost);
                }
                model.SetActive(true);
                CraftedItem.Dress(kind.Item, model);
                kind.Item.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons = new[] { icon };
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning($"The {kind.Name} art did not load ({error.Message}); its item remains registered.");
            }
        }
    }
}
