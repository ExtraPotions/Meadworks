// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// <c>inscribe reload</c> (console-commands.md section 3, configuration.md section 5): re-reads both YAML families,
    /// the translation files and the <c>.cfg</c> now instead of on the next poll. Admin, author side only (checked by
    /// <see cref="CommandAccess"/>): single player, the host, a dedicated server's console, or a player the server does
    /// not bind. On a server a successful family build is re-published to every connected player by the rules code.
    /// Whether a family applied comes from <see cref="ActiveRules.ReloadLocal"/>'s per-family result.
    /// </summary>
    internal static class ReloadCommand
    {
        public static void Run(CommandCall call)
        {
            (FamilyReload affixes, FamilyReload economy) = ActiveRules.ReloadLocal();
            Words.InstallNow();
            Runtime.NativeGameplay.ReloadConfig();
            RuleSet now = ActiveRules.Current;
            call.Reply($"rules generation {now.Generation}");
            call.Detail("inscriptions: " + Describe(affixes, $"reloaded, {now.Affixes.Affixes.Count} inscriptions"));
            call.Detail("economy: " + Describe(economy, $"reloaded, {now.Economy.Rarities.Count} rarities, {now.Economy.Stones.Count} runes"));
            call.Detail("translations and com.meadworks.inscribe.cfg: re-read");
        }

        private static string Describe(FamilyReload result, string applied)
        {
            switch (result)
            {
                case FamilyReload.Applied: return applied;
                case FamilyReload.Bound: return "the server's rules stay in force";
                case FamilyReload.NoFiles: return "no files found, the loaded rules stay";
                default: return "errors, previous configuration kept, see log";
            }
        }
    }
}
