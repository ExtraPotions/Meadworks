// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe lifecycle boundaries and original item models with matching rendered icons; tinted
// copies of existing rune art for the Honing and Tempering Runes.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Bundles;
using UnityEngine;
using Object = UnityEngine.Object;
using ExtraPotions.Meadworks.OriginalArt;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>Replaces cloned stone visuals while retaining item, network and rigidbody components.</summary>
    internal static class StoneTablets
    {
        /// <summary>The base every stone is copied from while it wears a model: the Ruby's group (a plain small item).</summary>
        public const StoneGroup BaseGroup = StoneGroup.Ascension;

        /// <summary>The game item whose material the models wear: opaque Standard with a normal map.</summary>
        public const string MaterialItem = "SerpentScale";

        private const string Holder = "attach";
        private const string ModelName = "model";

        internal static bool WearOriginal(StoneEntry entry, GameObject? materialItem)
        {
            // The Honing and Tempering Runes wear a tinted copy of an existing rune's art (user decision 2026-10-19: no new art).
            string art = Runtime.TintedRuneArt.ArtPrefab(entry.PrefabName);
            GameObject model = OriginalItemArt.Model(materialItem, art);
            Strip(entry.Prefab);
            GameObject placed = Place(entry.Prefab, model);
            Color tint = Color.white;
            bool tinted = art != entry.PrefabName && !StoneVisuals.Headless && StoneTints.TryById(entry.BuiltInId, out tint);
            if (tinted)
            {
                Runtime.TintedRuneArt.TintModel(placed, tint);
            }
            if (!StoneVisuals.Headless)
            {
                Sprite icon = OriginalItemArt.Icon(art);
                entry.Shared.m_icons = new[] { tinted ? StoneIcons.Tinted(icon, tint, entry.PrefabName) ?? icon : icon };
            }
            return true;
        }

        /// <summary>Removes the base's colliders and every part that draws or lights it (models, glows, sparkles).</summary>
        private static void Strip(GameObject item)
        {
            foreach (Collider collider in item.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
            Transform holder = HolderOf(item);
            foreach (GameObject part in LookParts(item))
            {
                if (part == null)
                {
                    continue;
                }
                if (part == item || part.transform == holder)
                {
                    DropDrawing(part);
                    continue;
                }
                Object.DestroyImmediate(part);
            }
        }

        // A particle system before its renderer: Unity refuses to remove a renderer a particle system still needs.
        private static readonly Type[] Drawing =
            { typeof(ParticleSystem), typeof(LODGroup), typeof(Light), typeof(Renderer), typeof(MeshFilter) };

        private static List<GameObject> LookParts(GameObject item)
        {
            var parts = new List<GameObject>();
            foreach (Type kind in Drawing)
            {
                foreach (Component c in item.GetComponentsInChildren(kind, true))
                {
                    parts.Add(c.gameObject);
                }
            }
            return parts;
        }

        // The root and the holder stay (they carry the item and the attach point); only their own drawing goes.
        private static void DropDrawing(GameObject part)
        {
            foreach (Type kind in Drawing)
            {
                foreach (Component c in part.GetComponents(kind))
                {
                    Object.DestroyImmediate(c);
                }
            }
        }

        /// <summary>The model as the item's: centred on the holder, on the item's layer, in the dressed material.</summary>
        private static GameObject Place(GameObject item, GameObject model)
        {
            Transform holder = HolderOf(item);
            GameObject copy = Object.Instantiate(model, holder, false);
            copy.name = ModelName;
            copy.SetActive(true);
            foreach (Transform part in copy.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = item.layer;
            }
            copy.transform.localPosition = -ModelBounds.In(copy, holder).center;
            return copy;
        }

        private static Transform HolderOf(GameObject item) => item.transform.Find(Holder) ?? item.transform;
    }
}
