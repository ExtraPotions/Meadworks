// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module step-aside (design 10, Q2, C09); same model as Tempest.Core (expDARE).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Thing.Integration
{
    /// <summary>
    /// The parts of Thing that step aside on their own (Q2): WorldTools, the World page tools Valheim_Admin_Tool also offers
    /// (player teleports, global time, terrain reset, remove drops, ward override, world seed; see
    /// <c>ActionInfo.OverlapsAdminTool</c>). People, Moderation log, Messages, Regeneration and Server config always stay.
    /// </summary>
    public enum ThingModule { WorldTools }

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
        internal ModuleSource(string guid, string name, ThingModule module, params string[] pluginNames)
        {
            Guid = guid;
            Name = name;
            Module = module;
            PluginNames = pluginNames;
        }

        public string Guid { get; }
        public string Name { get; }
        public ThingModule Module { get; }

        /// <summary>Display names that also identify the source (used when its GUID is unconfirmed).</summary>
        public IReadOnlyList<string> PluginNames { get; }

        public string ModuleId => Module.ToString().ToLowerInvariant();

        public bool Matches(InstalledPlugin plugin) => string.Equals(plugin.Guid, Guid, StringComparison.Ordinal)
            || PluginNames.Any(n => string.Equals(n, plugin.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>One step-aside notice: the localization key (one per module) and the source mod's name for <c>{0}</c>.</summary>
    public readonly struct StepAsideNotice
    {
        public StepAsideNotice(ThingModule module, string sourceName, string sourceGuid)
        {
            Module = module;
            SourceName = sourceName;
            SourceGuid = sourceGuid;
        }

        public ThingModule Module { get; }
        public string SourceName { get; }
        public string SourceGuid { get; }
        public string Key => KeyOf(Module);

        public static string KeyOf(ThingModule module) => "step_aside_" + module.ToString().ToLowerInvariant();
    }

    public sealed class ModuleStepAsideDecision
    {
        private readonly HashSet<ThingModule> aside;

        internal ModuleStepAsideDecision(IReadOnlyList<ModuleSource> present)
        {
            Present = present;
            aside = new HashSet<ThingModule>(present.Select(p => p.Module));
            Notices = present.OrderBy(p => p.Module).Select(p => new StepAsideNotice(p.Module, p.Name, p.Guid)).ToList();
        }

        public IReadOnlyList<ModuleSource> Present { get; }

        public bool Any => aside.Count > 0;

        public bool SteppedAside(ThingModule module) => aside.Contains(module);

        /// <summary>Valheim_Admin_Tool is loaded on this peer: the overlapping World tools hide (<c>ActionCatalog.Visible</c>).</summary>
        public bool AdminToolLoaded => SteppedAside(ThingModule.WorldTools);

        public IReadOnlyList<StepAsideNotice> Notices { get; }
    }

    public static class ModuleStepAside
    {
        /// <summary>
        /// Valheim_Admin_Tool 0.5.0 (GUID from its config header). Anti Valheim Admin Tool is not a source: it is a server
        /// blocker whose role is Hex's Mischief gate (design 10).
        /// </summary>
        public static readonly ModuleSource AdminTool = new ModuleSource("com.icecub.valheimadmintool", "Valheim_Admin_Tool", ThingModule.WorldTools);

        public static IReadOnlyList<ModuleSource> Sources { get; } = Array.AsReadOnly(new[] { AdminTool });

        public static ModuleStepAsideDecision Decide(IEnumerable<InstalledPlugin> installed)
        {
            if (installed == null) throw new ArgumentNullException(nameof(installed));
            var list = installed.ToList();
            return new ModuleStepAsideDecision(Sources.Where(s => list.Any(s.Matches)).ToList());
        }
    }
}
