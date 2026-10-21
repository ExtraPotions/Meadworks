// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The kraken's materials and textures. Every part of the model wears the game's sea serpent's own creature material
    /// with the part's baked textures (so it is lit, fogged and wet like the game's creatures), without the serpent's
    /// metal, gloss and glow maps; the eyes glow a little.
    /// The ink splats for the screen come from the same bundle.
    /// </summary>
    public static class KrakenLook
    {
        public const string EyeMaterial = "ecp_kraken_eye";
        public const string InkPrefix = "ecp_kraken_ink";
        private static readonly Color EyeGlow = new Color(0.9f, 0.55f, 0.1f) * 0.6f;

        /// <summary>The serpent's body material: its largest skinned mesh's.</summary>
        public static Material Skin(GameObject serpent) =>
            serpent.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .OrderByDescending(r => r.sharedMesh != null ? r.sharedMesh.vertexCount : 0).First().sharedMaterial;

        /// <summary>Every placeholder material on the part replaced by the skin wearing its textures, one copy per placeholder.</summary>
        public static void Dress(GameObject part, Material skin, Dictionary<Material, Material> dressed)
        {
            foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = materials[i] != null ? Dressed(materials[i], skin, dressed) : materials[i];
                }
                renderer.sharedMaterials = materials;
            }
        }

        /// <summary>The screen's ink splats in the bundle (none if it has none: the screen then draws its own).</summary>
        public static Texture2D[] Splats(AssetBundle bundle) =>
            bundle.LoadAllAssets<Texture2D>().Where(texture => texture.name.StartsWith(InkPrefix)).ToArray();

        /// <summary>The game's own icon for being covered in tar, for the ink's status icon; null if the game has none.</summary>
        public static Sprite? InkIcon()
        {
            StatusEffect? tared = ObjectDB.instance != null ? ObjectDB.instance.GetStatusEffect("Tared".GetStableHashCode()) : null;
            return tared != null ? tared.m_icon : null;
        }

        private static Material Dressed(Material placeholder, Material skin, Dictionary<Material, Material> dressed)
        {
            if (!dressed.TryGetValue(placeholder, out Material material))
            {
                material = Glow(GameMaterials.Dress(skin, placeholder));
                dressed[placeholder] = material;
            }
            return material;
        }

        // The serpent's own maps are laid out for the serpent: its metal-and-gloss map and its cyan glow map would paint
        // the kraken metallic teal with glowing patches. Off with them: a wet, slightly glossy skin, and only the eyes glow.
        private static Material Glow(Material material)
        {
            GameMaterials.Plain(material, 0.3f);
            if (material.name.StartsWith(EyeMaterial))
            {
                material.SetColor("_EmissionColor", EyeGlow);
                material.EnableKeyword("_EMISSION");
            }
            return material;
        }
    }
}
