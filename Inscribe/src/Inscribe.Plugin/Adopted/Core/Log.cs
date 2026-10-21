// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using BepInEx.Logging;

namespace ExtraPotions.Inscribe.Adopted.Core
{
    /// <summary>Thin static wrapper over the plugin's logger so any class can log without holding a reference.</summary>
    public static class Log
    {
        private static ManualLogSource? _log;

        public static void Bind(ManualLogSource log) => _log = log;

        public static void Info(string message) => _log?.LogInfo(message);

        public static void Warn(string message) => _log?.LogWarning(message);

        public static void Error(string message) => _log?.LogError(message);

        public static void Debug(string message) => _log?.LogDebug(message);
    }
}
