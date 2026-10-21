// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Saga.Config;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Saga.Runtime
{
    /// <summary>
    /// Applies a queued preset on the next update, before gameplay reads the values.
    /// A reload or a session change corrects the label only. A remote client does not expand a host preset into its own file.
    /// </summary>
    internal sealed class SagaPresetRuntime : IDisposable
    {
        private readonly ConfigFile file;
        private readonly SagaConfig config;
        private readonly MeadworksLog log;
        private readonly SagaPresetChanges pending = new SagaPresetChanges();
        private readonly Dictionary<SagaPresetGroup, SagaPreset> acknowledged = new Dictionary<SagaPresetGroup, SagaPreset>();
        private bool writing;
        private bool reloaded;
        private long generation;
        private SessionRole role;

        internal SagaPresetRuntime(ConfigFile file, SagaConfig config, MeadworksLog log)
        {
            this.file = file ?? throw new ArgumentNullException(nameof(file));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            Remember();
            if (SagaPresets.Retired.Length > 0)
                log.Info("Saga retains but ignores obsolete configuration keys: " + string.Join(", ", SagaPresets.Retired) + ".");
            ReconcileLabels();
            file.SettingChanged += Changed;
            file.ConfigReloaded += Reloaded;
        }

        private bool Remote => CruciblePlugin.IsReady && CruciblePlugin.Session.Role == SessionRole.RemoteClient;

        private object Read(SagaSetting setting) => config.Entry(setting).BoxedValue;

        private void Remember()
        {
            generation = CruciblePlugin.IsReady ? CruciblePlugin.Session.Generation : -1;
            role = CruciblePlugin.IsReady ? CruciblePlugin.Session.Role : SessionRole.Menu;
        }

        private void Changed(object? sender, SettingChangedEventArgs args)
        {
            if (writing || args?.ChangedSetting == null || !config.TryMatch(args.ChangedSetting, out var setting)) return;
            pending.Observe(setting, Remote);
        }

        private void Reloaded(object? sender, EventArgs args)
        {
            if (writing) return;
            pending.Clear();
            reloaded = true;
        }

        internal void Tick()
        {
            var previousGeneration = generation;
            var previousRole = role;
            Remember();
            if (reloaded || previousGeneration != generation || previousRole != role)
            {
                pending.Clear();
                reloaded = false;
                ReconcileLabels();
                return;
            }
            try { pending.Flush(Remote, Read, Apply); }
            catch (AggregateException error)
            {
                foreach (var inner in error.InnerExceptions) log.Warning(inner.Message);
            }
        }

        private void Apply(SagaPresetGroup group, IReadOnlyDictionary<SagaSetting, object> values)
        {
            if (values.All(pair => Equals(Read(pair.Key), pair.Value)))
            {
                acknowledged[group] = (SagaPreset)values[group.Selector];
                return;
            }
            var declared = values.Keys.Select(config.Entry).ToArray();
            ConfigAmendResult result;
            try { result = MeadworksConfig.AmendBatch(Plugin.Guid, declared, () => Write(values), "Saga"); }
            catch (Exception error)
            {
                Restore(group);
                throw new InvalidOperationException("Preset was not applied: " + group.Selector.Id + ". " + error.Message, error);
            }
            if (!result.Applied)
            {
                Restore(group);
                throw new InvalidOperationException("Preset was not applied: " + group.Selector.Id + ".");
            }
            acknowledged[group] = (SagaPreset)values[group.Selector];
        }

        private void ReconcileLabels()
        {
            foreach (var group in SagaPresets.Groups)
            {
                if (!SagaPresets.CanWrite(group, Remote)) continue;
                var entry = (ConfigEntry<SagaPreset>)config.Entry(group.Selector);
                var selected = group.OnLoad(entry.Value, Read);
                if (selected != entry.Value) Write(new Dictionary<SagaSetting, object> { [group.Selector] = selected });
                acknowledged[group] = selected;
            }
        }

        private void Restore(SagaPresetGroup group)
        {
            var previous = acknowledged.TryGetValue(group, out var saved) ? saved : SagaPreset.Custom;
            var actual = group.OnLoad(previous, Read);
            try
            {
                Write(new Dictionary<SagaSetting, object> { [group.Selector] = actual });
                acknowledged[group] = actual;
            }
            catch (Exception error)
            {
                var save = file.SaveOnConfigSet;
                writing = true;
                file.SaveOnConfigSet = false;
                try { config.Entry(group.Selector).BoxedValue = actual; acknowledged[group] = actual; }
                finally { file.SaveOnConfigSet = save; writing = false; }
                log.Warning("Preset selector restored in memory but could not be saved: " + group.Selector.Id + ". " + error.Message);
            }
        }

        private void Write(IReadOnlyDictionary<SagaSetting, object> changes)
        {
            var entries = changes.Keys.Select(config.Entry).ToArray();
            var before = entries.ToDictionary(entry => entry, entry => entry.BoxedValue);
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
            pending.Clear();
        }
    }
}
