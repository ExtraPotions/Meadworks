// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module step-aside conditioned on each source mod's own switch (design 9.1, Shepherd Q1 rule).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Cairn.Conversion;

namespace ExtraPotions.Cairn.Integration
{
    /// <summary>The parts of Cairn that step aside on their own (design 9.1). The Exploration skill never steps aside.</summary>
    public enum CairnModule { Radius, Visibility, Hud }

    /// <summary>A source mod by BepInEx plugin GUID.</summary>
    public sealed class ModuleSource
    {
        internal ModuleSource(string guid, string name)
        {
            Guid = guid;
            Name = name;
        }

        public string Guid { get; }
        public string Name { get; }
    }

    /// <summary>
    /// The source mods' own switches, read from their config files. Null means unknown (file missing or unreadable): the
    /// module then steps aside, the safe choice when two engines would both act.
    /// </summary>
    public sealed class SourceSwitches
    {
        public static SourceSwitches Unknown => new SourceSwitches();

        /// <summary>TimeWeatherHud <c>[General] Enabled</c> is true (its default).</summary>
        public bool? TimeWeatherHudEnabled { get; set; }

        public static SourceSwitches Read(string? timeWeatherHudConfig)
        {
            var s = new SourceSwitches();
            if (timeWeatherHudConfig != null)
            {
                var entry = SourceConfig.Parse(timeWeatherHudConfig).FirstOrDefault(e => e.Section == "General" && e.Key == "Enabled");
                s.TimeWeatherHudEnabled = entry.Key == null || !string.Equals(entry.Value, "false", StringComparison.OrdinalIgnoreCase);
            }
            return s;
        }
    }

    /// <summary>One step-aside notice per module: localization key and the installed sources for <c>{0}</c>.</summary>
    public readonly struct StepAsideNotice
    {
        public StepAsideNotice(CairnModule module, string sourceNames)
        {
            Module = module;
            SourceNames = sourceNames;
        }

        public CairnModule Module { get; }
        public string SourceNames { get; }
        public string Key => KeyOf(Module);

        public static string KeyOf(CairnModule module) => "step_aside_" + module.ToString().ToLowerInvariant();
    }

    public sealed class StepAsideDecision
    {
        private readonly Dictionary<CairnModule, List<ModuleSource>> aside;

        internal StepAsideDecision(Dictionary<CairnModule, List<ModuleSource>> aside)
        {
            this.aside = aside;
            Notices = aside.OrderBy(p => p.Key).Select(p => new StepAsideNotice(p.Key, string.Join(" and ", p.Value.Select(s => s.Name)))).ToList();
        }

        public bool Any => aside.Count > 0;

        public bool SteppedAside(CairnModule module) => aside.ContainsKey(module);

        public IReadOnlyList<ModuleSource> SourcesOf(CairnModule module) =>
            aside.TryGetValue(module, out var list) ? list : (IReadOnlyList<ModuleSource>)Array.Empty<ModuleSource>();

        public IReadOnlyList<StepAsideNotice> Notices { get; }
    }

    public static class ModuleStepAside
    {
        public static readonly ModuleSource Pathfinder = new ModuleSource("dev.crystal.pathfinder", "Pathfinder");
        public static readonly ModuleSource NoFogBruh = new ModuleSource("vapok.mods.nofogbruh", "NoFogBruh");
        public static readonly ModuleSource BetterFog = new ModuleSource("JevMods.BetterFog", "BetterFog");
        public static readonly ModuleSource TimeWeatherHud = new ModuleSource("lazly.valheim.timeweatherhud", "TimeWeatherHud");

        public static IReadOnlyList<ModuleSource> Sources { get; } = Array.AsReadOnly(new[] { Pathfinder, NoFogBruh, BetterFog, TimeWeatherHud });

        /// <summary>Per module, the installed sources whose own switch says they act on it. GUIDs match exactly.</summary>
        public static StepAsideDecision Decide(IEnumerable<string> installedGuids, SourceSwitches switches)
        {
            if (installedGuids == null) throw new ArgumentNullException(nameof(installedGuids));
            var s = switches ?? SourceSwitches.Unknown;
            var installed = new HashSet<string>(installedGuids.Where(g => g != null), StringComparer.Ordinal);
            var aside = new Dictionary<CairnModule, List<ModuleSource>>();
            void Add(ModuleSource source, CairnModule module, bool acts)
            {
                if (!acts || !installed.Contains(source.Guid)) return;
                if (!aside.TryGetValue(module, out var list)) aside[module] = list = new List<ModuleSource>();
                list.Add(source);
            }
            Add(Pathfinder, CairnModule.Radius, true);
            Add(NoFogBruh, CairnModule.Visibility, true);
            Add(BetterFog, CairnModule.Visibility, true);
            Add(TimeWeatherHud, CairnModule.Hud, s.TimeWeatherHudEnabled != false);
            return new StepAsideDecision(aside);
        }
    }
}
