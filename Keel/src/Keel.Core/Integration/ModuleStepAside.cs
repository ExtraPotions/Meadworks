// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module step-aside conditioned on each source mod's own switch (design 9.1, Shepherd Q1 rule).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Keel.Conversion;

namespace ExtraPotions.Keel.Integration
{
    /// <summary>
    /// The parts of Keel that step aside on their own: Tuning (ship values, damage, the two cheats and the ship panel),
    /// Sailing (skill, perks, Wind Call, Lookout) and Catalog (the eight ships and their pens).
    /// </summary>
    public enum KeelModule { Tuning, Sailing, Catalog }

    /// <summary>A source mod by BepInEx plugin GUID and the module it covers.</summary>
    public sealed class ModuleSource
    {
        internal ModuleSource(string guid, string name, KeelModule module)
        {
            Guid = guid;
            Name = name;
            Module = module;
        }

        public string Guid { get; }
        public string Name { get; }
        public KeelModule Module { get; }

        /// <summary>Crucible C09 module id (lowercase).</summary>
        public string ModuleId => Module.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// The source mods' own switches, read from their config files. Null means unknown (file missing or unreadable):
    /// the module then steps aside, the safe choice when two engines would both act.
    /// </summary>
    public sealed class SourceSwitches
    {
        public static SourceSwitches Unknown => new SourceSwitches();

        /// <summary>GrindstoneSkills <c>[8 - Sailing] Sailing Enabled</c> (default true).</summary>
        public bool? GsSailing { get; set; }

        public static SourceSwitches Read(string? grindstoneConfig)
        {
            var s = new SourceSwitches();
            if (grindstoneConfig != null)
            {
                var entry = SourceConfig.Parse(grindstoneConfig).FirstOrDefault(e => e.Section == "8 - Sailing" && e.Key == "Sailing Enabled");
                s.GsSailing = entry.Key == null || !string.Equals(entry.Value, "false", StringComparison.OrdinalIgnoreCase);
            }
            return s;
        }
    }

    /// <summary>One step-aside notice: the localization key (one per module) and the source mod's name for <c>{0}</c>.</summary>
    public readonly struct StepAsideNotice
    {
        public StepAsideNotice(KeelModule module, string sourceName, string sourceGuid)
        {
            Module = module;
            SourceName = sourceName;
            SourceGuid = sourceGuid;
        }

        public KeelModule Module { get; }
        public string SourceName { get; }
        public string SourceGuid { get; }
        public string Key => KeyOf(Module);

        public static string KeyOf(KeelModule module) => "step_aside_" + module.ToString().ToLowerInvariant();
    }

    public sealed class ModuleStepAsideDecision
    {
        private readonly HashSet<KeelModule> aside;

        internal ModuleStepAsideDecision(IReadOnlyList<ModuleSource> present)
        {
            Present = present;
            aside = new HashSet<KeelModule>(present.Select(p => p.Module));
            Notices = present.OrderBy(p => p.Module).Select(p => new StepAsideNotice(p.Module, p.Name, p.Guid)).ToList();
        }

        /// <summary>Installed source mods that act on their module, in table order.</summary>
        public IReadOnlyList<ModuleSource> Present { get; }

        public bool Any => aside.Count > 0;

        public bool SteppedAside(KeelModule module) => aside.Contains(module);

        public IReadOnlyList<StepAsideNotice> Notices { get; }
    }

    public static class ModuleStepAside
    {
        public static readonly ModuleSource ShipConfig = new ModuleSource("com.ShipConfig", "ShipConfig", KeelModule.Tuning);
        public static readonly ModuleSource GrindstoneSkills = new ModuleSource("com.GrindstoneSkills", "GrindstoneSkills", KeelModule.Sailing);

        /// <summary>GUID from the config file name its README names (<c>DeathMonger.TheGreatestShips.cfg</c>).</summary>
        public static readonly ModuleSource TheGreatestShips = new ModuleSource("DeathMonger.TheGreatestShips", "TheGreatestShips", KeelModule.Catalog);

        public static IReadOnlyList<ModuleSource> Sources { get; } = Array.AsReadOnly(new[] { ShipConfig, GrindstoneSkills, TheGreatestShips });

        /// <summary>Per module, the installed sources whose own switch says they act on it. GUIDs match exactly.</summary>
        public static ModuleStepAsideDecision Decide(IEnumerable<string> installedGuids, SourceSwitches switches)
        {
            if (installedGuids == null) throw new ArgumentNullException(nameof(installedGuids));
            var s = switches ?? SourceSwitches.Unknown;
            var installed = new HashSet<string>(installedGuids.Where(g => g != null), StringComparer.Ordinal);
            bool Acts(ModuleSource source) => source != GrindstoneSkills || s.GsSailing != false;
            return new ModuleStepAsideDecision(Sources.Where(src => installed.Contains(src.Guid) && Acts(src)).ToList());
        }
    }
}