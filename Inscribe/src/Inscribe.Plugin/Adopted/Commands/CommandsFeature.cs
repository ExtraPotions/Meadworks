// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// Entry point of the Commands area, called once from plugin Awake after the rules, settings and words are loaded.
    /// Owns: the <c>inscribe</c> console command (console-commands.md). The command is registered from a postfix on
    /// <c>Terminal.InitTerminal</c> (<see cref="TerminalInitPatch"/>); if the terminal already initialised before this
    /// plugin loaded, it is registered here instead. Registration is idempotent.
    /// </summary>
    public static class CommandsFeature
    {
        public static void Init()
        {
            if (Terminal.m_terminalInitialized)
            {
                EcraftCommand.Register();
            }
        }
    }
}
