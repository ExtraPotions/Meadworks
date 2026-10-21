// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module step-aside (design 10.1, Shepherd Q1 rule) for EliteCrafting and ItemEnhancements.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Inscribe.Integration
{
    /// <summary>Inscribe's feature modules (design 1.2). <see cref="Enhancement"/> is the one-time conversion of the removed enhancement system (0.2.0).</summary>
    public enum InscribeModule { Rarity, Enhancement, Drops, Display, Cheats }

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
    /// One step-aside notice: localization key, the installed sources for <c>{0}</c>, and the Crucible C09 module report
    /// (module id, source GUID). With both sources the module is <c>all</c>, reported under EliteCrafting's GUID with both names.
    /// </summary>
    public readonly struct StepAsideNotice
    {
        public StepAsideNotice(string key, string sourceNames, string sourceGuid)
        {
            Key = key;
            SourceNames = sourceNames;
            SourceGuid = sourceGuid;
        }

        public string Key { get; }
        public string SourceNames { get; }
        public string SourceGuid { get; }
        public string ModuleId => Key.Substring("step_aside_".Length);
    }

    /// <summary>
    /// What runs this session. With EliteCrafting: Rarity (runes, inscriptions), rune and gear drops, the rarity parts of
    /// Display and the rune prefab registration step aside (EliteCrafting registers the same <c>ECF_*</c> names). With
    /// ItemEnhancements: the enhancement conversion and the legacy scroll prefab registration step aside (that mod owns the
    /// levels and the scrolls), and the Rarity stat postfixes register after ItemEnhancements' so values compose. With
    /// both: Inscribe is fully off with one notice.
    /// </summary>
    public sealed class StepAsideDecision
    {
        internal StepAsideDecision(bool eliteCrafting, bool itemEnhancements)
        {
            EliteCrafting = eliteCrafting;
            ItemEnhancements = itemEnhancements;
            var notices = new List<StepAsideNotice>();
            if (ProductOff) notices.Add(new StepAsideNotice("step_aside_all", ModuleStepAside.EliteCrafting.Name + " and " + ModuleStepAside.ItemEnhancements.Name, ModuleStepAside.EliteCrafting.Guid));
            else if (eliteCrafting) notices.Add(new StepAsideNotice("step_aside_rarity", ModuleStepAside.EliteCrafting.Name, ModuleStepAside.EliteCrafting.Guid));
            else if (itemEnhancements) notices.Add(new StepAsideNotice("step_aside_enhancement", ModuleStepAside.ItemEnhancements.Name, ModuleStepAside.ItemEnhancements.Guid));
            Notices = notices;
        }

        public bool EliteCrafting { get; }
        public bool ItemEnhancements { get; }
        public bool ProductOff => EliteCrafting && ItemEnhancements;
        public bool Any => EliteCrafting || ItemEnhancements;

        public bool SteppedAside(InscribeModule module)
        {
            if (ProductOff) return true;
            switch (module)
            {
                case InscribeModule.Rarity: return EliteCrafting;
                case InscribeModule.Enhancement: return ItemEnhancements;
                default: return false;
            }
        }

        public bool RuneAndGearDrops => !EliteCrafting;
        public bool RarityDisplay => !EliteCrafting;
        public bool RunePrefabs => !EliteCrafting;
        /// <summary>Inscribe registers the inert legacy scroll prefabs so saved scrolls load and convert.</summary>
        public bool ScrollPrefabs => !ItemEnhancements;

        /// <summary>Only ItemEnhancements runs: Inscribe's item-stat postfixes run after its own (design 10.1).</summary>
        public bool StatPatchesAfterItemEnhancements => ItemEnhancements && !EliteCrafting;

        public IReadOnlyList<StepAsideNotice> Notices { get; }
    }

    public static class ModuleStepAside
    {
        public static readonly ModuleSource EliteCrafting = new ModuleSource("com.EliteCrafting", "EliteCrafting");
        public static readonly ModuleSource ItemEnhancements = new ModuleSource("ckforgame.ItemEnhancements", "ItemEnhancements");

        public static IReadOnlyList<ModuleSource> Sources { get; } = Array.AsReadOnly(new[] { EliteCrafting, ItemEnhancements });

        /// <summary>Decides from the loaded plugin GUIDs; they match exactly (a renamed .old plugin does not load).</summary>
        public static StepAsideDecision Decide(IEnumerable<string> installedGuids)
        {
            if (installedGuids == null) throw new ArgumentNullException(nameof(installedGuids));
            var installed = new HashSet<string>(installedGuids.Where(g => g != null), StringComparer.Ordinal);
            return new StepAsideDecision(installed.Contains(EliteCrafting.Guid), installed.Contains(ItemEnhancements.Guid));
        }
    }
}
