// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Display;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;
using ExtraPotions.Meadworks.Crucible.UI;
using System;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// A gem onto an item whose sockets are all full (sockets.md section 4, user decision 2026-10-07): the player picks
    /// the socket whose gem it replaces. The game's own yes/no popup asks once per filled socket, in order ("Socket 2 holds
    /// Thor's Gem: +6 lightning damage. Replace it?"); Yes replaces that one (the old gem comes back by the Gem Recovery chance), No asks about the next,
    /// No on the last keeps everything. Yes re-runs every check first, aimed at the chosen socket. Local player only.
    /// </summary>
    internal static class GemChooser
    {
        public static void Ask(StoneJob job)
        {
            if (!UnifiedPopup.IsAvailable() || job.State.Gems.Count == 0)
            {
                StoneFeedback.Show(job.Player, StoneResult.Refuse("sockets_full").Refusal!);
                return;
            }
            AskAt(job, 0);
        }

        private static void AskAt(StoneJob job, int index)
        {
            if (index >= job.State.Gems.Count)
            {
                return;
            }
            int socket = job.State.GemSocketAt(index);
            Push(job, index, () => Chosen(job, socket), () => Next(job, index));
        }

        /// <summary>
        /// A gem aimed at a socket the player picked (the Rune Table's socket row): asks once whether to replace the gem
        /// there, then sets it. A socket whose entry is unreadable is replaced without asking (there is nothing to name).
        /// </summary>
        public static void Confirm(StoneJob job, int socket)
        {
            int index = -1;
            for (int i = 0; i < job.State.Gems.Count; i++)
            {
                index = job.State.GemSocketAt(i) == socket ? i : index;
            }
            if (index < 0 || !UnifiedPopup.IsAvailable())
            {
                Set(job, socket);
                return;
            }
            Push(job, index, () => Chosen(job, socket), null);
        }

        private static void Push(StoneJob job, int index, Action yes, Action? no)
        {
            string socket = (job.State.GemSocketAt(index) + 1).ToString();
            string title = Words.Localize("$ecf_ui_gem_replace_title", job.StoneName);
            string body = Words.Localize("$ecf_ui_gem_replace_body", socket, Holding(job.State, index), job.StoneName)
                + "\n" + Runtime.GemRecoveryRuntime.ChanceLine(job.State.RarityId);
            var source = Runtime.Plugin.Source;
            if (source != null && source.IsActive && CrucibleUiPlugin.Active)
            {
                if (KitDialogs.Confirm(source, title, body, yes, no, localizeText: false) != null) return;
            }
            bool answered = false;
            UnifiedPopup.Push(new YesNoPopup(title, body, () =>
            {
                if (answered) return;
                answered = true; UnifiedPopup.Pop(); yes();
            }, () =>
            {
                if (answered) return;
                answered = true; UnifiedPopup.Pop(); no?.Invoke();
            }, localizeText: false));
        }

        /// <summary>"Thor's Gem: +6 lightning damage" for gem <paramref name="index"/>.</summary>
        private static string Holding(ItemState state, int index)
        {
            GemRoll gem = state.Gems[index];
            AffixDef? def = state.GemDefinitionAt(index);
            StoneDef? stone = ActiveRules.Current.Economy.Stone(gem.GemId);
            string name = Words.Localize(stone?.Name ?? "$ecf_stone_" + gem.GemId);
            return name + ": " + AffixLines.Sentence(gem.Roll.Id, gem.Roll.Value, def);
        }

        private static void Next(StoneJob job, int index)
        {
            if (Player.m_localPlayer != job.Player) return;
            AskAt(job, index + 1);
        }

        private static void Chosen(StoneJob asked, int socket)
        {
            Set(asked, socket);
        }

        // Every check again (the inventory may have changed while the popup was open), aimed at the socket, then the commit.
        private static void Set(StoneJob asked, int socket)
        {
            Player player = Player.m_localPlayer;
            if (player == null || player != asked.Player)
            {
                return;
            }
            StoneJob job = asked.AtSocket(player, socket);
            StoneResult result = StonePipeline.Evaluate(job);
            if (result.Refused)
            {
                StoneFeedback.Show(player, result.Refusal!);
                return;
            }
            StoneCommit.Commit(job, result);
        }
    }
}
