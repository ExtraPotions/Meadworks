// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The material every tether is drawn with, made once and shared: the game's own alpha-blended particle shader (the
    /// one its torch halos use) with its tint lifted past white, so the line's colour reaches the bloom and glows faintly,
    /// over a small soft-edged strip made here, so the line fades out across its width instead of ending in a hard edge.
    /// Alpha-blended rather than additive so it still reads against snow and a bright sky. Should that shader be missing,
    /// Unity's sprite shader draws the same strip without the glow.
    /// </summary>
    internal static class TetherGlow
    {
        private const string GlowShader = "Legacy Shaders/Particles/Alpha Blended";
        private const string PlainShader = "Sprites/Default";
        private const string TintProperty = "_TintColor";

        /// <summary>Texels across the strip, edge to edge.</summary>
        private const int Across = 32;

        /// <summary>The legacy shader doubles its tint, so 0.8 draws the line at 1.6 times its colour: enough to bloom.</summary>
        private static readonly Color Tint = new Color(0.8f, 0.8f, 0.8f, 0.5f);

        private static Material? _material;

        /// <summary>The shared material; made again if the game unloaded it. Null when neither shader is there.</summary>
        public static Material? Material()
        {
            if (_material != null)
            {
                return _material;
            }
            Shader? shader = Shader.Find(GlowShader) ?? Shader.Find(PlainShader);
            if (shader == null)
            {
                return null;
            }
            _material = new Material(shader) { mainTexture = Strip() };
            if (_material.HasProperty(TintProperty))
            {
                _material.SetColor(TintProperty, Tint);
            }
            return _material;
        }

        // White, opaque along the middle and fading to nothing at both edges.
        private static Texture2D Strip()
        {
            Texture2D texture = new Texture2D(1, Across, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            Color32[] texels = new Color32[Across];
            for (int y = 0; y < Across; y++)
            {
                float inner = 1f - Mathf.Abs(y / (Across - 1f) * 2f - 1f);
                texels[y] = new Color32(255, 255, 255, (byte)(255f * inner * inner));
            }
            texture.SetPixels32(texels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
