// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module step-aside (design section 9, C09); same model as Thing.Core and Tempest.Core (expDARE).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Beacon.Integration
{
    /// <summary>
    /// The part of Beacon that steps aside on its own: Join, every patch of the game's join flow (favorites, stored password,
    /// save offer, auto-join, reconnect). The panel and the converter always stay so the player can import.
    /// </summary>
    public enum BeaconModule { Join }

    public readonly struct InstalledPlugin
    {
        public InstalledPlugin(string guid, string name)
        {
            Guid = guid ?? "";
            Name = name ?? "";
        }

        public string Guid { get; }
        public string Name { get; }
    }

    public sealed class ModuleSource
    {
        internal ModuleSource(string guid, string name, BeaconModule module, params string[] pluginNames)
        {
            Guid = guid;
            Name = name;
            Module = module;
            PluginNames = pluginNames;
        }

        public string Guid { get; }
        public string Name { get; }
        public BeaconModule Module { get; }
        public IReadOnlyList<string> PluginNames { get; }

        public bool Matches(InstalledPlugin plugin) => string.Equals(plugin.Guid, Guid, StringComparison.Ordinal)
            || PluginNames.Any(n => string.Equals(n, plugin.Name, StringComparison.OrdinalIgnoreCase));
    }

    public readonly struct StepAsideNotice
    {
        public StepAsideNotice(BeaconModule module, string sourceName, string sourceGuid)
        {
            Module = module;
            SourceName = sourceName;
            SourceGuid = sourceGuid;
        }

        public BeaconModule Module { get; }
        public string SourceName { get; }
        public string SourceGuid { get; }
        public string Key => KeyOf(Module);

        public static string KeyOf(BeaconModule module) => "step_aside_" + module.ToString().ToLowerInvariant();
    }

    public sealed class ModuleStepAsideDecision
    {
        readonly HashSet<BeaconModule> aside;

        internal ModuleStepAsideDecision(IReadOnlyList<ModuleSource> present)
        {
            Present = present;
            aside = new HashSet<BeaconModule>(present.Select(p => p.Module));
            Notices = present.OrderBy(p => p.Module).Select(p => new StepAsideNotice(p.Module, p.Name, p.Guid)).ToList();
        }

        public IReadOnlyList<ModuleSource> Present { get; }
        public bool Any => aside.Count > 0;
        public bool SteppedAside(BeaconModule module) => aside.Contains(module);
        public IReadOnlyList<StepAsideNotice> Notices { get; }
    }

    public static class ModuleStepAside
    {
        /// <summary>AutoConnector 1.0.0 (GUID from its config header).</summary>
        public static readonly ModuleSource AutoConnector = new ModuleSource("MidnightsFX.Autoconnect", "AutoConnector", BeaconModule.Join, "AutoConnector");

        public static IReadOnlyList<ModuleSource> Sources { get; } = Array.AsReadOnly(new[] { AutoConnector });

        public static ModuleStepAsideDecision Decide(IEnumerable<InstalledPlugin> installed)
        {
            if (installed == null) throw new ArgumentNullException(nameof(installed));
            var list = installed.ToList();
            return new ModuleStepAsideDecision(Sources.Where(s => list.Any(s.Matches)).ToList());
        }
    }
}
