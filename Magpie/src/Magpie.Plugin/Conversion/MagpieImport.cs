// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Reuses Meadworks' own Ember C09 preview/admission and exact-entry batch publication patterns.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Magpie.Config;
using ExtraPotions.Magpie.Conversion;
using ExtraPotions.Magpie.Runtime.Settings;
using ExtraPotions.Magpie.Text;
using ExtraPotions.Meadworks.Crucible;
using Authority = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Magpie.Runtime
{
    internal sealed class MagpieImport : IDisposable
    {
        sealed class Snapshot
        {
            internal string SourceHash = "", TargetHash = "", Session = "";
            internal long Generation, World;
            internal SessionRole Role;
            internal bool Consumed;
        }
        readonly ConfigFile config;
        readonly MagpieConfig settings;
        readonly List<IDisposable> registrations = new List<IDisposable>();
        readonly ConditionalWeakTable<ImportPlan, Snapshot> previews = new ConditionalWeakTable<ImportPlan, Snapshot>();
        bool disposed;
        internal MagpieImport(ConfigFile config, MagpieConfig settings)
        {
            this.config = config; this.settings = settings;
            try
            {
                foreach (ImportSource source in Enum.GetValues(typeof(ImportSource)))
                {
                    var selected = source;
                    registrations.Add(MeadworksConversion.Register(new ConversionRegistration("magpie." + source.ToString().ToLowerInvariant(), Plugin.Guid,
                        SourceImport.SourceId(source), source.ToString(),
                        () => !disposed && Authority.IsReady && Authority.Session.IsAuthority && File.Exists(SourcePath(selected)),
                        () => Preview(selected), (document, resolutions, backup) => Apply(document as ImportPlan ?? throw new ArgumentException("Wrong Magpie preview."), backup))));
                }
            }
            catch { Dispose(); throw; }
        }
        static string SourcePath(ImportSource source) => Path.Combine(Paths.ConfigPath, SourceImport.FileName(source));
        static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(bytes)); }
        static string Read(string path, out string hash)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 1024 * 1024) throw new InvalidDataException("Import source exceeds 1 MiB.");
            using var memory = new MemoryStream(); stream.CopyTo(memory);
            var bytes = memory.ToArray(); hash = Hash(bytes);
            using var reader = new StreamReader(new MemoryStream(bytes)); return reader.ReadToEnd();
        }
        string TargetHash()
        {
            string disk = File.Exists(config.ConfigFilePath) ? Hash(File.ReadAllBytes(config.ConfigFilePath)) : "missing";
            var entries = config.Keys.OrderBy(key => key.Section, StringComparer.Ordinal).ThenBy(key => key.Key, StringComparer.Ordinal)
                .Select(key => key.Section + "." + key.Key + "=" + TomlTypeConverter.ConvertToString(config[key].BoxedValue, config[key].SettingType));
            return Hash(Encoding.UTF8.GetBytes(disk + "\n" + string.Join("\n", entries)));
        }
        void AuthorityAdmission()
        {
            if (disposed || !Authority.IsReady || !Authority.Session.IsAuthority || Authority.Session.Role == SessionRole.RemoteClient)
                throw new InvalidOperationException("Preview and apply Magpie imports on the current authority.");
        }
        ImportPlan Preview(ImportSource source)
        {
            AuthorityAdmission();
            var session = Authority.Session;
            var snapshot = new Snapshot { Generation = session.Generation, World = session.WorldId, Session = session.SessionId,
                Role = session.Role, TargetHash = TargetHash() };
            var plan = SourceImport.Plan(source, Read(SourcePath(source), out snapshot.SourceHash));
            Values(plan, out _, requirePermission: false);
            Admission(plan, snapshot);
            previews.Add(plan, snapshot);
            return plan;
        }
        void Admission(ImportPlan plan, Snapshot snapshot)
        {
            AuthorityAdmission(); var session = Authority.Session;
            if (snapshot.Consumed || snapshot.Generation != session.Generation || snapshot.World != session.WorldId ||
                snapshot.Session != session.SessionId || snapshot.Role != session.Role || snapshot.TargetHash != TargetHash())
                throw new InvalidOperationException("Import target or session changed; preview again.");
            Read(SourcePath(plan.Source), out string sourceHash);
            if (sourceHash != snapshot.SourceHash) throw new InvalidOperationException("Import source changed; preview again.");
        }
        Dictionary<string, object> Values(ImportPlan plan, out List<ConfigEntryBase> entries, bool requirePermission = true)
        {
            entries = new List<ConfigEntryBase>();
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in plan.Values)
            {
                var spec = MagpieSettings.All.Single(setting => setting.Id == pair.Key);
                var entry = settings.Entry(spec);
                if (!MeadworksConfig.TryGetMetadata(entry, out var metadata) || metadata.ProductId != Plugin.Guid ||
                    requirePermission && (!metadata.Decision.Allowed || metadata.Decision.RequiresServerAmendment || settings.IsLocked(spec)))
                    throw new InvalidOperationException("Import setting is blocked: " + pair.Key);
                object value = spec.Shortcut ? KeyboardShortcut.Deserialize((string)pair.Value) : pair.Value;
                if (!entry.SettingType.IsInstanceOfType(value) || entry.Description.AcceptableValues != null && !entry.Description.AcceptableValues.IsValid(value))
                    throw new ArgumentException("Invalid imported value: " + pair.Key);
                entries.Add(entry); values.Add(pair.Key, value);
            }
            ((IValidatedConversionTarget)MeadworksConversion.Target(config)).Validate(values);
            return values;
        }
        object Apply(ImportPlan plan, bool backup)
        {
            if (!backup || !previews.TryGetValue(plan, out var snapshot)) throw new InvalidOperationException("Current preview and backup required.");
            Admission(plan, snapshot);
            var values = Values(plan, out var entries);
            if (entries.Count == 0) return string.Format(CultureInfo.CurrentCulture, MagpieText.Text(MagpieTextKeys.ImportDone), 0);
            var target = MeadworksConversion.Target(config);
            bool savedLocally = false;
            ConfigAmendResult result;
            try
            {
                result = MeadworksConfig.AmendBatch(Plugin.Guid, entries, () =>
                {
                    Admission(plan, snapshot); Values(plan, out _);
                    target.Backup(); target.WriteAtomic(values);
                    savedLocally = true; snapshot.Consumed = true;
                }, "Magpie import");
            }
            catch (Exception error) when (savedLocally)
            {
                throw new IOException(MagpieText.Text(MagpieTextKeys.ImportSyncIncomplete), error);
            }
            if (!result.Applied) throw new InvalidOperationException("Magpie import was refused before writing.");
            return string.Format(CultureInfo.CurrentCulture, MagpieText.Text(MagpieTextKeys.ImportDone), values.Count);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var item in registrations) item.Dispose();
            registrations.Clear();
        }
    }
}
