// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Inscribe.Conversion;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Meadworks.Crucible;
using YamlDotNet.RepresentationModel;
using Authority = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Inscribe.Runtime
{
    internal sealed class InscribeSourceConversion : IDisposable
    {
        private sealed class Plan
        {
            internal bool Consumed;
            internal long Generation;
            internal SessionRole Role;
            internal string TargetState = "", RulePaths = "";
            internal readonly Dictionary<string, string> Hashes = new Dictionary<string, string>();
            internal readonly Dictionary<string, object> Settings = new Dictionary<string, object>();
            internal readonly Dictionary<string, string> Files = new Dictionary<string, string>();
            internal readonly List<string> Notes = new List<string>();
            public override string ToString() => string.Join("\n", Notes.Concat(Settings.Select(pair => "Set " + pair.Key + " = " + pair.Value)).Concat(Files.Keys.Select(path => "Write " + path)));
        }
        private readonly ConfigFile config;
        private readonly List<IDisposable> registrations = new List<IDisposable>();
        private bool disposed;
        internal InscribeSourceConversion(ConfigFile config)
        {
            this.config = config;
            // ItemEnhancements' config and YAML imports were removed with enhancement (0.2.0).
            var selected = ImportSource.EliteCraftingConfig; Plan? current = null;
            registrations.Add(MeadworksConversion.Register(new ConversionRegistration("inscribe." + selected, Plugin.Guid, "com.EliteCrafting", selected.ToString(),
                () => !disposed && Authority.Session.IsAuthority && File.Exists(Path.Combine(Paths.ConfigPath, SourceImport.FileName(selected))),
                () => current = PreviewConfig(selected), (document, resolutions, backup) => Apply(document, current, backup))));
        }
        private static string HashBytes(byte[] bytes) { using var digest = SHA256.Create(); return Convert.ToBase64String(digest.ComputeHash(bytes)); }
        private static string Hash(string path) => File.Exists(path) ? HashBytes(File.ReadAllBytes(path)) : "missing";
        private string Read(Plan plan, string path)
        {
            var bytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
            string observed = bytes == null ? "missing" : HashBytes(bytes);
            if (plan.Hashes.TryGetValue(path, out var expected) && expected != observed) throw new InvalidOperationException("File changed while validating preview: " + path);
            plan.Hashes[path] = observed;
            if (bytes == null) return "";
            using var reader = new StreamReader(new MemoryStream(bytes)); return reader.ReadToEnd();
        }
        private static string RulePaths() => string.Join("\n", Directory.GetFiles(Paths.ConfigPath, "Inscribe.*.yml").OrderBy(path => path, StringComparer.Ordinal));
        private string TargetState() => string.Join("\n", config.Keys.OrderBy(key => key.Section, StringComparer.Ordinal).ThenBy(key => key.Key, StringComparer.Ordinal)
            .Select(key => key.Section + "." + key.Key + "=" + TomlTypeConverter.ConvertToString(config[key].BoxedValue, config[key].SettingType)));
        private Plan NewPlan()
        {
            if (disposed || !Authority.IsReady || !Authority.Session.IsAuthority || Authority.Session.Role == SessionRole.RemoteClient)
                throw new InvalidOperationException("Authority required for import preview.");
            var plan = new Plan { Generation = Authority.Session.Generation, Role = Authority.Session.Role, TargetState = TargetState(), RulePaths = RulePaths() };
            plan.Hashes[config.ConfigFilePath] = Hash(config.ConfigFilePath); return plan;
        }
        private void SessionAdmission(Plan plan)
        {
            if (disposed || !Authority.IsReady || !Authority.Session.IsAuthority || Authority.Session.Role == SessionRole.RemoteClient
                || Authority.Session.Generation != plan.Generation || Authority.Session.Role != plan.Role)
                throw new InvalidOperationException("Import authority or session changed; preview again.");
        }
        private void Admission(Plan plan)
        {
            SessionAdmission(plan);
            if (plan.Consumed || TargetState() != plan.TargetState || RulePaths() != plan.RulePaths) throw new InvalidOperationException("Import preview was consumed or target values changed; preview again.");
            foreach (var hash in plan.Hashes) if (Hash(hash.Key) != hash.Value) throw new InvalidOperationException("A source or destination changed; preview again.");
        }
        private List<ConfigEntryBase> Entries(IReadOnlyDictionary<string, object> values)
        {
            var entries = new List<ConfigEntryBase>();
            foreach (var pair in values)
            {
                var setting = Config.InscribeSettings.All.SingleOrDefault(value => value.Id == pair.Key)
                    ?? throw new InvalidOperationException("Unknown Inscribe import setting: " + pair.Key);
                var entry = Plugin.Settings!.Entry(setting);
                if (!MeadworksConfig.TryGetMetadata(entry, out var metadata) || metadata.ProductId != Plugin.Guid
                    || !metadata.Decision.Allowed || metadata.Decision.RequiresServerAmendment)
                    throw new InvalidOperationException("Import setting is blocked: " + pair.Key);
                object value = entry.SettingType.IsInstanceOfType(pair.Value) ? pair.Value
                    : TomlTypeConverter.ConvertToValue(Convert.ToString(pair.Value, CultureInfo.InvariantCulture), entry.SettingType);
                if (value == null || entry.Description.AcceptableValues != null && !entry.Description.AcceptableValues.IsValid(value))
                    throw new InvalidOperationException("Invalid Inscribe import setting: " + pair.Key);
                entries.Add(entry);
            }
            return entries;
        }
        private static void Add(Plan plan, ImportPlan import) { foreach (var value in import.Values) plan.Settings[value.SettingId] = value.Value; plan.Notes.AddRange(import.Notes.Select(note => note.ToString())); if (import.LogOverride != null) plan.Notes.Add("Log level request: " + import.LogOverride + " (set through Crucible logging)"); }
        private Plan PreviewConfig(ImportSource source)
        {
            var plan = NewPlan(); Add(plan, SourceImport.Plan(source, Read(plan, Path.Combine(Paths.ConfigPath, SourceImport.FileName(source)))));
            ((IValidatedConversionTarget)MeadworksConversion.Target(config)).Validate(plan.Settings); return plan;
        }
        private void Validate(Plan plan)
        {
            ((IValidatedConversionTarget)MeadworksConversion.Target(config)).Validate(plan.Settings);
            foreach (var spec in new[] { FamilySpec.Economy })
            {
                if (!plan.Files.Keys.Any(path => Path.GetFileName(path).StartsWith(spec.Prefix, StringComparison.Ordinal))) continue;
                var sources = Directory.GetFiles(Paths.ConfigPath, spec.Prefix + "*.yml").Where(path => !plan.Files.ContainsKey(path)).Select(path => new SourceText(Path.GetFileName(path), Read(plan, path))).ToList();
                sources.AddRange(plan.Files.Where(pair => Path.GetFileName(pair.Key).StartsWith(spec.Prefix, StringComparison.Ordinal)).Select(pair => new SourceText(Path.GetFileName(pair.Key), pair.Value)));
                sources = sources.OrderBy(source => source.Name == spec.MainFile ? 0 : 1).ThenBy(source => source.Name, StringComparer.OrdinalIgnoreCase).ToList();
                var issues = new RuleIssues(); var merged = FamilyBuilder.Merge(spec, sources, issues);
                EconomyParser.Parse(merged, issues);
                if (issues.HasErrors) throw new FormatException(string.Join("; ", issues.Errors));
            }
        }
        private object Apply(object document, Plan? current, bool backup)
        {
            if (!backup || current == null || !ReferenceEquals(document, current)) throw new InvalidOperationException("Current preview and backups required.");
            Admission(current); Validate(current); var entries = Entries(current.Settings);
            if (entries.Count == 0 && current.Files.Count == 0) return "No settings or files to import.";
            var target = MeadworksConversion.Target(config);
            var previous = current.Files.ToDictionary(pair => pair.Key, pair => File.Exists(pair.Key) ? File.ReadAllBytes(pair.Key) : null);
            var oldSettings = current.Settings.Keys.ToDictionary(key => key, key => Plugin.Settings!.Entry(Config.InscribeSettings.All.Single(setting => setting.Id == key)).BoxedValue);
            var writtenFiles = new Dictionary<string, string>();
            var writtenSettings = new Dictionary<string, object>();
            bool savedLocally = false, publicationIncomplete = false;
            string savedConfigHash = "";
            void RecoveryAdmission()
            {
                SessionAdmission(current); Entries(oldSettings);
                if (!savedLocally) return;
                if (Hash(config.ConfigFilePath) != savedConfigHash) throw new InvalidOperationException("Saved configuration changed before recovery; backups retained.");
                foreach (var pair in writtenSettings)
                    if (!Equals(Plugin.Settings!.Entry(Config.InscribeSettings.All.Single(setting => setting.Id == pair.Key)).BoxedValue, pair.Value))
                        throw new InvalidOperationException("Imported settings changed before recovery; backups retained.");
            }
            try
            {
                Admission(current); Entries(current.Settings);
                foreach (var pair in current.Files)
                {
                    if (Hash(pair.Key) != current.Hashes[pair.Key]) throw new InvalidOperationException("Import destination changed before its write.");
                    if (File.Exists(pair.Key)) File.Copy(pair.Key, pair.Key + ".import-" + DateTime.UtcNow.Ticks + ".bak", false);
                    WriteFile(pair.Key, pair.Value);
                    writtenFiles.Add(pair.Key, HashBytes(new UTF8Encoding(false).GetBytes(pair.Value)));
                }
                if (entries.Count > 0)
                {
                    ConfigAmendResult result;
                    try
                    {
                        result = MeadworksConfig.AmendBatch(Plugin.Guid, entries, () =>
                        {
                            SessionAdmission(current); Entries(current.Settings);
                            foreach (var hash in current.Hashes)
                                if (!writtenFiles.ContainsKey(hash.Key) && Hash(hash.Key) != hash.Value)
                                    throw new InvalidOperationException("A source or destination changed before import write.");
                            foreach (var file in writtenFiles)
                                if (Hash(file.Key) != file.Value) throw new InvalidOperationException("An imported file changed before config write.");
                            if (TargetState() != current.TargetState || Hash(config.ConfigFilePath) != current.Hashes[config.ConfigFilePath])
                                throw new InvalidOperationException("Target settings changed before import write.");
                            target.Backup(); target.WriteAtomic(current.Settings);
                            savedLocally = true; current.Consumed = true; savedConfigHash = Hash(config.ConfigFilePath);
                            foreach (var pair in current.Settings)
                                writtenSettings.Add(pair.Key, Plugin.Settings!.Entry(Config.InscribeSettings.All.Single(setting => setting.Id == pair.Key)).BoxedValue);
                        }, "Inscribe import");
                    }
                    catch (Exception error) when (savedLocally)
                    {
                        publicationIncomplete = true;
                        throw new IOException(InscribeText.Text("import_sync_incomplete"), error);
                    }
                    if (!result.Applied) throw new InvalidOperationException("Inscribe config import was refused before its write.");
                }
                SessionAdmission(current); current.Consumed = true;
                var rules = ActiveRules.ReloadLocal();
                SessionAdmission(current);
                if (savedLocally) savedConfigHash = Hash(config.ConfigFilePath);
                if (rules.Economy == FamilyReload.Rejected) throw new InvalidOperationException("Rules refused import; restoring the prior files and settings.");
                current.Consumed = true;
                current.Notes.Add("Applied with backups. Source files retained."); return current.ToString();
            }
            catch (Exception original)
            {
                if (publicationIncomplete) throw;
                try
                {
                    RecoveryAdmission();
                    foreach (var pair in writtenFiles)
                        if (Hash(pair.Key) != pair.Value) throw new InvalidOperationException("An imported file changed before recovery; backups retained.");
                    foreach (var pair in writtenFiles)
                    {
                        var bytes = previous[pair.Key];
                        if (bytes == null) File.Delete(pair.Key); else WriteBytes(pair.Key, bytes);
                    }
                    if (savedLocally)
                    {
                        var recovered = MeadworksConfig.AmendBatch(Plugin.Guid, entries, () =>
                        {
                            RecoveryAdmission(); target.WriteAtomic(oldSettings);
                        }, "Inscribe import recovery");
                        if (!recovered.Applied) throw new InvalidOperationException("Config recovery publication was refused.");
                    }
                    ActiveRules.ReloadLocal();
                }
                catch (Exception recovery)
                {
                    current.Consumed = true;
                    throw new IOException(InscribeText.Text("import_recovery_incomplete"), new AggregateException(original, recovery));
                }
                throw;
            }
        }
        private static void WriteFile(string destination, string text) => WriteBytes(destination, new UTF8Encoding(false).GetBytes(text));
        internal static void WriteBytes(string destination, byte[] bytes)
        {
            string temporary = destination + ".import-" + Guid.NewGuid().ToString("N") + ".tmp";
            bool owned = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    owned = true;
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                if (File.Exists(destination)) File.Replace(temporary, destination, null); else File.Move(temporary, destination);
                owned = false;
            }
            finally { if (owned && File.Exists(temporary)) File.Delete(temporary); }
        }
        public void Dispose() { disposed = true; foreach (var registration in registrations) registration.Dispose(); registrations.Clear(); }
    }
}
