// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Sockets
{
    /// <summary>The <c>Gems and sockets</c> switch (synced): off, no sockets on drops and the chisel and gems neither drop nor work.</summary>
    internal static class SocketSwitch
    {
        public static bool On => ModSettings.GemsAndSockets?.Value ?? true;

        /// <summary>Whether this stone may drop: every rune, and the chisel and gems while the switch is on.</summary>
        public static bool Drops(StoneDef stone) => On || !StoneCatalog.IsSocketStone(stone.Id);
    }
}
