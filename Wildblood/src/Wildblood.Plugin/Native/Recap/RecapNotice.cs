// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.AdoptedLibs.Hotkeys;

namespace ExtraPotions.Wildblood.Adopted.Recap
{
    /// <summary>
    /// One line at the top left once a recap is made: who killed the player and how to watch it. The game's own top-left
    /// message, so it looks like the game's and fades like it; nothing waits for it.
    /// </summary>
    internal static class RecapNotice
    {
        private static int killedVariant, diedVariant;
        public static void Show(DeathRecap recap)
        {
            if (!RecapSettings.Notice.Value || MessageHud.instance == null)
            {
                return;
            }
            int length = System.Math.Min(recap.Killer.Length, 256);
            if (length < recap.Killer.Length && length > 0 && char.IsHighSurrogate(recap.Killer[length - 1])
                && char.IsLowSurrogate(recap.Killer[length])) length--;
            string name = recap.Killer.Substring(0, length);
            name = name.Replace("<", "‹").Replace(">", "›");
            int variant = recap.Dealt ? killedVariant : diedVariant;
            string notice = recap.Dealt ? "recap_killed" : "recap_died";
            if (variant != 0) notice += "_" + (variant + 1);
            string what = ExtraPotions.Wildblood.Runtime.WildbloodText.Format(notice, name);
            string key = OpenWith();
            ExtraPotions.Wildblood.Runtime.WildbloodText.TopLeft(key.Length == 0 ? what
                : ExtraPotions.Wildblood.Runtime.WildbloodText.Format("recap_notice", what, key));
            if (recap.Dealt) killedVariant = (variant + 1) % 3;
            else diedVariant = (variant + 1) % 3;
        }

        public static string OpenWith()
        {
            string key = KeyNames.Short(RecapSettings.Key.Value);
            return key;
        }
    }
}
