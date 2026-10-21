// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Coexistence;

namespace ExtraPotions.Almanac.Runtime
{
    /// <summary>
    /// Reads installed managers' toggle shortcuts (same process, read only; never patches, disables or edits them) and
    /// applies the portable <see cref="HotkeyPolicy"/>. No UI: the one-time notice is logged.
    /// </summary>
    internal static class HotkeyCoexistence
    {
        public static List<ManagerFacts> InstalledManagers()
        {
            var found = new List<ManagerFacts>();
            foreach (var manager in KnownManagers.All)
            {
                if (!Chainloader.PluginInfos.TryGetValue(manager.Guid, out var info)) continue;
                string? chord = null;
                try
                {
                    var config = info.Instance != null ? info.Instance.Config : null;
                    var definition = new ConfigDefinition(manager.ToggleSection, manager.ToggleKey);
                    if (config != null && config.ContainsKey(definition) && config[definition].BoxedValue is KeyboardShortcut shortcut)
                        chord = shortcut.Serialize();
                }
                catch (Exception)
                {
                    chord = null; // Unreadable toggle: the policy assumes the manager's default F1.
                }
                found.Add(new ManagerFacts(manager.Guid, info.Metadata?.Name ?? manager.Name, chord));
            }
            return found;
        }

        public static HotkeyDecision Decide(ConfigEntry<KeyboardShortcut> toggle, ConfigEntry<CoexistenceMode> mode)
            => HotkeyPolicy.Decide(toggle.Value.Serialize(), mode.Value, InstalledManagers());
    }
}
