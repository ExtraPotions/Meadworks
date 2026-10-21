// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// Registers <c>inscribe</c> when the game builds its console command table (game notes Q21). Runs on every peer that
    /// has a terminal: clients, the host and a dedicated server's console. <c>InitTerminal</c> is guarded by the game and
    /// our registration is idempotent, so the console and chat terminals coming up never register twice.
    /// </summary>
    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    internal static class TerminalInitPatch
    {
        [HarmonyPostfix]
        private static void Postfix() => EcraftCommand.Register();
    }
}
