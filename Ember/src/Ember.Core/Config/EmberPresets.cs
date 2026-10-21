// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Ember.Doors;

namespace ExtraPotions.Ember.Config
{
    public enum SettingsPreset { MeadWorksDefault, Vanilla, Relaxed, Custom }

    public sealed class EmberPresetGroup
    {
        private readonly IReadOnlyDictionary<SettingsPreset, IReadOnlyDictionary<EmberSetting, object>> choices;

        internal EmberPresetGroup(EmberSetting<SettingsPreset> selector,
            IReadOnlyDictionary<SettingsPreset, IReadOnlyDictionary<EmberSetting, object>> choices)
        {
            Selector = selector;
            this.choices = choices;
            Members = choices[SettingsPreset.MeadWorksDefault].Keys.ToArray();
        }

        public EmberSetting<SettingsPreset> Selector { get; }
        public IReadOnlyList<EmberSetting> Members { get; }
        public IReadOnlyDictionary<EmberSetting, object> Values(SettingsPreset preset) =>
            choices.TryGetValue(preset, out var values) ? values : new Dictionary<EmberSetting, object>();

        public SettingsPreset Reconcile(SettingsPreset saved, Func<EmberSetting, object> read) =>
            saved != SettingsPreset.Custom && Values(saved).Count > 0 && Values(saved).All(p => Equals(p.Value, read(p.Key)))
                ? saved : SettingsPreset.Custom;
    }

    public sealed class PresetEdits
    {
        private bool selectorChanged;
        private bool memberChanged;
        public void Observe(bool selector) { if (selector) selectorChanged = true; else memberChanged = true; }
        public bool Pending => selectorChanged || memberChanged;
        public bool ApplySelection => selectorChanged && !memberChanged;
        public void Clear() { selectorChanged = false; memberChanged = false; }
    }

    public static class EmberPresets
    {
        private static readonly Lazy<IReadOnlyList<EmberPresetGroup>> groups = new Lazy<IReadOnlyList<EmberPresetGroup>>(Create);
        public static IReadOnlyList<EmberPresetGroup> All => groups.Value;

        private static IReadOnlyList<EmberPresetGroup> Create()
        {
            var sleep = new EmberSetting[] { EmberSettings.Sleep.UseVanillaSleepWindow, EmberSettings.Sleep.SleepStart,
                EmberSettings.Sleep.PartialSleepBoost, EmberSettings.Sleep.BonusMultiplier, EmberSettings.Sleep.BonusIncrementScale,
                EmberSettings.Sleep.BoostFadeSeconds, EmberSettings.Sleep.WakeAtWindowEnd };
            var vanilla = Defaults(sleep);
            vanilla[EmberSettings.Sleep.UseVanillaSleepWindow] = true;
            vanilla[EmberSettings.Sleep.PartialSleepBoost] = false;
            var relaxed = Defaults(sleep);
            relaxed[EmberSettings.Sleep.BonusMultiplier] = 1f;
            relaxed[EmberSettings.Sleep.BonusIncrementScale] = 30f;
            relaxed[EmberSettings.Sleep.BoostFadeSeconds] = 5f;

            var doors = new EmberSetting[] { EmberSettings.Doors.EnableAutoDoor }.Concat(DoorKinds.All.SelectMany(k =>
                new EmberSetting[] { EmberSettings.Door(k).OpenDistance, EmberSettings.Door(k).CloseDistance })).ToArray();
            var manual = Defaults(doors);
            manual[EmberSettings.Doors.EnableAutoDoor] = false;
            var wide = Defaults(doors);
            foreach (var kind in DoorKinds.All)
            {
                wide[EmberSettings.Door(kind).OpenDistance] = kind.PreviousOpenDistance;
                wide[EmberSettings.Door(kind).CloseDistance] = kind.PreviousCloseDistance;
            }
            return new[] { Group(EmberSettings.Sleep.Preset, sleep, vanilla, relaxed), Group(EmberSettings.Doors.Preset, doors, manual, wide) };
        }

        private static Dictionary<EmberSetting, object> Defaults(IEnumerable<EmberSetting> members) => members.ToDictionary(s => s, s => s.DefaultValue);
        private static EmberPresetGroup Group(EmberSetting<SettingsPreset> selector, EmberSetting[] members,
            Dictionary<EmberSetting, object> vanilla, Dictionary<EmberSetting, object> relaxed) => new EmberPresetGroup(selector,
                new Dictionary<SettingsPreset, IReadOnlyDictionary<EmberSetting, object>> {
                    [SettingsPreset.MeadWorksDefault] = Defaults(members), [SettingsPreset.Vanilla] = vanilla, [SettingsPreset.Relaxed] = relaxed });
    }
}
