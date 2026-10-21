namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Which game font the kit reads with. The first text under the inventory or main menu is a heading in the capitals-only
    /// Norse font, so body text asks for the reading font by name: Averia Serif ("Valheim-AveriaSerifLibre" in game).
    /// </summary>
    public static class KitFontRules {
        public const string ReadingPrefix="Valheim-AveriaSerif";
        public static bool IsReading(string? name)=>name!=null&&name.StartsWith(ReadingPrefix,System.StringComparison.Ordinal);
    }

    /// <summary>The multiline editor's line-number column sits inside the field box, left of the text.</summary>
    public static class KitMultilineLayout {
        public const float Gutter=36f;
        public const float Gap=6f;
        /// <summary>The gutter's left edge inside the box.</summary>
        public const float GutterLeft=KitInputLayout.InsetX;
        /// <summary>The text area's left inset inside the box, past the gutter when line numbers are shown.</summary>
        public static float TextLeft(bool lineNumbers)=>KitInputLayout.InsetX+(lineNumbers?Gutter+Gap:0f);
    }

    /// <summary>
    /// The game's popup shade (measured on ServerListGui's Add server form): black at alpha 123/255 over the whole screen
    /// behind a form; its yes/no popups and the password dialog have none.
    /// </summary>
    public static class KitPopupShade {
        public const byte Alpha=123;
        public static readonly KitColor Colour=KitColor.FromBytes(0,0,0,Alpha);
    }
}
