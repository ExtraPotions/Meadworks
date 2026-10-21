// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, ValheimModLibs, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Globalization;

namespace ExtraPotions.Wildblood.AdoptedLibs.PlayerGrid
{
    /// <summary>
    /// The writing side, for PackPanel: the character's custom data key <c>PackPanel.mainGrid</c> holds
    /// "width|rows|blocked" (invariant integers). The main grid is the first <c>rows</c> rows (a worn backpack's rows
    /// included), <c>width</c> wide; the last <c>blocked</c> cells of its bottom row hold nothing (a backpack's partly
    /// used last row); every row below is PackPanel's slots. Written with every layout applied while PackPanel is on,
    /// removed when it is switched off. The key is saved with the character and outlives the mod, so
    /// <see cref="PackPanelGrid"/> trusts it only while PackPanel lays the inventory out.
    /// </summary>
    public static class GridContract
    {
        public const string Key = "PackPanel.mainGrid";

        public static void Write(Player player, int width, int mainRows, int blockedCells) =>
            player.m_customData[Key] = string.Join("|",
                width.ToString(CultureInfo.InvariantCulture),
                mainRows.ToString(CultureInfo.InvariantCulture),
                blockedCells.ToString(CultureInfo.InvariantCulture));

        public static void Clear(Player player) => player.m_customData.Remove(Key);

        /// <summary>The three numbers of a written value; false for anything else.</summary>
        public static bool TryParse(string value, out int width, out int mainRows, out int blockedCells)
        {
            width = mainRows = blockedCells = 0;
            string[] parts = value.Split('|');
            return parts.Length == 3
                && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out mainRows)
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out blockedCells);
        }
    }
}
