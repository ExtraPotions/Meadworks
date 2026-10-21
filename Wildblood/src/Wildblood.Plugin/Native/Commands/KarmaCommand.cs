// SPDX-License-Identifier: GPL-3.0-only
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0, adapted from CreatureConsoleCommands.cs (ExecuteKarma and
// RequireAuthoritativeAdmin) at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// Changed by expDARE, 2026: the `wildblood karma` verb of the Creature Commands cheat; the admin and cheat gates are checked by
// the console route, the Pressure module and Karma mode are checked here.
using System.Globalization;
using ExtraPotions.Wildblood.AdoptedCm;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Runtime;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Commands
{
    /// <summary>
    /// <c>wildblood karma [value]</c>: shows, or sets, the Karma of the 3x3 zone neighbourhood around the local player. Karma lives on
    /// the machine that runs the world (single-player, a hosting player or the dedicated server), so it answers only there, as
    /// CreatureManager's cm:karma does.
    /// </summary>
    public static class KarmaCommand
    {
        public static void Run(Terminal.ConsoleEventArgs args)
        {
            if (!WildbloodModules.Active(WildbloodModule.Pressure))
            {
                CommandReply.Send(args, "wildblood karma: the Pressure module is off.");
                return;
            }
            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                CommandReply.Send(args, "wildblood karma: Karma is kept by the machine that runs the world; run this on the server host.");
                return;
            }
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                CommandReply.Send(args, "wildblood karma: needs a local player to place the neighborhood.");
                return;
            }
            Vector3 position = player.transform.position;
            if (args.Length == 2)
            {
                CommandReply.Send(args, CreatureKarmaManager.GetDebugLine(position));
                return;
            }
            if (!float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                || float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                CommandReply.Send(args, "usage: wildblood karma [non-negative value]");
                return;
            }
            CreatureKarmaManager.SetDebugKarma(position, value);
            CommandReply.Send(args, CreatureKarmaManager.GetDebugLine(position));
        }
    }
}
