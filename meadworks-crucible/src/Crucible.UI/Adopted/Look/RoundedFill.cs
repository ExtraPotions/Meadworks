// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Look/RoundedFill.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: pixel coverage from Core KitPaint; Crucible sprite names.
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// A white rounded rectangle as a 9-slice sprite, painted once in code (no image file): tinted by an Image's colour
    /// it gives a fill round corners (PackPanel's stat sheet inset). The corners are <see cref="Radius"/> UI units
    /// whatever the size; their edge is anti-aliased over one texture pixel, the straight edges stay crisp.
    /// </summary>
    public static class RoundedFill
    {
        public const float Radius = KitPaint.RoundedRadius;

        private const int Corner = KitPaint.RoundedCorner;
        private const int Size = KitPaint.RoundedSize;
        private static Sprite sprite;

        public static Sprite Sprite => sprite != null ? sprite : sprite = Paint();

        private static Sprite Paint()
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "Crucible_rounded",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            texture.SetPixels32(KitPaintUnity.White(KitPaint.RoundedAlpha()));
            texture.Apply(false, true);
            Sprite made = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Corner * 100f / Radius,
                0u, SpriteMeshType.FullRect, new Vector4(Corner, Corner, Corner, Corner));
            made.name = "Crucible_rounded";
            made.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return made;
        }
    }

    internal static class KitPaintUnity
    {
        /// <summary>White pixels with the given alpha, in <c>SetPixels32</c> order.</summary>
        public static Color32[] White(byte[] alpha)
        {
            Color32[] pixels = new Color32[alpha.Length];
            for (int i = 0; i < alpha.Length; i++)
                pixels[i] = new Color32(255, 255, 255, alpha[i]);
            return pixels;
        }
    }
}
