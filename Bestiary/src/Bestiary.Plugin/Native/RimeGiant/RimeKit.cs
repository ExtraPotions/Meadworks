// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// Puts the bundle's kit on the troll copy: the eight plates of rime (<c>ecr_rime_plate_0</c> to <c>_7</c>) and the
    /// crust of snow it wears asleep (<c>ecr_rime_crust</c>...). The kit's top-level children are mounts named after
    /// the troll's bones; each is parented to its bone with no offset, so every part keeps the offset the workshop
    /// measured on the same skeleton at the game's scale (AssetWorkshop RimeGiant). Every part wears the troll's own
    /// material with the part's baked textures, so the ice is lit like the creature it grows on.
    /// </summary>
    public static class RimeKit
    {
        public const string Plate = "ecr_rime_plate_";
        public const string Crust = "ecr_rime_crust";

        public static void Wear(Transform visual, GameObject kitPrefab, Material skin)
        {
            // Beside the Visual, not in it, so the bone search never finds the kit's own mounts; under the inactive
            // copy, so nothing wakes.
            GameObject kit = Object.Instantiate(kitPrefab, visual.parent, false);
            Dress(kit, skin);
            foreach (Transform mount in kit.transform.Cast<Transform>().ToArray())
            {
                Hang(mount, visual);
            }
            Object.DestroyImmediate(kit);
        }

        /// <summary>The troll's body material (its largest skinned mesh's), which every part of the kit is lit with.</summary>
        public static Material Skin(Transform troll) =>
            troll.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.sharedMesh != null ? r.sharedMesh.vertexCount : 0).First().sharedMaterial;

        /// <summary>The plates under a giant, by number; a missing one is null.</summary>
        public static GameObject?[] Plates(Transform giant, int count)
        {
            var plates = new GameObject?[count];
            for (int i = 0; i < count; i++)
            {
                plates[i] = GameMaterials.Find(giant, Plate + i)?.gameObject;
            }
            return plates;
        }

        /// <summary>The crust's pieces under a giant.</summary>
        public static List<GameObject> CrustPieces(Transform giant) =>
            giant.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(Crust)).Select(t => t.gameObject).ToList();

        private static void Dress(GameObject kit, Material skin)
        {
            var dressed = new Dictionary<Material, Material>();
            foreach (Renderer renderer in kit.GetComponentsInChildren<Renderer>(true))
            {
                Material placeholder = renderer.sharedMaterial;
                if (!dressed.TryGetValue(placeholder, out Material material))
                {
                    material = GameMaterials.Dress(skin, placeholder);
                    dressed[placeholder] = material;
                }
                renderer.sharedMaterial = material;
            }
        }

        private static void Hang(Transform mount, Transform visual)
        {
            Transform? bone = GameMaterials.Find(visual, mount.name);
            if (bone == null)
            {
                Log.Warn($"Rime giant: the troll has no {mount.name} bone; what hangs there is left off.");
                Object.DestroyImmediate(mount.gameObject);
                return;
            }
            mount.SetParent(bone, false);
            mount.localPosition = Vector3.zero;
            mount.localRotation = Quaternion.identity;
            mount.localScale = Vector3.one;
        }
    }
}
