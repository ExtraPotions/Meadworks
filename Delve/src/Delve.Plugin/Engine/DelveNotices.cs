// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: notice delivery (design D5, D6): the game's own ShowMessage route through Crucible's
// MeadworksServerNotices, so vanilla clients see them; text in the server language (Notices / Server Language), server
// overrides applied; nothing is sent while Notices / Notify is off.
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Localization;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Delve.Runtime
{
    public static class DelveNotices
    {
        public static bool On => Plugin.Settings != null && Plugin.Settings.Get(DelveSettings.Notices.Notify);

        /// <summary>"1 day", "6 days", "1 hour", "12 hours" in the server language.</summary>
        public static string Duration(ClockKind clock, int units)
        {
            var (key, count) = NoticeText.Duration(clock, units);
            return count == 1 && (key == DelveTextKeys.DaysOne || key == DelveTextKeys.HoursOne) ? DelveText.ServerText(key) : DelveText.ServerFormat(key, count);
        }

        public static bool Tell(DelvePlayer player, string text, ServerNoticePlacement placement = ServerNoticePlacement.Center) =>
            On && Tell(player.Uid, text, placement);

        /// <summary>Sends without the Notify switch (command replies).</summary>
        public static bool Tell(long peer, string text, ServerNoticePlacement placement) =>
            !string.IsNullOrEmpty(text) && MeadworksServerNotices.TrySend(Plugin.Guid, peer, text, placement);
    }
}
