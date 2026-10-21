// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: picking the one inscription a Honing Rune acts on (batch 3), as GemChooser picks a socket.
using System;
using ExtraPotions.Inscribe.Adopted.Text;
using ExtraPotions.Inscribe.Runtime;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// The established yes/no dialog asks once per inscription the rune can change, in item order ("Thor's Chain: +15%
    /// chain lightning T3. Use the Honing Rune on this inscription?"); Yes re-runs every check aimed at that one and commits,
    /// No asks about the next, No on the last changes nothing. Local player only.
    /// </summary>
    internal static class InscriptionChooser
    {
        public static void Ask(StoneJob job)
        {
            if (!(StoneVerbs.For(job.Def) is TargetedVerb verb) || !UnifiedPopup.IsAvailable())
            {
                StoneFeedback.Show(job.Player, StoneResult.Refuse("no_eligible_affix").Refusal!);
                return;
            }
            var candidates = verb.Candidates(job);
            AskAt(job, candidates.ToArray(), 0);
        }

        private static void AskAt(StoneJob job, int[] candidates, int at)
        {
            if (at >= candidates.Length || Player.m_localPlayer != job.Player)
            {
                return;
            }
            int index = candidates[at];
            var def = job.State.DefinitionAt(index)!;
            string title = InscribeText.Format("pick_inscription_title", job.StoneName);
            string body = InscribeText.Format("pick_inscription_body", TargetedVerb.Line(def, job.State.Affixes[index]), job.StoneName);
            Action yes = () => Use(job, index);
            Action no = () => AskAt(job, candidates, at + 1);
            var source = Plugin.Source;
            if (source != null && source.IsActive && CrucibleUiPlugin.Active
                && KitDialogs.Confirm(source, title, body, yes, no, localizeText: false) != null)
            {
                return;
            }
            bool answered = false;
            UnifiedPopup.Push(new YesNoPopup(title, body,
                () => { if (answered) return; answered = true; UnifiedPopup.Pop(); yes(); },
                () => { if (answered) return; answered = true; UnifiedPopup.Pop(); no(); }));
        }

        // Every check again (the inventory may have changed while the dialog was open), aimed at the inscription, then the commit.
        private static void Use(StoneJob asked, int index)
        {
            Player player = Player.m_localPlayer;
            if (player == null || player != asked.Player)
            {
                return;
            }
            StoneJob job = asked.AtAffix(player, index);
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
