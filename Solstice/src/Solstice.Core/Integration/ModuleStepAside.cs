// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: step-aside, same model as Tempest and Keel.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Solstice.Integration
{
    /// <summary>The parts of Solstice that step aside on their own. Solstice is one feature, so there is one module.</summary>
    public enum SolsticeModule { Seasons }

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

    /// <summary>A source mod by BepInEx plugin GUID and the module it covers.</summary>
    public sealed class ModuleSource
    {
        internal ModuleSource(string guid, string name, SolsticeModule module)
        {
            Guid = guid;
            Name = name;
            Module = module;
        }

        public string Guid { get; }
        public string Name { get; }
        public SolsticeModule Module { get; }

        public bool Matches(InstalledPlugin plugin) => string.Equals(plugin.Guid, Guid, StringComparison.Ordinal);
    }

    /// <summary>One step-aside notice: the localisation key (one per module) and the source mod's name for <c>{0}</c>.</summary>
    public readonly struct StepAsideNotice
    {
        public StepAsideNotice(SolsticeModule module, string sourceName, string sourceGuid)
        {
            Module = module;
            SourceName = sourceName;
            SourceGuid = sourceGuid;
        }

        public SolsticeModule Module { get; }
        public string SourceName { get; }
        public string SourceGuid { get; }
        public string Key => KeyOf(Module);

        public static string KeyOf(SolsticeModule module) => "step_aside_" + module.ToString().ToLowerInvariant();
    }

    public sealed class ModuleStepAsideDecision
    {
        readonly HashSet<SolsticeModule> aside;

        internal ModuleStepAsideDecision(IReadOnlyList<ModuleSource> present)
        {
            Present = present;
            aside = new HashSet<SolsticeModule>(present.Select(p => p.Module));
            Notices = present.OrderBy(p => p.Module).Select(p => new StepAsideNotice(p.Module, p.Name, p.Guid)).ToList();
        }

        public IReadOnlyList<ModuleSource> Present { get; }

        public bool Any => aside.Count > 0;

        public bool SteppedAside(SolsticeModule module) => aside.Contains(module);

        public IReadOnlyList<StepAsideNotice> Notices { get; }
    }

    public static class ModuleStepAside
    {
        /// <summary>shudnal's Seasons (the pinned reference): it changes the same game systems, so only one of them may run.</summary>
        public static readonly ModuleSource Seasons = new ModuleSource("shudnal.Seasons", "Seasons", SolsticeModule.Seasons);

        /// <summary>RustyMods' Seasonality: the reference declares it incompatible with Seasons for the same reason.</summary>
        public static readonly ModuleSource Seasonality = new ModuleSource("RustyMods.Seasonality", "Seasonality", SolsticeModule.Seasons);

        public static IReadOnlyList<ModuleSource> Sources { get; } = Array.AsReadOnly(new[] { Seasons, Seasonality });

        public static ModuleStepAsideDecision Decide(IEnumerable<InstalledPlugin> installed)
        {
            if (installed == null) throw new ArgumentNullException(nameof(installed));
            var list = installed.ToList();
            return new ModuleStepAsideDecision(Sources.Where(s => list.Any(s.Matches)).ToList());
        }
    }
}
