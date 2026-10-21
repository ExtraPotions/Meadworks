// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGateSurfaceLook.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The resources every gate's sheet shares: one material on an unlit, alpha-blended, two-sided shader the
    /// game already has loaded, and a small tileable ripple texture made here, so nothing ships as an asset. Made on
    /// first use, destroyed on world unload.</summary>
    internal static class SeaGateSurfaceLook
    {
        private const int TextureSize = 64;
        private const float TwoPi = Mathf.PI * 2f;

        /// <summary>Unlit, alpha-blended, vertex-coloured and without back-face culling, best first.</summary>
        private static readonly string[] ShaderNames =
        {
            "Legacy Shaders/Particles/Alpha Blended",
            "Sprites/Default",
            "UI/Default"
        };

        private static Material material;
        private static Texture2D texture;
        private static bool searched;

        /// <summary>The shared sheet material; null when none of the shaders is loaded.</summary>
        internal static Material SheetMaterial
        {
            get
            {
                if (material == null && !searched)
                {
                    searched = true;
                    material = NewMaterial();
                }
                return material;
            }
        }

        internal static void Forget()
        {
            if (material != null)
                Object.Destroy(material);
            if (texture != null)
                Object.Destroy(texture);
            material = null;
            texture = null;
            searched = false;
        }

        private static Material NewMaterial()
        {
            Shader shader = FindShader();
            if (shader == null)
            {
                SeaGateLog.LogWarning("Sea gate surface: no unlit transparent shader found; gates show only the portal effect.");
                return null;
            }
            texture = NewTexture();
            Material made = new Material(shader) { name = "Threshold_SeaGateSheet", mainTexture = texture };
            if (made.HasProperty("_TintColor"))
                made.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));  // the legacy particle shaders double it
            if (made.HasProperty("_Color"))
                made.SetColor("_Color", Color.white);
            return made;
        }

        private static Shader FindShader()
        {
            foreach (string name in ShaderNames)
            {
                Shader shader = Shader.Find(name);
                if (shader != null)
                    return shader;
            }
            return null;
        }

        /// <summary>White with a rippling alpha. Every term has whole-number frequencies in u and v, so it tiles.</summary>
        private static Texture2D NewTexture()
        {
            Texture2D made = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, mipChain: true)
            {
                name = "Threshold_SeaGateRipple",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Color[] pixels = new Color[TextureSize * TextureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                float u = (float)(i % TextureSize) / TextureSize;
                float v = (float)(i / TextureSize) / TextureSize;
                float value = Ripple(u, v);
                pixels[i] = new Color(1f, 1f, 1f, Mathf.Lerp(0.25f, 1f, value * value));
            }
            made.SetPixels(pixels);
            made.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return made;
        }

        /// <summary>0 to 1: two warped wave trains crossing, plus a slow diagonal swell.</summary>
        private static float Ripple(float u, float v)
        {
            float a = Mathf.Sin(TwoPi * (2f * u + 0.35f * Mathf.Sin(TwoPi * v)));
            float b = Mathf.Sin(TwoPi * (3f * v + 0.3f * Mathf.Sin(TwoPi * 2f * u)));
            float c = Mathf.Sin(TwoPi * (u + v) + 0.5f * Mathf.Sin(TwoPi * 3f * v));
            return Mathf.Clamp01(0.5f + 0.3f * a * b + 0.2f * c);
        }
    }
}
