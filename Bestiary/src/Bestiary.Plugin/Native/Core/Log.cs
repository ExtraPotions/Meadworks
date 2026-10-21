// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Logging;

namespace ExtraPotions.Bestiary.Runtime.Native.Core
{
    /// <summary>Thin static wrapper over the plugin's logger so any class can log without holding a reference.</summary>
    internal static class Log
    {
        private static ManualLogSource? _log;

        public static void Bind(ManualLogSource log) => _log = log;

        public static void Info(string message) => _log?.LogInfo(message);

        public static void Warn(string message) => _log?.LogWarning(message);

        public static void Error(string message) => _log?.LogError(message);
    }
}
