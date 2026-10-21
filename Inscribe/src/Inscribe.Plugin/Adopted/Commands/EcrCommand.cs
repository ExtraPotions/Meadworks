// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Loot;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// <c>inscribe ecr</c> (ecr-integration.md section 11, DECISIONS ECR-12): read-only. Whether Elite Creatures Reborn is
    /// installed on this machine and the synergy switch, whether ECR records a world tier, the hovered creature's ECR
    /// keys and the terms the drop roll would use for it (<see cref="EcrPreview"/>, the roll's own path), and how many of
    /// the last qualifying deaths rolled here carried ECR data (<see cref="EcrWatch"/>). Runs on the caller's machine and
    /// reads only replicated data and the synced rules; the death counts are this machine's own rolls.
    /// </summary>
    internal static class EcrCommand
    {
        public const string Grammar = "inscribe ecr";

        public static void Run(CommandCall call)
        {
            string synergy = $"synergy {(ModSettings.EcrSynergy.Value ? "ON" : "OFF")} ({Source()})";
            if (!EcrPresence.Present)
            {
                call.Reply($"Elite Creatures Reborn is not installed on this machine ({EcrPresence.Guid}); {synergy}, no effect without it.");
                return;
            }
            call.Reply($"Elite Creatures Reborn {EcrPresence.Version} found ({EcrPresence.Guid}); {synergy}");
            call.Detail(EcrWatch.TierSeen
                ? "world tier: recorded (ecr_tier seen on a creature this session)"
                : "world tier: not recorded by this ECR version (the tier terms stay inert, DECISIONS ECR-6)");
            Character? hovered = Player.m_localPlayer != null ? Player.m_localPlayer.GetHoverCreature() : null;
            if (hovered != null)
            {
                foreach (string line in EcrPreview.Explain(hovered).Split('\n'))
                {
                    call.Detail(line);
                }
            }
            call.Detail(EcrWatch.Summary());
        }

        // Whose value the switch is: the server's while it binds this player, else this machine's .cfg.
        private static string Source() => CommandAccess.IsAuthor ? "this machine's .cfg" : "server";
    }
}
