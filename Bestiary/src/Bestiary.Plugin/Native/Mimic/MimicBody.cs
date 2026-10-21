// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Mimic
{
    /// <summary>
    /// The mimic's visible body: our rig from the bundle (teeth, gums, tongue, eyes, animations) with the game's crypt
    /// chest hung on it - the base on the body bone, the lid on the lid bone - and every part dressed in the game's own
    /// materials: the teeth in Bone Fragments', the gums and tongue in the bear meat's, the eyes and throat in tinted
    /// copies. Nothing of the game's is in the bundle; the placeholders are replaced here by name.
    /// </summary>
    public static class MimicBody
    {
        private static readonly Color EyeGlow = new Color(0.75f, 1f, 0.2f);

        /// <summary>A fresh inactive copy of the rig, dressed; the caller parents it into a creature or corpse.</summary>
        public static GameObject Build(ZNetScene scene, GameObject model, GameObject chest)
        {
            GameObject body = PrefabBench.Copy(model, "Visual");
            HangChest(body.transform, chest);
            Material? flesh = GameMaterials.Borrow(scene.GetPrefab("BearMeat")) ?? GameMaterials.Borrow(scene.GetPrefab("RawMeat"));
            Material? bone = GameMaterials.Borrow(scene.GetPrefab("BoneFragments"));
            Material? stone = GameMaterials.Borrow(chest, "stonechest");
            GameMaterials.Apply(body, name => Pick(name, flesh, bone, stone));
            return body;
        }

        private static Material? Pick(string placeholder, Material? flesh, Material? bone, Material? stone)
        {
            switch (placeholder)
            {
                case "mimic_teeth": return bone;
                case "mimic_flesh": return flesh;
                case "mimic_throat": return Tinted(flesh, Color.black, Color.black);
                case "mimic_eye": return Tinted(stone, EyeGlow, EyeGlow * 3f);
                default: return null;
            }
        }

        /// <summary>A copy of a game material in one colour, optionally glowing, with its texture taken off.</summary>
        private static Material? Tinted(Material? source, Color colour, Color glow)
        {
            if (source == null)
            {
                return null;
            }
            var material = new Material(source) { color = colour, mainTexture = Texture2D.whiteTexture };
            if (glow.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", glow);
            }
            return material;
        }

        /// <summary>The chest's base follows the body bone and its closed lid the lid bone, placed as the rig rests.</summary>
        private static void HangChest(Transform body, GameObject chest)
        {
            Hang(body, "body", GameMaterials.Find(chest.transform, "stonechest"));
            Hang(body, "lid", GameMaterials.Find(chest.transform, "stonechesttop_closed"));
        }

        private static void Hang(Transform body, string bone, Transform? part)
        {
            Transform? joint = GameMaterials.Find(body, bone);
            if (joint == null || part == null)
            {
                Log.Warn($"Crypt mimic: no {bone} bone or chest part; the chest will be missing.");
                return;
            }
            GameObject copy = Object.Instantiate(part.gameObject);
            copy.name = "chest_" + bone;
            copy.transform.SetPositionAndRotation(body.position, body.rotation);
            copy.transform.SetParent(joint, true);
            foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);   // the creature has its own; a mesh collider cannot ride a rigidbody
            }
            copy.SetActive(true);
        }
    }
}
