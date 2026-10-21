using ExtraPotions.Meadworks.Crucible;

namespace Burrow.Look
{
    /// <summary>
    /// The geometry and look of Burrow's panel buttons (Quick stack, Store all, Top up, Sort stacked under the weight plate
    /// of the player panel's armor/weight column, or in one row under the panel without Mantle), as plain numbers so the
    /// portable suite can check them without Unity. Heights, caption sizes and colors come from the Meadworks kit
    /// (Crucible.Core <c>KitButtonRow</c>, <c>KitButtonText</c>, <c>KitPalette</c>), so the buttons match every
    /// <c>KitButton</c> of the other products: a copy of the game's take-all button with a lit (white) face and a warm caption.
    /// </summary>
    public static class ButtonRowStyle
    {
        /// <summary>A column button's width before the weight plate's width is known.</summary>
        public const float Width = 78f;
        public const float Height = KitButtonRow.ButtonHeight;
        public const float Gap = 4f;

        /// <summary>Buttons stacked under the weight plate, top to bottom.</summary>
        public const int PlayerButtons = 4;

        public const float CaptionMinSize = KitButtonText.MinSize;
        public const float CaptionMaxSize = KitButtonText.MaxSize;

        /// <summary>TextMeshPro caption margin (left, top, right, bottom) as R, G, B, A.</summary>
        public static KitColor CaptionMargin => KitButtonText.Margin;

        /// <summary>The button image's color: the kit's lit tab (white, so the game sprite shows as drawn).</summary>
        public static KitColor Face => KitPalette.TabLit;

        /// <summary>The caption's color: the kit's warm lit text.</summary>
        public static KitColor Caption => KitPalette.TabLitText;

        /// <summary>
        /// The top edge of stacked button <paramref name="index"/>, the gap under <paramref name="floor"/> and each one step lower.
        /// The column's buttons are squares as wide as its plates (48 units), so a caption gets two lines.
        /// </summary>
        public static float StackTop(float floor, int index, float height = Height) => floor - Gap - index * (height + Gap);

        /// <summary>How far under the floor <paramref name="buttons"/> stacked buttons reach.</summary>
        public static float StackHeight(int buttons, float height = Height) => buttons <= 0 ? 0f : buttons * (height + Gap);

        /// <summary>The lowest column button: room for a two-line caption at about 10 points.</summary>
        public const float ColumnMinHeight = 30f;

        /// <summary>Kept clear above the player panel's bottom edge, where the panel art's ragged rim begins.</summary>
        public const float PanelInset = 4f;

        /// <summary>Column captions use the whole button less this inset on each side, so two lines fit a short button.</summary>
        public const float ColumnCaptionInsetX = 2f;
        public const float ColumnCaptionInsetY = 1f;

        public static bool IsPlayerColumnBox(float boxBottom, float playerBottom) => boxBottom >= playerBottom;

        /// <summary>
        /// The tallest column button: a two-line caption ("Quick / stack") at about 13 points with the caption insets.
        /// Taller buttons left the captions floating in empty frames beside Mantle's 64-unit plates (playtest 2026-10-20).
        /// </summary>
        public const float ColumnMaxHeight = 36f;

        /// <summary>
        /// The column buttons' height: the <paramref name="room"/> between the floor and the panel's bottom (less the inset)
        /// shared by <paramref name="buttons"/>, so the stack ends inside the panel; never under <see cref="ColumnMinHeight"/>,
        /// never over <see cref="ColumnMaxHeight"/> and never taller than the plates are wide.
        /// </summary>
        public static float ColumnHeight(float room, int buttons, float plateWidth) =>
            buttons <= 0 ? System.Math.Min(plateWidth, ColumnMaxHeight)
                : System.Math.Max(ColumnMinHeight, System.Math.Min(System.Math.Min(plateWidth, ColumnMaxHeight), (room - PanelInset) / buttons - Gap));

        /// <summary>
        /// Without Mantle or PackPanel the game's weight plate sits on the panel's bottom edge and four buttons cannot fit
        /// under it, so they stand in one row under the player panel (user decision 2026-10-12 20:19): wide enough for
        /// "Quick stack" on one line at 14 pt, the column's 30-unit minimum high.
        /// </summary>
        public const float RowWidth = 100f;
        public const float RowHeight = ColumnMinHeight;

        /// <summary>The center x of row button <paramref name="index"/> of <paramref name="buttons"/>, the row centered on <paramref name="center"/>.</summary>
        public static float RowCenter(float center, int index, int buttons) =>
            center - (buttons * RowWidth + (buttons - 1) * Gap) / 2f + index * (RowWidth + Gap) + RowWidth / 2f;

        /// <summary>
        /// The row's top edge: <see cref="Gap"/> under the player panel's bottom <paramref name="panelBottom"/>, raised (never
        /// above the panel's bottom) so the row ends on or above the container panel's top <paramref name="containerTop"/>.
        /// The game leaves exactly one row height between the two panels.
        /// </summary>
        public static float RowTop(float panelBottom, float containerTop) =>
            System.Math.Min(panelBottom, System.Math.Max(panelBottom - Gap, containerTop + RowHeight));
    }
}
