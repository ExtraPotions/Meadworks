// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The kraken's visible body from the embedded bundle (AssetWorkshop assets/ecp_kraken): the head and six copies of
    /// the long tentacle under one <see cref="KrakenBody.ModelName"/> object, dressed in the game's serpent material
    /// (<see cref="KrakenLook"/>), drawn on the serpent's own layer. Nothing animates them but the mod's code, so any
    /// animator the import added is taken off. The living creature's copy also gets its hit boxes, on a fixed body of
    /// their own so the tentacles' moving colliders never weigh on the creature's physics.
    /// </summary>
    public static class KrakenModel
    {
        public const string Bundle = "ecp_kraken";
        public const string HeadAsset = "ecp_kraken_head";
        public const string TentacleAsset = "ecp_kraken_tentacle";

        public static Transform Build(Transform parent, AssetBundle bundle, Material skin, int layer, bool hitboxes)
        {
            var model = new GameObject(KrakenBody.ModelName);
            model.transform.SetParent(parent, false);
            var dressed = new Dictionary<Material, Material>();
            Part(EmbeddedBundle.Prefab(bundle, HeadAsset), model.transform, KrakenBody.HeadName, skin, dressed, layer);
            GameObject tentacle = EmbeddedBundle.Prefab(bundle, TentacleAsset);
            for (int i = 0; i < KrakenBody.Tentacles; i++)
            {
                Part(tentacle, model.transform, KrakenBody.TentaclePrefix + i, skin, dressed, layer);
            }
            if (hitboxes)
            {
                KrakenHitboxes.Add(model.transform);
                model.AddComponent<Rigidbody>().isKinematic = true;
            }
            return model.transform;
        }

        private static void Part(GameObject prefab, Transform model, string name, Material skin, Dictionary<Material, Material> dressed, int layer)
        {
            GameObject part = Object.Instantiate(prefab, model, false);
            part.name = name;
            KrakenLook.Dress(part, skin, dressed);
            foreach (Transform piece in part.GetComponentsInChildren<Transform>(true))
            {
                piece.gameObject.layer = layer;
            }
            foreach (SkinnedMeshRenderer renderer in part.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.updateWhenOffscreen = true;   // posed far from its rest bounds, it must never be culled wrongly
            }
            foreach (Component extra in part.GetComponentsInChildren<Animator>(true))
            {
                Object.DestroyImmediate(extra);
            }
            foreach (Component extra in part.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(extra);
            }
        }
    }
}
