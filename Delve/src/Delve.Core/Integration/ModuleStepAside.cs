// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module step-aside (design 9, user decision Q1: per part; DV15 for Elite Creatures Reborn).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Conversion;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Integration
{
    /// <summary>The parts of Delve that step aside on their own.</summary>
    public enum DelveModule
    {
        /// <summary>Room counts (A).</summary>
        Rooms,
        /// <summary>Full dungeon resets (C1).</summary>
        FullReset,
        /// <summary>Loot chest refill and history (C2).</summary>
        Chests,
        /// <summary>Dungeon resources, world objects and boss return (C3 to C5).</summary>
        Resources,
        /// <summary>Camp and dungeon creature respawn (DV15).</summary>
        Respawn,
    }

    public sealed class SourceInfo
    {
        internal SourceInfo(string guid, string name, params DelveModule[] modules)
        {
            Guid = guid;
            Name = name;
            Modules = Array.AsReadOnly(modules);
        }

        public string Guid { get; }
        public string Name { get; }

        /// <summary>The modules this source can take over.</summary>
        public IReadOnlyList<DelveModule> Modules { get; }
    }

    /// <summary>The source switches that decide whether an installed tool acts on Delve's part (read from its own files).</summary>
    public sealed class SourceSwitches
    {
        private SourceSwitches(bool vatDungeons, bool ecrRespawn, bool ecrLoot)
        {
            AdminToolDungeonResets = vatDungeons;
            CreatureRespawn = ecrRespawn;
            DungeonLoot = ecrLoot;
        }

        /// <summary>Nothing readable: every switch at its source's default (all off).</summary>
        public static SourceSwitches Unknown { get; } = new SourceSwitches(false, false, false);

        /// <summary>Valheim_Admin_Tool <c>[Automatic resets] Reset dungeons automatically</c>.</summary>
        public bool AdminToolDungeonResets { get; }

        /// <summary>Elite Creatures Reborn <c>respawning: camps</c> or <c>dungeons</c>.</summary>
        public bool CreatureRespawn { get; }

        /// <summary>Elite Creatures Reborn <c>respawning: dungeon loot</c>.</summary>
        public bool DungeonLoot { get; }

        public static SourceSwitches Read(string? adminToolConfig, string? creatureRules)
        {
            bool vat = false;
            if (adminToolConfig != null)
            {
                var cfg = CfgFile.Parse(adminToolConfig);
                vat = cfg.Bool("Automatic resets", "Reset dungeons automatically") ?? false;
            }
            var respawn = SourceImport.RespawningBlock(creatureRules);
            bool camps = Flag(respawn, "camps"), dungeons = Flag(respawn, "dungeons"), loot = Flag(respawn, "dungeon loot");
            return new SourceSwitches(vat, camps || dungeons, loot);
        }

        private static bool Flag(IReadOnlyDictionary<string, string> block, string key) =>
            block.TryGetValue(key, out var v) && bool.TryParse(v, out var b) && b;
    }

    public sealed class StepAsideNotice
    {
        internal StepAsideNotice(DelveModule module, SourceInfo source)
        {
            Module = module;
            SourceGuid = source.Guid;
            SourceName = source.Name;
        }

        public DelveModule Module { get; }
        public string SourceGuid { get; }
        public string SourceName { get; }

        /// <summary>The C05 key of the notice, <c>step_aside_&lt;module&gt;</c> (<c>{0}</c> = source name).</summary>
        public string Key => ModuleStepAside.KeyOf(Module);
    }

    public sealed class ModuleStepAsideDecision
    {
        internal ModuleStepAsideDecision(IReadOnlyList<StepAsideNotice> notices) => Notices = notices;

        /// <summary>One notice per module that steps aside, naming the first source that took it.</summary>
        public IReadOnlyList<StepAsideNotice> Notices { get; }

        public IReadOnlyList<DelveModule> Modules => Notices.Select(n => n.Module).OrderBy(m => m).ToArray();

        public bool Any => Notices.Count > 0;

        public bool SteppedAside(DelveModule module) => Notices.Any(n => n.Module == module);
    }

    public static class ModuleStepAside
    {
        public static readonly SourceInfo MaxDungeonRooms = new SourceInfo("digitalroot.mods.maxdungeonrooms", "Max_Dungeon_Rooms", DelveModule.Rooms);
        public static readonly SourceInfo MoreDungeonResets = new SourceInfo("com.loathe.MoreDungeonResets", "MoreDungeonResets", DelveModule.FullReset);
        public static readonly SourceInfo RestockRegen = new SourceInfo("liekos47.restockregen", "RestockRegen", DelveModule.Chests, DelveModule.Resources);
        public static readonly SourceInfo ValheimAdminTool = new SourceInfo("com.icecub.valheimadmintool", "Valheim_Admin_Tool", DelveModule.FullReset);
        public static readonly SourceInfo EliteCreaturesReborn = new SourceInfo("gglasgow.elitecreaturesreborn", "Elite Creatures Reborn", DelveModule.Chests, DelveModule.Respawn);

        public static IReadOnlyList<SourceInfo> Sources { get; } = Array.AsReadOnly(new[] { MaxDungeonRooms, MoreDungeonResets, RestockRegen, ValheimAdminTool, EliteCreaturesReborn });

        public static string KeyOf(DelveModule module) => "step_aside_" + module.ToString().ToLowerInvariant();

        /// <summary>The module a kind belongs to.</summary>
        public static DelveModule ModuleOf(DelveKind kind)
        {
            switch (kind)
            {
                case DelveKind.FullReset: return DelveModule.FullReset;
                case DelveKind.Chests: return DelveModule.Chests;
                case DelveKind.CampSpawners:
                case DelveKind.DungeonSpawners: return DelveModule.Respawn;
                default: return DelveModule.Resources;
            }
        }

        /// <summary>
        /// Which modules step aside, given the installed plugin GUIDs and the source switches. Max_Dungeon_Rooms,
        /// MoreDungeonResets and RestockRegen take their parts whenever installed; Valheim_Admin_Tool only while its
        /// automatic dungeon reset is on; Elite Creatures Reborn only while its respawning (or dungeon loot) is on.
        /// </summary>
        public static ModuleStepAsideDecision Decide(IEnumerable<string> installed, SourceSwitches switches)
        {
            if (installed == null) throw new ArgumentNullException(nameof(installed));
            if (switches == null) throw new ArgumentNullException(nameof(switches));
            var present = new HashSet<string>(installed, StringComparer.Ordinal);
            var notices = new List<StepAsideNotice>();
            foreach (var source in Sources)
            {
                if (!present.Contains(source.Guid)) continue;
                foreach (var module in source.Modules)
                {
                    if (!Acts(source, module, switches) || notices.Any(n => n.Module == module)) continue;
                    notices.Add(new StepAsideNotice(module, source));
                }
            }
            return new ModuleStepAsideDecision(notices);
        }

        private static bool Acts(SourceInfo source, DelveModule module, SourceSwitches switches)
        {
            if (source == ValheimAdminTool) return switches.AdminToolDungeonResets;
            if (source == EliteCreaturesReborn) return module == DelveModule.Respawn ? switches.CreatureRespawn : switches.DungeonLoot;
            return true;
        }
    }
}
