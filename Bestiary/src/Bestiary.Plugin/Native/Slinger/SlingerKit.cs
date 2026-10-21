// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Slinger
{
    /// <summary>
    /// Puts the bundle's kit on the Greydwarf copy. The kit's top-level children are mounts named after the Greydwarf's
    /// bones (l_hand, r_hand, root); each is parented to its bone with no offset, so every part keeps the offset the
    /// workshop measured on the same skeleton at the game's scale (AssetWorkshop Slinger/SlingKit). Every part wears
    /// the Greydwarf's own material with the part's baked textures, and the pouch gets a copy of the stone's look.
    /// </summary>
    public static class SlingerKit
    {
        private const string StoneSlot = "ecr_sling_stone";

        public static void Wear(Transform visual, GameObject kitPrefab, Material skin, GameObject stone)
        {
            // Beside the Visual, not in it, so the bone search below never finds the kit's own mounts; under the
            // inactive copy, so nothing wakes.
            GameObject kit = Object.Instantiate(kitPrefab, visual.parent, false);
            Dress(kit, skin);
            foreach (Transform mount in kit.transform.Cast<Transform>().ToArray())
            {
                Hang(mount, visual);
            }
            Object.DestroyImmediate(kit);
            Pebble(GameMaterials.Find(visual, StoneSlot), SlingerStone.Visual(stone));
        }

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
                Log.Warn($"Greydwarf slinger: the Greydwarf has no {mount.name} bone; what hangs there is left off.");
                Object.DestroyImmediate(mount.gameObject);
                return;
            }
            mount.SetParent(bone, false);
            mount.localPosition = Vector3.zero;
            mount.localRotation = Quaternion.identity;
            mount.localScale = Vector3.one;
        }

        /// <summary>A copy of the flying stone's mesh, at its size, in the pouch; <see cref="SlingRig"/> shows it while drawn.</summary>
        private static void Pebble(Transform? slot, Transform? look)
        {
            MeshFilter? mesh = look?.GetComponentInChildren<MeshFilter>(true);
            MeshRenderer? renderer = mesh?.GetComponent<MeshRenderer>();
            if (slot == null || mesh == null || renderer == null)
            {
                return;
            }
            var pebble = new GameObject("ecr_sling_pebble") { layer = slot.gameObject.layer };
            pebble.transform.SetParent(slot, false);
            pebble.transform.localScale = mesh.transform.lossyScale / slot.lossyScale.x;
            pebble.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            pebble.AddComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial;
        }
    }
}
