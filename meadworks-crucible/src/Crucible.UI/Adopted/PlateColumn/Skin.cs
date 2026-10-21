// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/Skin.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using System;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// The brown look the stat boxes share with anything else a mod wants to match to them: 9-slice sprites painted in
    /// code the first time they are asked for (no image file ships), kept for the rest of the session and made again if
    /// Unity ever unloads them. Their colours are baked in, so an Image showing one keeps its colour white; draw it with
    /// <c>Image.Type.Sliced</c>. The colours and shapes are tuned in <see cref="SkinPalette"/>. Each mod that merges this
    /// library paints its own copies; they look the same.
    /// </summary>
    public static class Skin
    {
        internal const string PanelName = "PlateColumn_skin_panel";
        internal const string CellName = "PlateColumn_skin_cell";
        internal const string BoxName = "PlateColumn_skin_box";

        private static Sprite? panel;
        private static Sprite? cell;
        private static Sprite? box;

        /// <summary>
        /// An inventory panel's background: a dark line, a bronze frame, a second dark line, then a dark brown fill that
        /// lets a little of the scene through; rounded corners.
        /// </summary>
        public static Sprite Panel => Cached(ref panel, SkinPalette.Panel);

        /// <summary>An inventory cell: a darker fill with a thin brown border and slightly rounded corners.</summary>
        public static Sprite Cell => Cached(ref cell, SkinPalette.Cell);

        /// <summary>A stat box in the column: a dark brown fill with a two-pixel lighter brown border, rounded corners.</summary>
        public static Sprite Box => Cached(ref box, SkinPalette.Box);

        /// <summary>A light parchment tone for small captions drawn with the skin.</summary>
        public static Color LabelColour => SkinPalette.Label;

        private static Sprite Cached(ref Sprite? slot, SkinRecipe recipe)
        {
            if (slot == null)
            {
                slot = SkinPainter.Make(recipe);
            }
            return slot;
        }

        /// <summary>Whether <paramref name="sprite"/> is this skin's box, painted by any copy of the library.</summary>
        internal static bool IsBox(Sprite? sprite) =>
            sprite != null && string.Equals(sprite.name, BoxName, StringComparison.Ordinal);
    }
}
