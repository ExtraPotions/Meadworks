// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Ember.Config;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Ember.Runtime
{
    internal sealed class EmberPresetRuntime : IDisposable
    {
        private readonly ConfigFile file;
        private readonly EmberConfig config;
        private readonly MeadworksLog log;
        private readonly Dictionary<EmberPresetGroup, PresetEdits> pending = EmberPresets.All.ToDictionary(g => g, _ => new PresetEdits());
        private readonly Dictionary<EmberPresetGroup, SettingsPreset> acknowledged = new Dictionary<EmberPresetGroup, SettingsPreset>();
        private bool writing;
        private long generation;
        private SessionRole role;

        internal EmberPresetRuntime(ConfigFile file, EmberConfig config, MeadworksLog log)
        {
            this.file = file; this.config = config; this.log = log;
            RememberSession();
            Reconcile();
            file.SettingChanged += Changed;
            file.ConfigReloaded += Reloaded;
        }

        private bool Remote => CruciblePlugin.IsReady && CruciblePlugin.Session.Role == SessionRole.RemoteClient;
        private object Read(EmberSetting setting) => config.Entry(setting).BoxedValue;

        private void RememberSession()
        {
            generation = CruciblePlugin.IsReady ? CruciblePlugin.Session.Generation : -1;
            role = CruciblePlugin.IsReady ? CruciblePlugin.Session.Role : SessionRole.Menu;
        }

        private void Changed(object? sender, SettingChangedEventArgs args)
        {
            if (writing) return;
            foreach (var group in EmberPresets.All)
            {
                if (Remote && group.Selector.Scope != SettingScope.Local) continue;
                if (ReferenceEquals(args.ChangedSetting, config.Entry(group.Selector))) pending[group].Observe(true);
                else if (group.Members.Any(s => ReferenceEquals(args.ChangedSetting, config.Entry(s)))) pending[group].Observe(false);
            }
        }

        private void Reloaded(object? sender, EventArgs args)
        {
            foreach (var edit in pending.Values) edit.Clear();
            Reconcile();
        }

        private void Reconcile()
        {
            foreach (var group in EmberPresets.All)
            {
                if (Remote && group.Selector.Scope != SettingScope.Local) continue;
                var entry = (ConfigEntry<SettingsPreset>)config.Entry(group.Selector);
                var selected = group.Reconcile(entry.Value, Read);
                if (selected != entry.Value) Write(new Dictionary<EmberSetting, object> { [group.Selector] = selected });
                acknowledged[group] = selected;
            }
        }

        internal void Tick()
        {
            var previousGeneration = generation;
            var previousRole = role;
            RememberSession();
            if (previousGeneration != generation || previousRole != role)
            {
                foreach (var edit in pending.Values) edit.Clear();
                Reconcile();
                return;
            }
            foreach (var group in EmberPresets.All)
            {
                var edit = pending[group];
                if (!edit.Pending) continue;
                var apply = edit.ApplySelection;
                edit.Clear();
                if (Remote && group.Selector.Scope != SettingScope.Local) continue;
                if (group.Selector.Scope == SettingScope.Local &&
                    (config.IsLocked(group.Selector) || group.Members.Any(config.IsLocked)))
                {
                    RestoreSelection(group);
                    continue;
                }
                var selected = (SettingsPreset)Read(group.Selector);
                var changes = apply ? new Dictionary<EmberSetting, object>(group.Values(selected)) : new Dictionary<EmberSetting, object>();
                changes[group.Selector] = apply ? selected : SettingsPreset.Custom;
                var declared = new[] { group.Selector }.Concat(group.Members).Select(config.Entry).ToArray();
                try
                {
                    var result = MeadworksConfig.AmendBatch(Plugin.Guid, declared, () => Write(changes));
                    if (result.Applied) acknowledged[group] = apply ? selected : SettingsPreset.Custom;
                    else
                    {
                        log.Warning("Preset was not applied: " + group.Selector.Id + ". " + result);
                        RestoreSelection(group);
                    }
                }
                catch (Exception error)
                {
                    log.Warning("Preset was not applied: " + group.Selector.Id + ". " + error.Message);
                    RestoreSelection(group);
                }
            }
        }

        private void RestoreSelection(EmberPresetGroup group)
        {
            var previous = acknowledged.TryGetValue(group, out var saved) ? saved : SettingsPreset.Custom;
            var actual = group.Reconcile(previous, Read);
            try { Write(new Dictionary<EmberSetting, object> { [group.Selector] = actual }); acknowledged[group] = actual; }
            catch (Exception error)
            {
                var save = file.SaveOnConfigSet;
                writing = true; file.SaveOnConfigSet = false;
                try { config.Entry(group.Selector).BoxedValue = actual; acknowledged[group] = actual; }
                finally { file.SaveOnConfigSet = save; writing = false; }
                log.Warning("Preset selector restored in memory but could not be saved: " + group.Selector.Id + ". " + error.Message);
            }
        }

        private void Write(IReadOnlyDictionary<EmberSetting, object> changes)
        {
            var entries = changes.Keys.Select(config.Entry).ToArray();
            var before = entries.ToDictionary(e => e, e => e.BoxedValue);
            var save = file.SaveOnConfigSet;
            writing = true;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (var pair in changes) config.Entry(pair.Key).BoxedValue = pair.Value;
                PresetPersistence.Save(entries);
            }
            catch
            {
                foreach (var pair in before) pair.Key.BoxedValue = pair.Value;
                throw;
            }
            finally { file.SaveOnConfigSet = save; writing = false; }
        }

        public void Dispose()
        {
            file.SettingChanged -= Changed;
            file.ConfigReloaded -= Reloaded;
            foreach (var edit in pending.Values) edit.Clear();
        }
    }
}
