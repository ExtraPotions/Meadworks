// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Recap;
using ExtraPotions.Wildblood.Adopted.Recap.Window;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Commands
{
    /// <summary>
    /// <c>deaths</c>, typed as <c>/deaths</c> in chat or <c>deaths</c> in the F5 console: opens the death recap window,
    /// like its key. Open to every player - not a cheat, no devcommands, no admin check - since it shows only this
    /// player's own deaths, recorded on this machine. The game has no <c>deaths</c> command of its own.
    /// </summary>
    public static class DeathsCommand
    {
        public const string Name = "deaths";

        private static bool _registered;

        /// <summary>Idempotent, like <see cref="DamageCommand.Register"/>, and driven from the same Terminal init.</summary>
        public static void Register()
        {
            if (_registered)
            {
                return;
            }
            _registered = true;
            new Terminal.ConsoleCommand(Name, "opens the death recap window (Elite Creatures Reborn)",
                (Terminal.ConsoleEvent)OnDeaths);
        }

        private static void OnDeaths(Terminal.ConsoleEventArgs args) =>
            Guard.Run("deaths command", () => Run(args.Context));

        private static void Run(Terminal context)
        {
            if (!RecapWindow.Open())
            {
                context?.AddString("deaths: the recap window can only open in a world.");
                return;
            }
            context?.AddString($"deaths: {RecapStore.Deaths.Count} kept.");
        }
    }
}
