// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: where a Tempest center notice goes (batch 4B: no overlap with player windows).
namespace ExtraPotions.Tempest.Integration
{
    /// <summary>Where a center notice is delivered.</summary>
    public enum NoticeRoute
    {
        /// <summary>No game UI (headless or not loaded): the plugin log.</summary>
        Log,
        /// <summary>A player window is open: the message log only, so nothing is drawn over the window.</summary>
        Journal,
        /// <summary>The Crucible.UI kit notice service (MeadWorks presentation).</summary>
        Kit,
        /// <summary>The game's own center message, when Crucible.UI is unavailable.</summary>
        Native,
    }

    public static class NoticeRoutes
    {
        public static NoticeRoute For(bool gameUi, bool kitActive, bool playerWindowOpen)
        {
            if (!gameUi) return NoticeRoute.Log;
            if (playerWindowOpen) return NoticeRoute.Journal;
            return kitActive ? NoticeRoute.Kit : NoticeRoute.Native;
        }
    }
}
