// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module step-aside conditioned on each source mod's own switch (design 11.1, Shepherd Q1 rule).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Wildblood.Conversion;

namespace ExtraPotions.Wildblood.Integration
{
    /// <summary>Wildblood's feature modules (design 1.2). Display never steps aside.</summary>
    public enum WildbloodModule { Stars, Traits, Bosses, Loot, Luck, Pressure, Display, Recap, Horde, AI }

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
    /// The source mods' own switches, read from their files. Null means unknown (file missing or unreadable): the module
    /// then steps aside, the safe choice when two engines would both act.
    /// </summary>
    public sealed class SourceSwitches
    {
        public static SourceSwitches Unknown => new SourceSwitches();

        /// <summary>EliteCreaturesReborn <c>creature stars</c> (rule file; default true).</summary>
        public bool? EcrCreatureStars { get; set; }

        /// <summary>EliteCreaturesReborn <c>loot.mode: Vanilla</c>.</summary>
        public bool? EcrLootVanilla { get; set; }

        public bool? EcrHorde { get; set; }

        /// <summary>CreatureManager <c>Enable Level System = Off</c>.</summary>
        public bool? CmLevelSystemOff { get; set; }

        /// <summary>CreatureManager Global, Boss and Enforcer Modifiers all Off.</summary>
        public bool? CmAllModifiersOff { get; set; }

        /// <summary>CreatureManager <c>Karma System Mode = Off</c>.</summary>
        public bool? CmKarmaOff { get; set; }

        /// <summary>CreatureManager <c>Character Loot System = Vanilla</c>.</summary>
        public bool? CmLootVanilla { get; set; }
        public bool? CmTargetSwitchOff { get; set; }

        /// <summary>SaveYourLuckBruh <c>Enable SaveYourLuckBruh</c>.</summary>
        public bool? SylbEnabled { get; set; }

        /// <summary>Reads the switches from the source files' text; a null text leaves its switches unknown.</summary>
        public static SourceSwitches Read(string? ecrRules, string? cmConfig, string? sylbConfig, string? ecrConfig = null)
        {
            var s = new SourceSwitches();
            if (ecrConfig != null)
            {
                var horde = SourceConfig.Parse(ecrConfig).FirstOrDefault(e => e.Section == "13 - Horde" && e.Key == "Horde");
                s.EcrHorde = horde.Key != null && !string.Equals(horde.Value, "false", StringComparison.OrdinalIgnoreCase);
            }
            if (ecrRules != null)
            {
                var y = SimpleYaml.Read(ecrRules);
                s.EcrCreatureStars = !y.TryGetValue("creature stars", out var stars) || !string.Equals(stars, "false", StringComparison.OrdinalIgnoreCase);
                s.EcrLootVanilla = y.TryGetValue("loot.mode", out var mode) && string.Equals(mode, "Vanilla", StringComparison.OrdinalIgnoreCase);
            }
            if (cmConfig != null)
            {
                var cfg = SourceConfig.Parse(cmConfig).GroupBy(e => e.Section + "." + e.Key).ToDictionary(g => g.Key, g => g.First().Value);
                bool Is(string key, string value) => cfg.TryGetValue(key, out var v) && string.Equals(v, value, StringComparison.OrdinalIgnoreCase);
                bool Default(string key, string value, bool fallback) => cfg.ContainsKey(key) ? Is(key, value) : fallback;
                s.CmLevelSystemOff = Default("2 - Levels.Enable Level System", "Off", false);
                s.CmAllModifiersOff = Default("2 - Levels.Global Modifiers", "Off", false) && Default("2 - Levels.Boss Modifiers", "Off", false)
                    && Default("2 - Levels.Enforcer Modifiers", "Off", false);
                s.CmKarmaOff = Default("3 - Karma.Karma System Mode", "Off", false);
                s.CmTargetSwitchOff = Default("7 - AI.Player Target Switching", "Off", true);
                s.CmLootVanilla = Default("6 - Loot.Character Loot System", "Vanilla", false);
            }
            if (sylbConfig != null)
            {
                var entry = SourceConfig.Parse(sylbConfig).FirstOrDefault(e => e.Section == "Local Settings" && e.Key == "Enable SaveYourLuckBruh");
                s.SylbEnabled = entry.Key == null || !string.Equals(entry.Value, "false", StringComparison.OrdinalIgnoreCase);
            }
            return s;
        }
    }

    /// <summary>One step-aside notice per module: localization key and the installed sources for <c>{0}</c>.</summary>
    public readonly struct StepAsideNotice
    {
        public StepAsideNotice(WildbloodModule module, string sourceNames)
        {
            Module = module;
            SourceNames = sourceNames;
        }

        public WildbloodModule Module { get; }
        public string SourceNames { get; }
        public string Key => "step_aside_" + Module.ToString().ToLowerInvariant();
    }

    public sealed class StepAsideDecision
    {
        private readonly Dictionary<WildbloodModule, List<ModuleSource>> aside;

        internal StepAsideDecision(Dictionary<WildbloodModule, List<ModuleSource>> aside)
        {
            this.aside = aside;
            Notices = aside.OrderBy(p => p.Key).Select(p => new StepAsideNotice(p.Key, string.Join(" and ", p.Value.Select(s => s.Name)))).ToList();
        }

        public bool Any => aside.Count > 0;

        public bool SteppedAside(WildbloodModule module) => aside.ContainsKey(module);

        public IReadOnlyList<ModuleSource> SourcesOf(WildbloodModule module) =>
            aside.TryGetValue(module, out var list) ? list : (IReadOnlyList<ModuleSource>)Array.Empty<ModuleSource>();

        public IReadOnlyList<StepAsideNotice> Notices { get; }
    }

    public static class ModuleStepAside
    {
        public static readonly ModuleSource EliteCreaturesReborn = new ModuleSource("gglasgow.elitecreaturesreborn", "EliteCreaturesReborn");
        public static readonly ModuleSource CreatureManager = new ModuleSource("sighsorry.CreatureManager", "CreatureManager");
        public static readonly ModuleSource SaveYourLuckBruh = new ModuleSource("vapok.mods.SaveYourLuckBruh", "SaveYourLuckBruh");
        public static readonly ModuleSource DropNSpawn = new ModuleSource("sighsorry.DropNSpawn", "DropNSpawn");

        public static IReadOnlyList<ModuleSource> Sources { get; } = Array.AsReadOnly(new[] { EliteCreaturesReborn, CreatureManager, SaveYourLuckBruh, DropNSpawn });

        /// <summary>
        /// Per module, the installed sources whose own switch says they act on it. GUIDs match exactly (a renamed .old plugin
        /// does not load and is not reported by the chainloader).
        /// </summary>
        public static StepAsideDecision Decide(IEnumerable<string> installedGuids, SourceSwitches switches, bool deferToDropNSpawn)
        {
            if (installedGuids == null) throw new ArgumentNullException(nameof(installedGuids));
            var s = switches ?? SourceSwitches.Unknown;
            var installed = new HashSet<string>(installedGuids.Where(g => g != null), StringComparer.Ordinal);
            var aside = new Dictionary<WildbloodModule, List<ModuleSource>>();
            void Add(ModuleSource source, WildbloodModule module, bool acts)
            {
                if (!acts || !installed.Contains(source.Guid)) return;
                if (!aside.TryGetValue(module, out var list)) aside[module] = list = new List<ModuleSource>();
                list.Add(source);
            }
            Add(EliteCreaturesReborn, WildbloodModule.Stars, s.EcrCreatureStars != false);
            Add(EliteCreaturesReborn, WildbloodModule.Traits, true);
            Add(EliteCreaturesReborn, WildbloodModule.Bosses, true);
            Add(EliteCreaturesReborn, WildbloodModule.Loot, s.EcrLootVanilla != true);
            Add(EliteCreaturesReborn, WildbloodModule.Recap, true);
            Add(EliteCreaturesReborn, WildbloodModule.Horde, s.EcrHorde != false);
            Add(CreatureManager, WildbloodModule.Stars, s.CmLevelSystemOff != true);
            Add(CreatureManager, WildbloodModule.Traits, s.CmAllModifiersOff != true);
            Add(CreatureManager, WildbloodModule.Pressure, s.CmKarmaOff != true);
            Add(CreatureManager, WildbloodModule.Loot, s.CmLootVanilla != true);
            Add(CreatureManager, WildbloodModule.AI, s.CmTargetSwitchOff != true);
            Add(SaveYourLuckBruh, WildbloodModule.Luck, s.SylbEnabled != false);
            Add(DropNSpawn, WildbloodModule.Loot, deferToDropNSpawn);
            return new StepAsideDecision(aside);
        }
    }

    /// <summary>
    /// Keep-crossbow-loaded has exactly one owner. Edge owns weapon handling, so Wildblood's copy steps aside whenever
    /// Edge is installed, even with Edge's own switch off (that is the player's choice for the feature). EliteCreaturesReborn
    /// runs its own unconditional copy, so Wildblood steps aside for it too.
    /// </summary>
    public static class LoadedWeaponOwnership
    {
        /// <summary>Edge's BepInEx plugin GUID (Edge's EdgeCore.PluginGuid).</summary>
        public const string EdgeGuid = "com.meadworks.edge";

        public static bool WildbloodActs(IEnumerable<string> installedGuids)
        {
            if (installedGuids == null) throw new ArgumentNullException(nameof(installedGuids));
            return !installedGuids.Any(g => string.Equals(g, EdgeGuid, StringComparison.Ordinal)
                || string.Equals(g, ModuleStepAside.EliteCreaturesReborn.Guid, StringComparison.Ordinal));
        }
    }
}
