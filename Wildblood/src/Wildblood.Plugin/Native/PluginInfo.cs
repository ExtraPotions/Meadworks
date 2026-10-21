// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted
{
    /// <summary>Identity constants for the plugin. One place, so the GUID is never retyped.</summary>
    internal static class PluginInfo
    {
        // The GUID is the plugin's permanent identity (config file name, log lines, dependency handle). Fixed by the
        // spec's Identity section and must never change again - altering it orphans everyone's settings.
        public const string Guid = ExtraPotions.Wildblood.Runtime.Plugin.Guid;
        public const string Name = ExtraPotions.Wildblood.Runtime.Plugin.Name;
        public const string PluginVersion = ExtraPotions.Wildblood.Runtime.Plugin.Version;
        public const string Author = "expDARE";
        public const string Licence = "GNU General Public License v3.0";
    }
}
