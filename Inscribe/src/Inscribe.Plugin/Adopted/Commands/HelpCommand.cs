// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary><c>inscribe [help]</c>: one line per sub-command this player may run (console-commands.md section 1).</summary>
    internal static class HelpCommand
    {
        public static void Run(CommandCall call)
        {
            call.Reply("sub-commands you may run here (ids are case-insensitive):");
            foreach (SubCommand command in EcraftCommand.SubCommands)
            {
                if (CommandAccess.Allows(command.Access))
                {
                    call.Detail($"{command.Grammar} - {command.Summary}");
                }
            }
            if (!CommandAccess.IsAdmin)
            {
                call.Detail("give, roll, reroll, inscribe, reload, dump and tiers need admin rights on this server.");
            }
        }
    }
}
