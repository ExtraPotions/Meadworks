// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// Where a rune use is paid from when no rune stack is carried: the Rune Table (its own store, then the player's
    /// inventory, and the essence that steers the roll). The pipeline asks it instead of the carried stack; it pays in
    /// the same frame as the write, on the client that holds the table open.
    /// </summary>
    internal interface IRuneSupply
    {
        /// <summary>How many of the rune can be paid now.</summary>
        int Held(string? runeId);

        /// <summary>Why this use cannot be paid besides the rune count (no essence, the table lost), or null.</summary>
        StoneMessage? Shortfall(StoneJob job);

        /// <summary>Takes the cost: the runes and the essence. Called once, right after the item was written.</summary>
        void Pay(StoneJob job);

        /// <summary>The inscriptions the chosen essence guarantees (only these are drawn); null = a plain roll.</summary>
        ISet<string>? Favoured { get; }
    }
}
