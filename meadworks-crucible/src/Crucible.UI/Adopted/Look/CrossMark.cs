// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Look/CrossMark.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: pixel coverage from Core KitPaint; Crucible sprite names.
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// A white cross (an X with round ends) as a sprite, painted once in code (no image file): tinted grey it marks a
    /// cell that is shown but not open (<see cref="BlockedCell"/>). Its strokes are anti-aliased over one pixel.
    /// </summary>
    public static class CrossMark
    {
        private const int Size = KitPaint.CrossSize;
        private static Sprite sprite;

        public static Sprite Sprite => sprite != null ? sprite : sprite = Paint();

        private static Sprite Paint()
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "Crucible_cross",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            texture.SetPixels32(KitPaintUnity.White(KitPaint.CrossAlpha()));
            texture.Apply(false, true);
            Sprite made = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            made.name = "Crucible_cross";
            made.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return made;
        }
    }
}
