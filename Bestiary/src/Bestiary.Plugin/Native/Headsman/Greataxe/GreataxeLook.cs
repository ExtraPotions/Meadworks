// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The bundle's models in the game items' "attach" (what a hand holds, and what lies on the ground): the game's
    /// model, collider and upgrade glow out, ours in at its own pose in the hand's frame, dressed in the bone material,
    /// with a box round it to lie on; and the Battleaxe's swing trail laid along our blade.
    /// </summary>
    public static class GreataxeLook
    {
        private const string Kept = "equiped";   // the Battleaxe's trail holder

        public static void Wear(Transform attach, Transform model, Material skin)
        {
            Strip(attach);
            GameObject copy = Object.Instantiate(model.gameObject, attach, false);
            copy.name = model.name;
            (copy.transform.localPosition, copy.transform.localRotation, copy.transform.localScale) = (model.localPosition, model.localRotation, model.localScale);
            foreach (Transform part in copy.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = attach.gameObject.layer;
            }
            HeadsmanKit.Dress(copy, skin);
            Bounds bounds = ModelBounds.In(copy, attach);
            var box = attach.gameObject.AddComponent<BoxCollider>();
            (box.center, box.size) = (bounds.center, Vector3.Max(bounds.size, Vector3.one * 0.05f));
        }

        /// <summary>The game's model, colliders and glow off the attach; the trail holder stays.</summary>
        private static void Strip(Transform attach)
        {
            Object.DestroyImmediate(attach.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(attach.GetComponent<MeshFilter>());
            foreach (Collider collider in attach.GetComponents<Collider>())
            {
                Object.DestroyImmediate(collider);
            }
            foreach (Transform child in attach.Cast<Transform>().Where(c => c.name != Kept).ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>The swing trail from the haft beside the blade out to the edge's middle.</summary>
        public static void Trail(Transform attach)
        {
            Transform? axe = attach.Cast<Transform>().FirstOrDefault(c => c.name != Kept);
            Transform? from = GameMaterials.Find(attach, "base"), to = GameMaterials.Find(attach, "tip");
            if (axe == null || from == null || to == null)
            {
                return;
            }
            from.position = axe.TransformPoint(HeadsmanAxe.Spine(HeadsmanAxe.Edge.y));
            to.position = axe.TransformPoint(HeadsmanAxe.Edge);
        }
    }
}
