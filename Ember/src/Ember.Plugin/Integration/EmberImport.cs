// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: C09 converter registrations for the three source configs (design 10.3). Source files are read
// only; Apply backs up Ember's config first.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Ember.Config;
using ExtraPotions.Ember.Conversion;
using ExtraPotions.Ember.Localization;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Ember.Runtime
{
    public sealed class EmberImport : IDisposable
    {
        readonly ConfigFile config;
        readonly EmberConfig settings;
        readonly MeadworksLog log;
        readonly ConfigEntry<string> marker;
        readonly List<IDisposable> registrations = new List<IDisposable>();
        readonly ConditionalWeakTable<ImportPlan, Snapshot> previews = new ConditionalWeakTable<ImportPlan, Snapshot>();
        bool disposed;

        sealed class Snapshot
        {
            internal string SourceHash = "", TargetHash = "";
            internal long Generation;
            internal SessionRole Role;
            internal bool Consumed;
        }

        public EmberImport(ConfigFile config, EmberConfig settings, MeadworksLog log)
        {
            this.config = config;
            this.settings = settings;
            this.log = log;
            marker = MeadworksConfig.BindHidden(config, "Import", "Imported Sources", "", "Managed by Ember.");
            foreach (ImportSource source in Enum.GetValues(typeof(ImportSource)))
            {
                var s = source;
                registrations.Add(MeadworksConversion.Register(new ConversionRegistration("ember." + s.ToString().ToLowerInvariant(), Plugin.Guid,
                    Integration.ModuleStepAside.Sources[(int)s].Guid, s.ToString(),
                    () => !disposed && File.Exists(SourcePath(s)) && !Imported(s),
                    () => Preview(s),
                    (document, resolutions, backup) => Apply(document as ImportPlan ?? throw new ArgumentException("Wrong Ember import preview."), backup))));
            }
        }

        static string SourcePath(ImportSource source) => Path.Combine(Paths.ConfigPath, SourceImport.FileName(source));

        bool Imported(ImportSource source) => marker.Value.Split(',').Contains(source.ToString());

        public ImportPlan Preview(ImportSource source)
        {
            if (disposed || Imported(source)) throw new InvalidOperationException("Source is unavailable or already imported.");
            var snapshot = new Snapshot { Generation = ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation,
                Role = ExtraPotions.Meadworks.Crucible.Plugin.Session.Role, TargetHash = TargetHash() };
            var text = Read(SourcePath(source), out snapshot.SourceHash);
            var plan = SourceImport.Plan(source, text);
            previews.Add(plan, snapshot);
            var report = Path.Combine(Paths.ConfigPath, "Meadworks", "ember", "import-" + source.ToString().ToLowerInvariant() + ".txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            File.WriteAllText(report, plan.Report());
            log.Info("Import preview for " + source + ": " + plan.Values.Count + " values, " + plan.Notes.Count + " notes (" + report + ").");
            return plan;
        }

        static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes));
        }

        static string Read(string path, out string hash)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 1024 * 1024) throw new InvalidDataException("Import source exceeds 1 MiB.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray(); hash = Hash(bytes);
            using var reader = new StreamReader(new MemoryStream(bytes));
            return reader.ReadToEnd();
        }

        string TargetHash()
        {
            var disk = File.Exists(config.ConfigFilePath) ? Hash(File.ReadAllBytes(config.ConfigFilePath)) : "missing";
            var entries = config.Keys.OrderBy(key => key.Section, StringComparer.Ordinal).ThenBy(key => key.Key, StringComparer.Ordinal)
                .Select(key => key.Section + "." + key.Key + "=" + TomlTypeConverter.ConvertToString(config[key].BoxedValue, config[key].SettingType));
            return Hash(Encoding.UTF8.GetBytes(disk + "\n" + string.Join("\n", entries)));
        }

        void Admission(ImportPlan plan, Snapshot snapshot)
        {
            var session = ExtraPotions.Meadworks.Crucible.Plugin.Session;
            if (disposed || snapshot.Consumed || !ExtraPotions.Meadworks.Crucible.Plugin.IsReady || Imported(plan.Source)
                || session.Role == SessionRole.RemoteClient || session.Role != snapshot.Role || session.Generation != snapshot.Generation
                || snapshot.TargetHash != TargetHash())
                throw new InvalidOperationException("Import source, target or session changed, or this is a remote client; preview on the authority again.");
            Read(SourcePath(plan.Source), out var sourceHash);
            if (sourceHash != snapshot.SourceHash) throw new InvalidOperationException("Source changed; preview again.");
        }

        ConfigEntryBase Editable(EmberSetting setting)
        {
            var entry = settings.Entry(setting);
            if (!MeadworksConfig.TryGetMetadata(entry, out var metadata) || metadata.ProductId != Plugin.Guid
                || !metadata.Decision.Allowed || metadata.Decision.RequiresServerAmendment || settings.IsLocked(setting))
                throw new InvalidOperationException("Import blocked: " + setting.Key);
            return entry;
        }

        object Apply(ImportPlan plan, bool backup)
        {
            if (!backup) throw new InvalidOperationException("Ember import requires a backup.");
            if (!previews.TryGetValue(plan, out var snapshot)) throw new ArgumentException("Wrong or expired Ember import preview.");
            Admission(plan, snapshot);
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            var entries = new List<ConfigEntryBase>();
            foreach (var value in plan.Values)
            {
                var setting = EmberSettings.Find(value.SettingId) ?? throw new ArgumentException("Unknown Ember import setting.");
                var entry = Editable(setting);
                if (!entry.SettingType.IsInstanceOfType(value.Value)
                    || (entry.Description.AcceptableValues != null && !entry.Description.AcceptableValues.IsValid(value.Value)))
                    throw new ArgumentException("Invalid Ember import value: " + setting.Key);
                values.Add(setting.Id, value.Value); entries.Add(entry);
            }
            if (entries.Count == 0) return EmberText.Format(EmberTextKeys.ImportDone, 0, plan.Source.ToString());
            var applied = entries.Count;
            values.Add(marker.Definition.Section + "." + marker.Definition.Key,
                string.Join(",", marker.Value.Split(',').Where(p => p.Length > 0).Append(plan.Source.ToString()).Distinct()));
            entries.Add(marker);
            var target = MeadworksConversion.Target(config);
            ((IValidatedConversionTarget)target).Validate(values);
            var savedLocally = false;
            ConfigAmendResult result;
            try
            {
                result = MeadworksConfig.AmendBatch(Plugin.Guid, entries, () =>
                {
                    Admission(plan, snapshot);
                    foreach (var value in plan.Values) Editable(EmberSettings.Find(value.SettingId)!);
                    target.Backup(); target.WriteAtomic(values);
                    savedLocally = true; snapshot.Consumed = true;
                }, "Ember import");
            }
            catch (Exception error) when (savedLocally)
            {
                throw new IOException("Ember import was saved locally, but synchronization did not complete. Review synchronization before continuing; do not retry this preview.", error);
            }
            if (!result.Applied) throw new InvalidOperationException("Ember import was refused before writing.");
            var text = EmberText.Format(EmberTextKeys.ImportDone, applied, plan.Source.ToString());
            log.Info(text);
            return text;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var r in registrations) r.Dispose();
            registrations.Clear();
        }
    }
}
