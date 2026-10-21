// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Tally;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Commands
{
    /// <summary>
    /// <c>damage</c>, typed as <c>/damage</c> in chat or <c>damage</c> in the F5 console: shows the latest boss damage
    /// board again for the usual time, whether it is still up or has gone. Open to every player - not a cheat, no
    /// devcommands, no admin check - since it only shows what was already sent to everyone. This machine's own latest
    /// board is shown when it has one; a player who joined after the kill has none and asks the server for its board
    /// (<see cref="BossBoardRecall"/>). The game has no <c>damage</c> command of its own (its <c>test damage</c> is a log
    /// switch, not a command), so the plain word is free.
    /// </summary>
    public static class DamageCommand
    {
        public const string Name = "damage";

        private const string None = "damage: no boss has fallen since the world was loaded.";

        private static bool _registered;

        /// <summary>Idempotent, like <see cref="DeathsCommand.Register"/>, and driven from the same Terminal init.</summary>
        public static void Register()
        {
            if (_registered)
            {
                return;
            }
            _registered = true;
            new Terminal.ConsoleCommand(Name, "shows the latest boss damage board again (Elite Creatures Reborn)",
                (Terminal.ConsoleEvent)OnDamage);
        }

        private static void OnDamage(Terminal.ConsoleEventArgs args) =>
            Guard.Run("damage command", () => Run(args.Context));

        private static void Run(Terminal context)
        {
            if (ZNet.instance == null)
            {
                Reply(context, "damage: no world is loaded.");
                return;
            }
            BossBoard? board = BossBoard.Latest;
            if (board != null)
            {
                Show(context, board);
            }
            else if (!BossBoardRecall.Request(answer => Answered(context, answer)))
            {
                Reply(context, None);
            }
        }

        private static void Answered(Terminal context, BossBoard? board)
        {
            if (board == null)
            {
                Reply(context, None);
                return;
            }
            Show(context, board);
        }

        private static void Show(Terminal context, BossBoard board)
        {
            if (!BossBoardView.Replay(board))
            {
                Reply(context, "damage: the board cannot be drawn right now.");
            }
        }

        // The answer from the server comes a moment later; the chat or console it was typed in may be gone by then.
        private static void Reply(Terminal context, string text)
        {
            if (context != null)
            {
                context.AddString(text);
            }
        }
    }
}
