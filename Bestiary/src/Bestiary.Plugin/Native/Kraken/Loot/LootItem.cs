// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// One of the kraken's item prefabs: a copy of the game's nearest item (net view, rigidbody, sync, the ground sparkle,
    /// sounds, and the shared data it starts from) wearing a model from the loot bundle in place of the game's. The game
    /// draws an item from its <c>attach</c> child in the hand, on a cooking station's spit and on an item stand, and a
    /// dropped item shows that child too, so the model goes where the game item's own model was: under <c>attach</c> when
    /// there is one, else on the root. It wears the game item's own material (the game's lighting, wetness and snow) with
    /// the model's baked albedo, on the game item's layer, and the bundle's icon (<c>&lt;asset&gt;_icon</c>).
    /// </summary>
    public static class LootItem
    {
        private const string Holder = "attach";
        private const string ModelName = "model";

        /// <summary>A copy of the game's item <paramref name="game"/>, named <paramref name="name"/>, wearing <paramref name="asset"/> centred and scaled.</summary>
        public static GameObject Build(ZNetScene scene, AssetBundle bundle, string game, string name, string asset, string word, float scale, float gloss)
        {
            GameObject item = Copy(scene, game, name);
            Swap(item, EmbeddedBundle.Prefab(bundle, asset), scale, gloss);
            Describe(item, bundle, asset, word);
            return item;
        }

        /// <summary>An inactive copy of the game's item, renamed; throws when the game has no such item.</summary>
        public static GameObject Copy(ZNetScene scene, string game, string name)
        {
            GameObject? prefab = scene.GetPrefab(game);
            if (prefab == null || prefab.GetComponent<ItemDrop>() == null)
            {
                throw new InvalidOperationException($"the game's {game} is missing or no longer an item");
            }
            return PrefabBench.Copy(prefab, name);
        }

        /// <summary>The game item's own model, where the new one goes; null when it has none.</summary>
        public static Transform? GameModel(GameObject item) => HolderOf(item).Find(ModelName);

        /// <summary>The game's model out, <paramref name="model"/> in, dressed, at <paramref name="scale"/>, centred on its holder.</summary>
        public static Transform Swap(GameObject item, GameObject model, float scale, float gloss)
        {
            Transform holder = HolderOf(item);
            Material? look = Look(item, model, gloss);
            Transform? old = holder.Find(ModelName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }
            GameObject copy = Object.Instantiate(model, holder, false);
            copy.name = ModelName;
            if (look != null)
            {
                GameMaterials.Apply(copy, placeholder => look);
            }
            foreach (Transform part in copy.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = holder.gameObject.layer;
            }
            copy.transform.localScale = Vector3.one * scale;
            copy.transform.localPosition = -ModelBounds.In(copy, holder).center;
            return copy.transform;
        }

        /// <summary>The item's name and description words (<c>$item_&lt;word&gt;</c>) and the bundle's icon for it.</summary>
        public static void Describe(GameObject item, AssetBundle bundle, string asset, string word)
        {
            ItemDrop.ItemData.SharedData shared = item.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_name = "$item_" + word;
            shared.m_description = "$item_" + word + "_description";
            Sprite? icon = bundle.LoadAsset<Sprite>(asset + "_icon");
            if (icon != null)
            {
                shared.m_icons = new[] { icon };
            }
            else
            {
                Log.Warn($"{item.name}: the bundle has no {asset}_icon; it shows the game item's icon.");
            }
        }

        private static Transform HolderOf(GameObject item) => item.transform.Find(Holder) ?? item.transform;

        /// <summary>The game item's material wearing the model's baked albedo, without the game's own detail maps.</summary>
        private static Material? Look(GameObject item, GameObject model, float gloss)
        {
            Material? game = GameMaterials.Borrow(item, ModelName);
            Renderer? renderer = model.GetComponentInChildren<Renderer>(true);
            if (game == null || renderer == null || renderer.sharedMaterial == null)
            {
                return null;
            }
            return GameMaterials.Plain(GameMaterials.Dress(game, renderer.sharedMaterial), gloss);
        }
    }
}
