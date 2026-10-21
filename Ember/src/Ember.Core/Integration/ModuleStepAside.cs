// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module step-aside (design 10.1, 10.2; user decision Q3).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Ember.Integration
{
    /// <summary>Ember's three independent modules, each replacing exactly one source mod.</summary>
    public enum EmberModule { Sleep, Torches, Doors }

    /// <summary>A source mod by BepInEx plugin GUID and the module it covers.</summary>
    public sealed class ModuleSource
    {
        internal ModuleSource(string guid, string name, EmberModule module)
        {
            Guid = guid;
            Name = name;
            Module = module;
        }

        public string Guid { get; }
        public string Name { get; }
        public EmberModule Module { get; }
    }

    /// <summary>One step-aside notice: the localization key (one per module) and the source mod's name for <c>{0}</c>.</summary>
    public readonly struct StepAsideNotice
    {
        public StepAsideNotice(EmberModule module, string sourceName)
        {
            Module = module;
            SourceName = sourceName;
        }

        public EmberModule Module { get; }
        public string SourceName { get; }
        public string Key => "step_aside_" + Module.ToString().ToLowerInvariant();
    }

    public sealed class ModuleStepAsideDecision
    {
        readonly HashSet<EmberModule> aside;

        internal ModuleStepAsideDecision(IReadOnlyList<ModuleSource> present)
        {
            Present = present;
            aside = new HashSet<EmberModule>(present.Select(p => p.Module));
            Notices = present.OrderBy(p => p.Module).Select(p => new StepAsideNotice(p.Module, p.Name)).ToList();
        }

        /// <summary>Installed source mods in table order.</summary>
        public IReadOnlyList<ModuleSource> Present { get; }

        public bool Any => aside.Count > 0;

        public bool SteppedAside(EmberModule module) => aside.Contains(module);

        public IReadOnlyList<StepAsideNotice> Notices { get; }
    }

    /// <summary>
    /// Step-aside per module: an installed BetterSleepBruh turns Ember's sleep module off, TorchesAreFires its torch
    /// module, DoorOpenerBruh its door module; the others keep running. GUIDs match exactly (a plugin renamed *.old does
    /// not load and is not reported by the chainloader).
    /// </summary>
    public static class ModuleStepAside
    {
        public static IReadOnlyList<ModuleSource> Sources { get; } = Array.AsReadOnly(new[]
        {
            new ModuleSource("vapok.mods.BetterSleepBruh", "BetterSleepBruh", EmberModule.Sleep),
            new ModuleSource("blacks7ar.TorchesAreFires", "TorchesAreFires", EmberModule.Torches),
            new ModuleSource("vapok.mods.dooropenerbruh", "DoorOpenerBruh", EmberModule.Doors),
        });

        public static ModuleStepAsideDecision Decide(IEnumerable<string> installedGuids)
        {
            if (installedGuids == null) throw new ArgumentNullException(nameof(installedGuids));
            var installed = new HashSet<string>(installedGuids.Where(g => g != null), StringComparer.Ordinal);
            return new ModuleStepAsideDecision(Sources.Where(s => installed.Contains(s.Guid)).ToList());
        }
    }
}
