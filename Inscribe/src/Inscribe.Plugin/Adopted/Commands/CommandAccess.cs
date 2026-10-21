// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using ExtraPotions.Inscribe.Adopted.Config;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// Access rules (console-commands.md section 2, DECISIONS.md CMD-1). Checked on the machine that runs the command:
    /// every admin sub-command changes only the caller's own inventory or files, which the game trusts a client with
    /// anyway. Admin = the host, single player, a dedicated server's own console, or a player on the server's admin
    /// list (<c>ZNet.LocalPlayerIsAdminOrHost</c>, which reads the admin list the server synced to the client). With no
    /// network session at all (main menu) nothing can be harmed, so everything is allowed.
    /// </summary>
    internal static class CommandAccess
    {
        public static bool IsAdmin => ZNet.instance == null || ZNet.instance.LocalPlayerIsAdminOrHost();

        /// <summary>The synced <c>Read-only commands for everyone</c> switch (the server's value while it binds).</summary>
        public static bool ReadOnlyOpen => ModSettings.ReadOnlyCommandsForEveryone == null || ModSettings.ReadOnlyCommandsForEveryone.Value;

        /// <summary>This machine's own files are in force: single player, the host, a server, a player the server does not bind.</summary>
        public static bool IsAuthor => ExtraPotions.Meadworks.Crucible.Plugin.Session.IsAuthority;

        public static bool Allows(Access access)
        {
            switch (access)
            {
                case Access.Everyone: return true;
                case Access.ReadOnly: return ReadOnlyOpen || IsAdmin;
                case Access.Author: return IsAdmin && IsAuthor;
                default: return IsAdmin && Runtime.Plugin.Settings?.Get(global::ExtraPotions.Inscribe.Config.InscribeSettings.Cheats.ItemCommands) == true;
            }
        }

        /// <summary>Replies with the refusal and returns false when the caller may not run the sub-command.</summary>
        public static bool Check(SubCommand command, CommandCall call)
        {
            if (!Runtime.Plugin.Ready || (!Runtime.InscribeModules.Active(global::ExtraPotions.Inscribe.Integration.InscribeModule.Rarity) && (command.Name == "roll" || command.Name == "reroll" || command.Name == "inscribe" || command.Name == "cleanse"))) { call.Reply("Rarity is disabled or stepped aside."); return false; }
            if (!Allows(command.Access))
            {
                call.ReplyPlain($"{EcraftCommand.Name}: {command.Name} needs admin rights on this server.");
                return false;
            }
            if (command.Access == Access.Author && !IsAuthor)
            {
                call.ReplyPlain($"{EcraftCommand.Name}: {command.Name} changes the server's files; run it on the server.");
                return false;
            }
            return true;
        }
    }
}
