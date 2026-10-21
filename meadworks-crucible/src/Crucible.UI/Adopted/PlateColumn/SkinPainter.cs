// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/SkinPainter.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// Paints a <see cref="SkinRecipe"/> into a texture and makes a 9-slice sprite of it, with no image file: every pixel
    /// is coloured by how deep it lies inside the rounded outline, so the rings follow the corners and every edge,
    /// straight or curved, is anti-aliased over one pixel. Straight edges fall on pixel boundaries and stay crisp.
    /// </summary>
    internal static class SkinPainter
    {
        private const float PixelsPerUnit = 100f;

        public static Sprite Make(SkinRecipe recipe)
        {
            Texture2D texture = new Texture2D(recipe.Size, recipe.Size, TextureFormat.RGBA32, false)
            {
                name = recipe.Name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            texture.SetPixels32(Paint(recipe));
            texture.Apply(false, true);
            float b = recipe.Border;
            Rect whole = new Rect(0f, 0f, recipe.Size, recipe.Size);
            Sprite sprite = Sprite.Create(texture, whole, new Vector2(0.5f, 0.5f), PixelsPerUnit, 0u,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sprite.name = recipe.Name;
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        private static Color32[] Paint(SkinRecipe recipe)
        {
            int size = recipe.Size;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float depth = Depth(x + 0.5f, y + 0.5f, size, recipe.Radius);
                    pixels[y * size + x] = Shade(depth, recipe);
                }
            }
            return pixels;
        }

        /// <summary>How far a pixel centre lies inside the rounded square's outline, in pixels; negative outside it.</summary>
        private static float Depth(float x, float y, int size, float radius)
        {
            float dx = Mathf.Max(Mathf.Max(radius - x, x - (size - radius)), 0f);
            float dy = Mathf.Max(Mathf.Max(radius - y, y - (size - radius)), 0f);
            if (dx > 0f && dy > 0f)
            {
                return radius - Mathf.Sqrt(dx * dx + dy * dy);
            }
            return Mathf.Min(Mathf.Min(x, size - x), Mathf.Min(y, size - y));
        }

        /// <summary>
        /// The ring the depth falls in, blended over the pixel that straddles a ring's inner edge, and the outer edge's
        /// coverage in the alpha.
        /// </summary>
        private static Color32 Shade(float depth, SkinRecipe recipe)
        {
            SkinRing[] rings = recipe.Rings;
            Color colour = rings.Length > 0 ? rings[0].Colour : recipe.Fill;
            float edge = 0f;
            for (int i = 0; i < rings.Length; i++)
            {
                edge += rings[i].Width;
                Color inner = i + 1 < rings.Length ? rings[i + 1].Colour : recipe.Fill;
                colour = Color.Lerp(colour, inner, Mathf.Clamp01(depth - edge + 0.5f));
            }
            colour.a *= Mathf.Clamp01(depth + 0.5f);
            return colour;
        }
    }
}
