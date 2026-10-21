// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module step-aside (design 9, C09, Shepherd Q1 rule).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Tempest.Integration
{
    /// <summary>
    /// The parts of Tempest that step aside on their own: Events (scheduler, environments, lightning, weather commands),
    /// Filter (the weather switches) and Override (the personal weather override).
    /// </summary>
    public enum TempestModule { Events, Filter, Override }

    /// <summary>An installed BepInEx plugin: GUID and display name.</summary>
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

    /// <summary>A source mod by BepInEx plugin GUID (and optional display names) and the module it covers.</summary>
    public sealed class ModuleSource
    {
        internal ModuleSource(string guid, string name, TempestModule module, params string[] pluginNames)
        {
            Guid = guid;
            Name = name;
            Module = module;
            PluginNames = pluginNames;
        }

        public string Guid { get; }
        public string Name { get; }
        public TempestModule Module { get; }

        /// <summary>Display names that also identify the source (used when its GUID is unconfirmed).</summary>
        public IReadOnlyList<string> PluginNames { get; }

        public string ModuleId => Module.ToString().ToLowerInvariant();

        public bool Matches(InstalledPlugin plugin) => string.Equals(plugin.Guid, Guid, StringComparison.Ordinal)
            || PluginNames.Any(n => string.Equals(n, plugin.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>One step-aside notice: the localization key (one per module) and the source mod's name for <c>{0}</c>.</summary>
    public readonly struct StepAsideNotice
    {
        public StepAsideNotice(TempestModule module, string sourceName, string sourceGuid)
        {
            Module = module;
            SourceName = sourceName;
            SourceGuid = sourceGuid;
        }

        public TempestModule Module { get; }
        public string SourceName { get; }
        public string SourceGuid { get; }
        public string Key => KeyOf(Module);

        public static string KeyOf(TempestModule module) => "step_aside_" + module.ToString().ToLowerInvariant();
    }

    public sealed class ModuleStepAsideDecision
    {
        private readonly HashSet<TempestModule> aside;

        internal ModuleStepAsideDecision(IReadOnlyList<ModuleSource> present)
        {
            Present = present;
            aside = new HashSet<TempestModule>(present.Select(p => p.Module));
            Notices = present.OrderBy(p => p.Module).Select(p => new StepAsideNotice(p.Module, p.Name, p.Guid)).ToList();
        }

        public IReadOnlyList<ModuleSource> Present { get; }

        public bool Any => aside.Count > 0;

        public bool SteppedAside(TempestModule module) => aside.Contains(module);

        public IReadOnlyList<StepAsideNotice> Notices { get; }
    }

    public static class ModuleStepAside
    {
        /// <summary>
        /// WildWeather 0.1.1: GUID inferred from its config file name <c>com.burks.wildweather.cfg</c> (unconfirmed, no copy
        /// inspected), so its plugin names also count (design 9).
        /// </summary>
        public static readonly ModuleSource WildWeather = new ModuleSource("com.burks.wildweather", "WildWeather", TempestModule.Events, "Wild Weather", "WildWeather");

        /// <summary>NoFogBruh's weather event switches act whenever it is installed (Cairn steps aside for its fog parts).</summary>
        public static readonly ModuleSource NoFogBruh = new ModuleSource("vapok.mods.nofogbruh", "NoFogBruh", TempestModule.Filter);

        /// <summary>Valheim_Admin_Tool's own local weather override (config file <c>com.icecub.valheimadmintool.cfg</c>).</summary>
        public static readonly ModuleSource AdminTool = new ModuleSource("com.icecub.valheimadmintool", "Valheim_Admin_Tool", TempestModule.Override);

        public static IReadOnlyList<ModuleSource> Sources { get; } = Array.AsReadOnly(new[] { WildWeather, NoFogBruh, AdminTool });

        public static ModuleStepAsideDecision Decide(IEnumerable<InstalledPlugin> installed)
        {
            if (installed == null) throw new ArgumentNullException(nameof(installed));
            var list = installed.ToList();
            return new ModuleStepAsideDecision(Sources.Where(s => list.Any(s.Matches)).ToList());
        }
    }
}
