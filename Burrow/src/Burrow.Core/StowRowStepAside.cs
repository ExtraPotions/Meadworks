// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the stow button row's per-module step-aside decision (C09), kept free of the game for tests.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Burrow
{
    /// <summary>A third-party mod whose own stack/store/sort buttons and hotkeys Burrow's row would double.</summary>
    public sealed class StowRowSource
    {
        public string Guid { get; }
        public string Name { get; }
        public StowRowSource(string guid, string name) { Guid = guid; Name = name; }
    }

    /// <summary>
    /// The stow button row (Quick stack, Store all, Top up, Sort) and its hotkeys step aside while one of these mods is
    /// loaded; every other Burrow feature keeps running. GUIDs are read from the packages' plugin attributes as text:
    /// Quick Stack Store Sort Trash Restock 1.4.15 (QuickStackStore.dll) and the soft dependencies ExtraSlots 1.2.17
    /// declares for AzuAutoStore and Simple Sort (assets/compat).
    /// </summary>
    public static class StowRowStepAside
    {
        public const string ModuleId = "stow_buttons";
        public const string TextKey = "step_aside_" + ModuleId;

        public static IReadOnlyList<StowRowSource> Sources { get; } = Array.AsReadOnly(new[]
        {
            new StowRowSource("goldenrevolver.quick_stack_store", "Quick Stack Store Sort Trash Restock"),
            new StowRowSource("Azumatt.AzuAutoStore", "AzuAutoStore"),
            new StowRowSource("aedenthorn.SimpleSort", "Simple Sort"),
        });

        /// <summary>
        /// The first declared source among the loaded plugin GUIDs (ordinal, as BepInEx keys them), or null when the row
        /// stays.
        /// </summary>
        public static StowRowSource? Match(IEnumerable<string> loadedGuids)
        {
            if (loadedGuids == null) throw new ArgumentNullException(nameof(loadedGuids));
            var loaded = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in loadedGuids)
                if (!string.IsNullOrEmpty(guid)) loaded.Add(guid);
            foreach (StowRowSource source in Sources)
                if (loaded.Contains(source.Guid)) return source;
            return null;
        }
    }
}
