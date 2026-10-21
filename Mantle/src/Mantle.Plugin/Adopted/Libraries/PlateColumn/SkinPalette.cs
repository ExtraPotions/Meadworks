using UnityEngine;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
{
    /// <summary>
    /// Every colour and shape of the <see cref="Skin"/> in one place, to tune the look here and nowhere else: warm browns
    /// in place of the game's wood, dark fills that let the icons and numbers stand out, thin lighter borders. Colours
    /// are baked into the textures, so an Image showing a skin sprite stays white. The fills are opaque: the game renders
    /// in linear colour space, where the few percent of the scene a dark fill lets through shows far more than its alpha
    /// suggests (0.94 looked half transparent over grass).
    /// </summary>
    internal static class SkinPalette
    {
        /// <summary>The panel's outermost line, darker than anything behind it so the frame reads against the world.</summary>
        public static readonly Color32 PanelOuterLine = new Color32(0x14, 0x0D, 0x07, 0xFF);

        /// <summary>The panel's bronze frame.</summary>
        public static readonly Color32 PanelFrame = new Color32(0x8C, 0x6A, 0x42, 0xFF);

        /// <summary>The line between the frame and the fill, so the frame looks raised.</summary>
        public static readonly Color32 PanelInnerLine = new Color32(0x1C, 0x13, 0x0B, 0xFF);

        /// <summary>#2E2014, opaque.</summary>
        public static readonly Color32 PanelFill = new Color32(0x2E, 0x20, 0x14, 0xFF);

        /// <summary>#1F150D, opaque: an inventory cell, darker than the panel it sits on.</summary>
        public static readonly Color32 CellFill = new Color32(0x1F, 0x15, 0x0D, 0xFF);

        public static readonly Color32 CellBorder = new Color32(0x5E, 0x46, 0x30, 0xFF);

        /// <summary>#281B10, opaque: a stat box.</summary>
        public static readonly Color32 BoxFill = new Color32(0x28, 0x1B, 0x10, 0xFF);

        public static readonly Color32 BoxBorder = new Color32(0x7D, 0x5E, 0x3B, 0xFF);

        /// <summary>Small captions a mod draws on or under the boxes.</summary>
        public static readonly Color32 Label = new Color32(0xD8, 0xC3, 0xA0, 0xFF);

        public static readonly SkinRecipe Panel = new SkinRecipe(Skin.PanelName, 32, 5f, 8, PanelFill,
            new SkinRing(1f, PanelOuterLine), new SkinRing(2f, PanelFrame), new SkinRing(1f, PanelInnerLine));

        public static readonly SkinRecipe Cell = new SkinRecipe(Skin.CellName, 24, 4f, 6, CellFill,
            new SkinRing(1f, CellBorder));

        public static readonly SkinRecipe Box = new SkinRecipe(Skin.BoxName, 24, 5f, 7, BoxFill,
            new SkinRing(2f, BoxBorder));
    }
}
