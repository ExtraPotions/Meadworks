// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Text;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// Pipeline step 13 (applying-stones.md section 4): runes with <c>confirm: true</c> (synced, per rune: Cleansing and
    /// the Serpent Rune by default) ask first, in the way this player chose (<c>Confirm destructive runes</c>, local and
    /// unsynced). Last on purpose:
    /// only a use that would succeed is ever confirmed. Local player only.
    /// <list type="bullet">
    /// <item><c>HoldShift</c>: applies only while Shift / the gamepad left trigger is held - the grid reports that as
    /// <c>Modifier.Split</c>, which vanilla ignores while an item is carried.</item>
    /// <item><c>Dialog</c>: the game's yes/no popup; Yes re-runs steps 1-12 (the inventory may have changed while it was
    /// open) and commits that fresh result. Falls back to HoldShift when no popup host exists.</item>
    /// <item><c>Off</c>: no gate.</item>
    /// </list>
    /// </summary>
    internal static class ConfirmGate
    {
        public static void Pass(StoneJob job, StoneResult result, bool shiftHeld)
        {
            ConfirmMode mode = ModSettings.ConfirmDestructiveStones?.Value ?? ConfirmMode.HoldShift;
            if (mode == ConfirmMode.HoldShift && ZInput.IsGamepadActive()) mode = ConfirmMode.Dialog;
            bool asks = job.Def!.Confirm;
            if (!asks || mode == ConfirmMode.Off || (mode == ConfirmMode.HoldShift && shiftHeld))
            {
                StoneCommit.Commit(job, result);
                return;
            }
            if (mode == ConfirmMode.Dialog && UnifiedPopup.IsAvailable())
            {
                Ask(job);
                return;
            }
            if (shiftHeld)
            {
                StoneCommit.Commit(job, result);
                return;
            }
            StoneFeedback.Show(job.Player, StoneResult.Refuse("confirm_required", job.StoneName).Refusal!);
        }

        private static void Ask(StoneJob job)
        {
            string title = Words.Localize("$ecf_ui_confirm_title", job.StoneName);
            string body = Words.Localize("$ecf_ui_confirm_body", job.ItemName, job.StoneName);
            var source = Runtime.Plugin.Source;
            if (source != null && source.IsActive && CrucibleUiPlugin.Active)
            {
                if (KitDialogs.Confirm(source, title, body, () => Confirmed(job), localizeText: false) != null) return;
            }
            bool answered = false;
            UnifiedPopup.Push(new YesNoPopup(title, body, () =>
            {
                if (answered) return;
                answered = true; UnifiedPopup.Pop(); Confirmed(job);
            }, () => { if (answered) return; answered = true; UnifiedPopup.Pop(); }, localizeText: false));
        }

        private static void Confirmed(StoneJob asked)
        {
            Player player = Player.m_localPlayer;
            if (player == null || player != asked.Player)
            {
                return;
            }
            StoneJob job = asked.Again(player);
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
