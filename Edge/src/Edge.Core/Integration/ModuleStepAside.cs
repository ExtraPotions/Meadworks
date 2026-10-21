// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module step-aside conditioned on each source mod's own switch (design 10.1, Shepherd Q1 rule).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Edge.Conversion;

namespace ExtraPotions.Edge.Integration
{
    /// <summary>The parts of Edge that step aside on their own (design 1.2 and 10.1).</summary>
    public enum EdgeModule { Secondaries, Quickstep, Slope, SwimReequip, LastShield, Archery }

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

        /// <summary>SecondaryAttacks <c>[1 - General] Quickstep Enabled</c> is On (its default).</summary>
        public bool? SaQuickstep { get; set; }

        /// <summary>SecondaryAttacks <c>[1 - General] Auto Equip Last Shield</c> is On (its default).</summary>
        public bool? SaLastShield { get; set; }

        /// <summary>KeepWeaponsAfterSwim <c>[General] Mode</c> is not Off (default AutoRestore).</summary>
        public bool? KwasActive { get; set; }

        public static SourceSwitches Read(string? secondaryAttacksConfig, string? keepWeaponsConfig)
        {
            var s = new SourceSwitches();
            if (secondaryAttacksConfig != null)
            {
                var cfg = SourceConfig.Parse(secondaryAttacksConfig);
                bool On(string key)
                {
                    var entry = cfg.FirstOrDefault(e => e.Section == "1 - General" && e.Key == key);
                    return entry.Key == null || !string.Equals(entry.Value, "Off", StringComparison.OrdinalIgnoreCase);
                }
                s.SaQuickstep = On("Quickstep Enabled");
                s.SaLastShield = On("Auto Equip Last Shield");
            }
            if (keepWeaponsConfig != null)
            {
                var mode = SourceConfig.Parse(keepWeaponsConfig).FirstOrDefault(e => e.Section == "General" && e.Key == "Mode");
                s.KwasActive = mode.Key == null || !string.Equals(mode.Value, "Off", StringComparison.OrdinalIgnoreCase);
            }
            return s;
        }
    }

    /// <summary>One step-aside notice per module: localization key and the installed sources for <c>{0}</c>.</summary>
    public readonly struct StepAsideNotice
    {
        public StepAsideNotice(EdgeModule module, string sourceNames)
        {
            Module = module;
            SourceNames = sourceNames;
        }

        public EdgeModule Module { get; }
        public string SourceNames { get; }
        public string Key => KeyOf(Module);

        public static string KeyOf(EdgeModule module) => "step_aside_" + module.ToString().ToLowerInvariant();
    }

    public sealed class StepAsideDecision
    {
        private readonly Dictionary<EdgeModule, List<ModuleSource>> aside;

        internal StepAsideDecision(Dictionary<EdgeModule, List<ModuleSource>> aside)
        {
            this.aside = aside;
            Notices = aside.OrderBy(p => p.Key).Select(p => new StepAsideNotice(p.Key, string.Join(" and ", p.Value.Select(s => s.Name)))).ToList();
        }

        public bool Any => aside.Count > 0;

        public bool SteppedAside(EdgeModule module) => aside.ContainsKey(module);

        public IReadOnlyList<ModuleSource> SourcesOf(EdgeModule module) =>
            aside.TryGetValue(module, out var list) ? list : (IReadOnlyList<ModuleSource>)Array.Empty<ModuleSource>();

        public IReadOnlyList<StepAsideNotice> Notices { get; }
    }

    public static class ModuleStepAside
    {
        public static readonly ModuleSource SecondaryAttacks = new ModuleSource("sighsorry.SecondaryAttacks", "SecondaryAttacks");
        public static readonly ModuleSource SlopeCombatAssistance = new ModuleSource("digitalroot.mods.slopecombatassistance", "Slope Combat Assistance");
        public static readonly ModuleSource SlopeCombatFix = new ModuleSource("digitalroot.mods.slopecombatfix", "Slope Combat Fix");
        public static readonly ModuleSource KeepWeaponsAfterSwim = new ModuleSource("lazly.valheim.keepweaponsafterswim", "KeepWeaponsAfterSwim");
        public static readonly ModuleSource Quickstep = new ModuleSource("shudnal.Quickstep", "Quickstep");
        public static readonly ModuleSource ShieldMeBruh = new ModuleSource("vapok.mods.shieldmebruh", "ShieldMeBruh");

        /// <summary>
        /// BetterArchery (ishid4). Its source is not public; the GUID is inferred from the config file name its Thunderstore page
        /// names (<c>ishid4.mods.betterarchery.cfg</c>; BepInEx names a plugin's config file after its GUID). Confirm in game.
        /// </summary>
        public static readonly ModuleSource BetterArchery = new ModuleSource("ishid4.mods.betterarchery", "BetterArchery");

        public static IReadOnlyList<ModuleSource> Sources { get; } =
            Array.AsReadOnly(new[] { SecondaryAttacks, SlopeCombatAssistance, SlopeCombatFix, KeepWeaponsAfterSwim, Quickstep, ShieldMeBruh, BetterArchery });

        /// <summary>Per module, the installed sources whose own switch says they act on it. GUIDs match exactly.</summary>
        public static StepAsideDecision Decide(IEnumerable<string> installedGuids, SourceSwitches switches)
        {
            if (installedGuids == null) throw new ArgumentNullException(nameof(installedGuids));
            var s = switches ?? SourceSwitches.Unknown;
            var installed = new HashSet<string>(installedGuids.Where(g => g != null), StringComparer.Ordinal);
            var aside = new Dictionary<EdgeModule, List<ModuleSource>>();
            void Add(ModuleSource source, EdgeModule module, bool acts)
            {
                if (!acts || !installed.Contains(source.Guid)) return;
                if (!aside.TryGetValue(module, out var list)) aside[module] = list = new List<ModuleSource>();
                list.Add(source);
            }
            Add(SecondaryAttacks, EdgeModule.Secondaries, true);
            Add(SecondaryAttacks, EdgeModule.Quickstep, s.SaQuickstep != false);
            Add(SecondaryAttacks, EdgeModule.LastShield, s.SaLastShield != false);
            Add(SlopeCombatAssistance, EdgeModule.Slope, true);
            Add(SlopeCombatFix, EdgeModule.Slope, true);
            Add(KeepWeaponsAfterSwim, EdgeModule.SwimReequip, s.KwasActive != false);
            Add(Quickstep, EdgeModule.Quickstep, true);
            Add(ShieldMeBruh, EdgeModule.LastShield, true);
            Add(BetterArchery, EdgeModule.Archery, true);
            return new StepAsideDecision(aside);
        }
    }
}
