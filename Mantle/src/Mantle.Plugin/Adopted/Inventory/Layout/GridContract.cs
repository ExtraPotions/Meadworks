using System.Globalization;

namespace Mantle.Layout
{
    /// <summary>
    /// What another mod may read about the local player's grid without referencing Mantle: the character's custom
    /// data key <c>Mantle.mainGrid</c> holds "width|rows|blocked" (invariant integers). The main grid is the first
    /// <c>rows</c> rows (a worn backpack's rows included), <c>width</c> wide; the last <c>blocked</c> cells of its bottom
    /// row hold nothing (a backpack's partly used last row); every row below is Mantle's slots, which bulk moves and
    /// sorts leave alone. Written with every layout applied while Mantle is on, removed when it is switched off.
    /// OpenKeep (1.8.0 or later) reads it for quick stack, store all, dump, sort and a shared chest's stack all. The key
    /// is saved with the character, so a reader trusts it only while Mantle is loaded.
    /// </summary>
    public static class GridContract
    {
        public const string Key = "Mantle.mainGrid";

        public static void Write(Player player, InventoryLayout layout)
        {
            player.m_customData[Key] = string.Join("|",
                layout.Width.ToString(CultureInfo.InvariantCulture),
                layout.MainRows.ToString(CultureInfo.InvariantCulture),
                layout.BlockedCells.ToString(CultureInfo.InvariantCulture));
        }

        public static void Clear(Player player) => player.m_customData.Remove(Key);
    }
}
