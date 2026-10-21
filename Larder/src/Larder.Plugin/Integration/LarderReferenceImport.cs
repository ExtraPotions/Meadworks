// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Product mapping and frozen-preview admission over Crucible C09/AS03; source files remain read-only.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Larder.Runtime
{
    public sealed class LarderImportPreview
    {
        internal LarderImportPreview(LarderReferenceImport owner, string source, ConfigConverter converter,
            ConversionPreview plan, string sourceHash, string targetHash, long generation,
            IEnumerable<string> errors, IEnumerable<string> normalizations)
        {
            Owner = owner; Source = source; Converter = converter; Preview = plan;
            SourceSha256 = sourceHash; TargetSha256 = targetHash; Generation = generation;
            Errors = Array.AsReadOnly(errors.Concat(plan.Errors).ToArray());
            Normalizations = Array.AsReadOnly(normalizations.ToArray());
        }
        internal LarderReferenceImport Owner { get; }
        internal ConfigConverter Converter { get; }
        internal long Generation { get; }
        public string Source { get; }
        public ConversionPreview Preview { get; }
        public string SourceSha256 { get; }
        public string TargetSha256 { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Normalizations { get; }
        public IReadOnlyList<ConversionValue> Values => Preview.Values;
        public IReadOnlyList<ConversionConflict> Conflicts => Preview.Conflicts;
        public override string ToString() => string.Join("\n", new[] { LarderText.Format("import_review", Source) }
            .Concat(Values.Select(v => v.SourceId + " → " + v.TargetId + " = " + Convert.ToString(v.Value, CultureInfo.InvariantCulture)))
            .Concat(Normalizations.Select(n => "Normalized: " + n)).Concat(Errors.Select(e => "Invalid: " + e))
            .Concat(Preview.Unknown.Select(e => "Skipped: " + e)).Concat(Preview.Metadata)
            .Concat(Conflicts.Select(c => "Choose source for " + c.TargetId + ": " + string.Join(", ", c.Choices.Select(v => v.SourceId))))
            .Concat(new[] { "Source SHA256: " + SourceSha256, "Target SHA256: " + TargetSha256 }));
    }

    public sealed class LarderReferenceImport : IDisposable
    {
        readonly ConfigFile config;
        readonly MeadworksLog log;
        readonly int thread = Thread.CurrentThread.ManagedThreadId;
        readonly List<IDisposable> registrations = new List<IDisposable>();
        readonly ConfigEntry<bool> previewFm, previewGs, applyFm, applyGs;
        readonly ConfigEntry<string> choices;
        bool disposed, offered;
        public string ReportPath => Path.Combine(Paths.ConfigPath, "Meadworks", "larder", "import-report.txt");
        public LarderReferenceImport(ConfigFile config, MeadworksLog log)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config)); this.log = log;
            previewFm = MeadworksConfig.Bind(config, "Import", "Preview FeastMaster", false, "Preview the existing reference file; source unchanged. Resets after running.");
            previewGs = MeadworksConfig.Bind(config, "Import", "Preview GrindstoneSkills", false, "Preview the existing Cooking reference settings; source unchanged. Resets after running.");
            applyFm = MeadworksConfig.Bind(config, "Import", "Apply FeastMaster", false, "Applies the previewed FeastMaster import. Run it on the host; Larder backs up its settings first. The FeastMaster files are not changed. Turns itself off after running.");
            applyGs = MeadworksConfig.Bind(config, "Import", "Apply GrindstoneSkills", false, "Applies the previewed GrindstoneSkills import. Run it on the host; Larder backs up its settings first. Star settings (removed from Larder) are reported, not imported. Resets after running.");
            choices = MeadworksConfig.Bind(config, "Import", "Source Choices", "", "Conflicts shown by Preview: target Section.Key=source id, separated by semicolons.");
            Register("FeastMaster", "com.FeastMaster", "larder.feastmaster");
            Register("GrindstoneSkills", "com.GrindstoneSkills", "larder.grindstoneskills");
        }
        void Register(string source, string guid, string id) => registrations.Add(MeadworksConversion.Register(
            new ConversionRegistration(id, Plugin.Guid, guid, source, () => !disposed && File.Exists(SourcePath(source)),
                () => Preview(source), (document, resolutions, backups) =>
                {
                    if (!backups) throw new InvalidOperationException("Larder import requires a target backup.");
                    return Apply(document as LarderImportPreview ?? throw new ArgumentException("Wrong Larder preview."), resolutions);
                })));
        public static string SourcePath(string source)
        {
            if (source != "FeastMaster" && source != "GrindstoneSkills") throw new ArgumentException("Unknown reference source.");
            return Path.Combine(Paths.ConfigPath, "com." + source + ".cfg");
        }
        void Check()
        {
            if (disposed || Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Import requires its live owning thread.");
        }
        public LarderImportPreview Preview(string source)
        {
            Check(); string path = SourcePath(source), sourceHash = FileHash(path);
            if (sourceHash == "missing") throw new FileNotFoundException("Reference configuration is unavailable.", path);
            var raw = MeadworksConversion.ReadSourceConfig(path, source);
            if (FileHash(path) != sourceHash) throw new InvalidOperationException("Source changed while preparing preview.");
            var rules = new List<ConversionRule>(); var values = new List<KeyValuePair<string, string>>();
            var errors = new List<string>(); var normalizations = new List<string>();
            foreach (var pair in raw)
            {
                string prefix = source + ".";
                if (!pair.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string definition = pair.Key.Substring(prefix.Length);
                // Match actual bound section/key definitions, including dots in either part; no guessed first-dot split.
                var targets = config.Keys.Where(d => Matches(source, definition, d)).Take(2).ToArray();
                if (targets.Length > 1) { errors.Add(pair.Key + ": ambiguous bound target."); continue; }
                if (targets.Length == 0) { values.Add(pair); continue; }
                ConfigEntryBase entry = config[targets[0]];
                try
                {
                    string text = pair.Value;
                    if (definition.EndsWith(".Regen Per 10 Extra Stamina", StringComparison.Ordinal))
                        text = (float.Parse(text, CultureInfo.InvariantCulture) / 10f).ToString("R", CultureInfo.InvariantCulture);
                    object parsed = TomlTypeConverter.ConvertToValue(text, entry.SettingType);
                    if (parsed is float number && (float.IsNaN(number) || float.IsInfinity(number))) throw new FormatException("Nonfinite value.");
                    object normalized = entry.Description.AcceptableValues?.Clamp(parsed) ?? parsed;
                    string serialized = normalized is string s ? s : Convert.ToString(normalized, CultureInfo.InvariantCulture);
                    if (text != pair.Value || !Equals(parsed, normalized)) normalizations.Add(pair.Key + ": " + pair.Value + " → " + serialized);
                    rules.Add(new ConversionRule(pair.Key, targets[0].Section + "." + targets[0].Key,
                        entry.SettingType.Name, entry.SettingType.IsEnum ? string.Join(",", Enum.GetNames(entry.SettingType)) : ""));
                    values.Add(new KeyValuePair<string, string>(pair.Key, serialized));
                }
                catch (Exception error) { errors.Add(pair.Key + ": " + error.GetBaseException().Message); }
            }
            var converter = new ConfigConverter(new ConversionMapping(1, source, TargetSchema(), rules));
            return new LarderImportPreview(this, source, converter, converter.Preview(values), sourceHash, TargetHash(),
                CruciblePlugin.IsReady ? CruciblePlugin.Session.Generation : 0, errors, normalizations);
        }
        static readonly string[] FmRecovery = { "1. Health Regeneration", "2. Stamina Regeneration", "3. Eitr Regeneration", "1. Health Regen", "2. Stamina Regen", "3. Eitr Regen" };
        static readonly string[] FmExertion = { "4. Stamina Costs", "5. Base Values", "6. Skills", "7. World Rates" };
        static bool Matches(string source, string raw, ConfigDefinition target)
        {
            string key = target.Key;
            if (source == "GrindstoneSkills")
            {
                if (target.Section != "7 - Cooking") return false;
                return raw == "4 - Kitchen." + key || raw == "5 - Experience." + key;
            }
            if ((target.Section == "2 - Foods" || target.Section == "3 - Eating") && raw == "0. Global Settings." + key) return true;
            if (target.Section == "3 - Eating" && raw == "8. Display." + key) return true;
            if (target.Section == "4 - Recovery" && FmRecovery.Any(s => raw == s + "." + key)) return true;
            if (target.Section == "5 - Exertion" && FmExertion.Any(s => raw == s + "." + key)) return true;
            if (target.Section == "6 - Kitchen" && (raw == "9. Kitchen." + key || raw == "9. Fermenter." + key)) return true;
            if (target.Section.StartsWith("Food: ", StringComparison.Ordinal) || target.Section.StartsWith("Mead: ", StringComparison.Ordinal))
            {
                string prefab = target.Section.Substring(6);
                if (raw == prefab + "." + key || target.Section.StartsWith("Mead: ", StringComparison.Ordinal) && raw == "0Meads_" + prefab + "." + key) return true;
            }
            if (target.Section.StartsWith("Station: ", StringComparison.Ordinal) && raw == target.Section.Substring(9) + "." + key) return true;
            if (target.Section == "4 - Recovery")
            {
                if (key == "Continuous Food Healing" && raw == "1. Health Regeneration.Steady Regeneration") return true;
                if (key == "Count Food Stamina Only" && raw == "2. Stamina Regeneration.Extra Stamina From Food Only") return true;
                if (key == "Regen Per Extra Stamina Point" && raw == "2. Stamina Regeneration.Regen Per 10 Extra Stamina") return true;
            }
            return false;
        }
        int TargetSchema() => config.TryGetEntry<int>("Meadworks", "SchemaVersion", out var schema) ? schema.Value : 0;
        string TargetHash() => Hash(string.Join("\n", config.Keys.OrderBy(k => k.Section, StringComparer.Ordinal).ThenBy(k => k.Key, StringComparer.Ordinal)
            .Select(k => k.Section.Length + ":" + k.Section + k.Key.Length + ":" + k.Key + "=" + config[k].GetSerializedValue())) + "\n" + FileHash(config.ConfigFilePath));
        void Admission(LarderImportPreview document)
        {
            Check();
            if (document == null || !ReferenceEquals(document.Owner, this)) throw new ArgumentException("Preview belongs to another importer.");
            if (!CruciblePlugin.IsReady || !Plugin.Ready || CruciblePlugin.Session.Role == SessionRole.RemoteClient)
                throw new InvalidOperationException("Apply imports on the live authority machine.");
            if (document.Generation != CruciblePlugin.Session.Generation || document.SourceSha256 != FileHash(SourcePath(document.Source)) || document.TargetSha256 != TargetHash())
                throw new InvalidOperationException("Source, target or session changed; preview again.");
            foreach (var value in document.Values)
            {
                var entry = config[config.Keys.Single(k => k.Section + "." + k.Key == value.TargetId)];
                if (!MeadworksConfig.TryGetMetadata(entry, out var metadata) || metadata.ProductId != Plugin.Guid ||
                    !metadata.Decision.Allowed || metadata.Decision.RequiresServerAmendment)
                    throw new InvalidOperationException("Import target is locked or has no owning amendment path: " + value.TargetId);
            }
        }
        public ConversionResult Apply(LarderImportPreview document, IReadOnlyDictionary<string, string> sourceChoices)
        {
            Admission(document);
            if (document.Errors.Count != 0) throw new ArgumentException(string.Join("; ", document.Errors));
            return document.Converter.Apply(document.Preview, new Target(this, document), sourceChoices);
        }
        sealed class Target : IValidatedConversionTarget
        {
            readonly LarderReferenceImport owner; readonly LarderImportPreview preview; readonly IValidatedConversionTarget target;
            public Target(LarderReferenceImport owner, LarderImportPreview preview)
            { this.owner = owner; this.preview = preview; target = (IValidatedConversionTarget)MeadworksConversion.Target(owner.config); }
            public int SchemaVersion => target.SchemaVersion;
            public void Validate(IReadOnlyDictionary<string, object> values) { owner.Admission(preview); target.Validate(values); }
            public void Backup()
            {
                owner.Admission(preview);
                if (!File.Exists(owner.config.ConfigFilePath)) throw new InvalidOperationException("Save the target before importing; backup is required.");
                target.Backup();
            }
            public void WriteAtomic(IReadOnlyDictionary<string, object> values) { owner.Admission(preview); target.WriteAtomic(values); }
        }
        public void Tick()
        {
            Check();
            if (!offered && ObjectDB.instance != null && ObjectDB.instance.m_items.Count != 0)
            {
                offered = true;
                if (File.Exists(SourcePath("FeastMaster")) || File.Exists(SourcePath("GrindstoneSkills")))
                    log.Info("Reference settings are available. Preview Larder Import settings or use Almanac; source files remain intact.");
            }
            if (previewFm.Value || applyFm.Value) Run("FeastMaster", applyFm.Value);
            if (previewGs.Value || applyGs.Value) Run("GrindstoneSkills", applyGs.Value);
        }
        void Run(string source, bool write)
        {
            var report = new List<string>();
            try
            {
                var document = Preview(source); report.Add(document.ToString());
                if (write)
                {
                    var resolutions = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (string item in choices.Value.Split(';')) { int cut = item.IndexOf('='); if (cut > 0) resolutions.Add(item.Substring(0, cut).Trim(), item.Substring(cut + 1).Trim()); }
                    var result = Apply(document, resolutions); report.Add("Applied " + result.Written + "; target backup: " + result.BackedUp);
                }
            }
            catch (Exception error) { report.Add("Import stopped: " + error.GetType().Name + ": " + error.GetBaseException().Message); }
            finally
            {
                previewFm.Value = applyFm.Value = previewGs.Value = applyGs.Value = false;
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllLines(ReportPath, report);
            }
        }
        static string FileHash(string path)
        {
            if (!File.Exists(path)) return "missing";
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Configuration exceeds 1 MiB.");
            using (var input = File.OpenRead(path)) using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(input).Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
        }
        static string Hash(string text)
        { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("X2", CultureInfo.InvariantCulture))); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var registration in registrations) registration.Dispose(); registrations.Clear();
        }
    }
}
