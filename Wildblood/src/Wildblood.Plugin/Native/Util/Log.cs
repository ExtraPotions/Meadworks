// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; diagnostics follow the live logging level.
using BepInEx.Logging;

namespace ExtraPotions.Wildblood.Adopted.Util
{
    /// <summary>Thin static wrapper over the plugin's logger so any class can log without holding a reference.</summary>
    internal static class Log
    {
        private static ManualLogSource? _log;

        /// <summary>
        /// When true, the step-by-step diagnostics (<see cref="Diag"/>) are emitted. Off by default so a live server is
        /// quiet; a server owner turns it on from the .cfg to make a mutation that "silently does nothing" - the class of
        /// bug that hid the Splintering ZDO-reset fault - visible at every step instead. Read live from Crucible's logging level.
        /// </summary>
        public static bool Diagnostics
        {
            get => ExtraPotions.Meadworks.Crucible.MeadworksLogging.Enabled(ExtraPotions.Wildblood.Runtime.Plugin.Guid, ExtraPotions.Meadworks.Crucible.LogSeverity.Verbose);
            set { }
        }

        public static void Bind(ManualLogSource log) => _log = log;

        public static void Info(string message) => _log?.LogInfo(message);

        public static void Warn(string message) => _log?.LogWarning(message);

        public static void Error(string message) => _log?.LogError(message);

        /// <summary>A permanent diagnostic line, printed only while <see cref="Diagnostics"/> is on. Never removed.</summary>
        public static void Diag(string message)
        {
            if (Diagnostics)
            {
                _log?.LogInfo("[diag] " + message);
            }
        }
    }
}
