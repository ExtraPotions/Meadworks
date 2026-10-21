// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Bestiary.Runtime.Prefabs
{
    /// <summary>
    /// Bleaches a game model or icon to bone, at run time, from the game's own textures: each texel's brightness is laid
    /// on a ramp from old dark bone to bleached bone, so the painted grain, wear and shading of the game's art stay and
    /// only the color changes. Nothing is shipped: the source is the running game's texture, the ramp is two colors.
    /// Used for the Bone Ballista (the game's ballista) and its icon. Copies are made once per source and shared.
    /// A dedicated server draws nothing and is left alone.
    /// </summary>
    public static class BoneDye
    {
        private static readonly Color Dark = new Color(0.30f, 0.26f, 0.20f), Light = new Color(0.95f, 0.91f, 0.81f);
        private const float Gain = 1.7f;

        private static readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
        private static readonly Dictionary<Texture, Texture2D> textures = new Dictionary<Texture, Texture2D>();

        private static bool Draws => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        /// <summary>Every renderer of `model` in bone copies of its materials.</summary>
        public static void Dress(GameObject model)
        {
            if (!Draws)
                return;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                    shared[i] = For(shared[i]);
                renderer.sharedMaterials = shared;
            }
        }

        /// <summary>A bone copy of an icon (its own cut of its atlas), or the icon itself when nothing draws.</summary>
        public static Sprite? Icon(Sprite? icon)
        {
            if (icon == null || !Draws)
                return icon;
            Texture2D whole = Dye(icon.texture);
            Rect cut = icon.textureRect;
            var own = new Texture2D((int)cut.width, (int)cut.height, TextureFormat.RGBA32, false) { name = icon.name + "_bone" };
            own.SetPixels(whole.GetPixels((int)cut.x, (int)cut.y, (int)cut.width, (int)cut.height));
            own.Apply();
            return Sprite.Create(own, new Rect(0f, 0f, own.width, own.height), new Vector2(0.5f, 0.5f), icon.pixelsPerUnit);
        }

        private static Material For(Material source)
        {
            if (source == null || materials.ContainsValue(source))
                return source!;
            if (!materials.TryGetValue(source, out Material made))
            {
                made = materials[source] = new Material(source) { name = source.name + "_bone" };
                if (source.HasProperty("_MainTex") && source.mainTexture != null)
                    made.mainTexture = Dye(source.mainTexture);
                else
                    made.color = Light;
            }
            return made;
        }

        /// <summary>The texture read back through the GPU (game textures are not readable) and laid on the bone ramp.</summary>
        private static Texture2D Dye(Texture source)
        {
            if (textures.TryGetValue(source, out Texture2D done))
                return done;
            RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            var dyed = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true) { name = source.name + "_bone" };
            dyed.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Color32[] texels = dyed.GetPixels32();
            for (int i = 0; i < texels.Length; i++)
                texels[i] = Ramp(texels[i]);
            dyed.SetPixels32(texels);
            dyed.Apply(true);
            return textures[source] = dyed;
        }

        /// <summary>One texel on the ramp by its brightness; its alpha kept.</summary>
        internal static Color32 Ramp(Color32 texel)
        {
            float brightness = (0.299f * texel.r + 0.587f * texel.g + 0.114f * texel.b) / 255f;
            Color bone = Color.Lerp(Dark, Light, Mathf.Clamp01(brightness * Gain));
            return new Color32((byte)(bone.r * 255f), (byte)(bone.g * 255f), (byte)(bone.b * 255f), texel.a);
        }
    }
}
