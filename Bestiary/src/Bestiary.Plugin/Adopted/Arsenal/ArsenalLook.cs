// Portions Copyright (C) 2026 Gerald Glasgow.
// EliteCreaturesPack 0.7.2 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: EliteCreaturesPack/EliteCreaturesPack/Arsenal/ArsenalLook.cs
// Changed by expDARE, 2026: namespace isolation for Bestiary; reference algorithms retained.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Bestiary.Runtime.Adopted.Arsenal
{
    /// <summary>
    /// Puts the bundle's bone models on the game's copies. Every model was built in the frame of the item's "attach"
    /// (the fist at its origin, the weapon as the game holds its own of the class), so it goes in at the slot's origin
    /// with no offset. Every part wears the Skeleton's own body material with the part's baked textures, so the bone
    /// is lit and grimed like the skeletons that carry it. The bows get their string here, a thin box between the tips
    /// (AssetWorkshop out/ecp_skel_bow*_points.json), in the colour of the skeleton archer's string.
    /// </summary>
    public static class ArsenalLook
    {
        /// <summary>The bow's tips as the skeleton archer holds it, and as a player holds a bow, in the attach frame.</summary>
        public static readonly (Vector3 top, Vector3 bottom) SkeletonBow =
            (new Vector3(0.0514f, 0.5783f, -0.4628f), new Vector3(-0.5625f, -0.1298f, 0.4669f));

        public static readonly (Vector3 top, Vector3 bottom) PlayerBow =
            (new Vector3(-0.3211f, 0.0730f, -0.6654f), new Vector3(-0.3555f, -0.1356f, 0.6375f));

        private const float StringWidth = 0.008f;   // the skeleton bow's: a 0.01 box at 0.8 scale

        /// <summary>The skeleton archer's bowstring material, found before its bow is stripped; null leaves the bone.</summary>
        public static Material? StringMaterial;

        /// <summary>The Skeleton's own body material, which the models copy so they are lit like the creature.</summary>
        public static Material Skin(Transform visual) =>
            visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.bounds.size.sqrMagnitude).First().sharedMaterial;

        /// <summary>Every child of the slot goes (the game's model, its colliders) but the named ones.</summary>
        public static void Clear(Transform slot, params string[] keep)
        {
            foreach (Transform child in slot.Cast<Transform>().Where(c => !keep.Contains(c.name)).ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>The game's model on the slot itself goes: its level of detail, renderer, mesh and colliders.</summary>
        public static void Strip(Transform slot)
        {
            Component[] parts = { slot.GetComponent<LODGroup>(), slot.GetComponent<MeshRenderer>(), slot.GetComponent<MeshFilter>() };
            foreach (Component part in parts.Concat(slot.GetComponents<Collider>()).Where(p => p != null))
            {
                Object.DestroyImmediate(part);
            }
            (slot.localPosition, slot.localRotation, slot.localScale) = (Vector3.zero, Quaternion.identity, Vector3.one);
        }

        /// <summary>A copy of the model at the slot's origin, on the slot's layer, without colliders, dressed.</summary>
        public static GameObject Wear(Transform slot, GameObject model, Material skin)
        {
            GameObject copy = Object.Instantiate(model, slot, false);
            copy.name = model.name;
            (copy.transform.localPosition, copy.transform.localRotation, copy.transform.localScale) = (Vector3.zero, Quaternion.identity, Vector3.one);
            foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
            foreach (Transform part in copy.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = slot.gameObject.layer;
            }
            Dress(copy, skin);
            return copy;
        }

        /// <summary>Every renderer under `model` onto a copy of the skin material wearing its own baked textures.</summary>
        public static void Dress(GameObject model, Material skin)
        {
            var dressed = new Dictionary<Material, Material>();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material placeholder = renderer.sharedMaterial;
                if (!dressed.TryGetValue(placeholder, out Material material))
                {
                    material = GameMaterials.Plain(GameMaterials.Dress(skin, placeholder), 0.1f);
                    dressed[placeholder] = material;
                }
                renderer.sharedMaterial = material;
            }
        }

        /// <summary>A box round the model on a new child of the slot, for the item lying on the ground; returns the box.</summary>
        public static Bounds Collide(Transform slot, GameObject model)
        {
            Bounds bounds = ModelBounds.In(model, slot);
            var holder = new GameObject("collider");
            holder.transform.SetParent(slot, false);
            holder.layer = slot.gameObject.layer;
            var box = holder.AddComponent<BoxCollider>();
            (box.center, box.size) = (bounds.center, bounds.size);
            return bounds;
        }

        /// <summary>
        /// The game's upgrade glow (brighter with each quality level) shaped for the game's model; emitted from a box
        /// round ours instead, its light at the box's middle.
        /// </summary>
        public static void Glow(Transform slot, Bounds bounds)
        {
            Transform? glow = slot.Find("UpgraderGlow");
            if (glow == null)
            {
                return;
            }
            (glow.localPosition, glow.localRotation, glow.localScale) = (Vector3.zero, Quaternion.identity, Vector3.one);
            foreach (ParticleSystem particles in glow.GetComponentsInChildren<ParticleSystem>(true))
            {
                Transform at = particles.transform;
                (at.localPosition, at.localRotation, at.localScale) = (Vector3.zero, Quaternion.identity, Vector3.one);
                ParticleSystem.ShapeModule shape = particles.shape;
                (shape.shapeType, shape.position, shape.rotation, shape.scale) = (ParticleSystemShapeType.Box, bounds.center, Vector3.zero, bounds.size);
            }
            foreach (Light light in glow.GetComponentsInChildren<Light>(true))
            {
                light.transform.localPosition = bounds.center;
            }
        }

        /// <summary>A bowstring from tip to tip on the slot.</summary>
        public static void String(Transform slot, (Vector3 top, Vector3 bottom) tips, Material fallback)
        {
            var bowstring = new GameObject("string");
            bowstring.transform.SetParent(slot, false);
            bowstring.layer = slot.gameObject.layer;
            Vector3 along = tips.bottom - tips.top;
            bowstring.transform.localPosition = (tips.top + tips.bottom) / 2f;
            bowstring.transform.localRotation = Quaternion.LookRotation(along);
            bowstring.transform.localScale = new Vector3(StringWidth, StringWidth, along.magnitude);
            bowstring.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            bowstring.AddComponent<MeshRenderer>().sharedMaterial = StringMaterial != null ? StringMaterial : fallback;
        }
    }
}
