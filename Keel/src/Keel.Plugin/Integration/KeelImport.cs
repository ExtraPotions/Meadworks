// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Reuses Timber ReferenceImport's C09 conversion, preview fingerprints and C01 amendment batch.
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
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Catalog;
using ExtraPotions.Keel.Conversion;
using ExtraPotions.Keel.Localization;
using ExtraPotions.Keel.Tuning;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Keel.Runtime
{
    public sealed class KeelImport : IDisposable
    {
        public const string Section = "Import";
        readonly ConfigFile config;
        readonly KeelConfig settings;
        readonly MeadworksLog log;
        readonly ConfigEntry<string> marker, pending;
        readonly List<IDisposable> registrations = new List<IDisposable>();
        readonly int thread = Thread.CurrentThread.ManagedThreadId;
        bool disposed;
        static string Id(ConfigEntryBase entry) => entry.Definition.Section + "." + entry.Definition.Key;
        ConfigEntryBase Entry(string id) => config[config.Keys.Single(k => k.Section + "." + k.Key == id)];
        static string SourcePath(ImportSource source) => Path.Combine(Paths.ConfigPath, SourceImport.FileName(source));

        public KeelImport(ConfigFile config, KeelConfig settings, MeadworksLog log)
        {
            this.config = config; this.settings = settings; this.log = log;
            marker = MeadworksConfig.BindHidden(config, Section, "Imported Sources", "", "Managed by Keel: sources already imported.");
            pending = MeadworksConfig.BindHidden(config, Section, "Pending Ship Values", "", "Managed imported per-ship values waiting for discovery.", ConfigScope.ServerAuthoritative);
            foreach (ImportSource source in Enum.GetValues(typeof(ImportSource)))
            {
                var s = source;
                var from = s == ImportSource.ShipConfig ? Integration.ModuleStepAside.ShipConfig
                    : s == ImportSource.GrindstoneSkills ? Integration.ModuleStepAside.GrindstoneSkills : Integration.ModuleStepAside.TheGreatestShips;
                registrations.Add(MeadworksConversion.Register(new ConversionRegistration("keel." + s.ToString().ToLowerInvariant(), Plugin.Guid,
                    from.Guid, from.Name, () => !disposed && File.Exists(SourcePath(s)) && !marker.Value.Split(',').Contains(s.ToString()),
                    () => Preview(s), (document, resolutions, backup) => Apply(document as PreviewDocument ?? throw new ArgumentException("Wrong Keel preview."), resolutions, backup))));
            }
        }
        void Check()
        {
            if (disposed || thread != Thread.CurrentThread.ManagedThreadId || !Plugin.Ready || !CruciblePlugin.IsReady)
                throw new InvalidOperationException("Keel importer is unavailable on this thread.");
        }
        static string Read(string path, out string hash)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 1024 * 1024) throw new IOException("Reference config exceeds 1 MiB.");
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            stream.Position = 0;
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
            return reader.ReadToEnd();
        }
        string TargetHash()
        {
            var disk = File.Exists(config.ConfigFilePath) ? Read(config.ConfigFilePath, out _) : "missing";
            var values = string.Join("\n", config.Keys.OrderBy(k => k.Section, StringComparer.Ordinal).ThenBy(k => k.Key, StringComparer.Ordinal)
                .Select(k => k.Section + "." + k.Key + "=" + config[k].GetSerializedValue()));
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(disk + "\n" + values))).Replace("-", "");
        }
        public PreviewDocument Preview(ImportSource source)
        {
            Check();
            var text = Read(SourcePath(source), out var sourceHash);
            var plan = SourceImport.Plan(source, text);
            var native = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var value in plan.Values)
            {
                var setting = KeelSettings.Find(value.SettingId);
                if (setting == null) continue;
                var entry = settings.Entry(setting);
                native[Id(entry)] = entry.SettingType == typeof(KeyboardShortcut)
                    ? KeyboardShortcut.Deserialize(Convert.ToString(value.Value, CultureInfo.InvariantCulture)) : value.Value;
            }
            if (plan.ShipValues.Count > 0)
            {
                var registry = new ShipRegistry(); PendingShipValues.Load(pending.Value, registry);
                var deferred = false;
                foreach (var value in plan.ShipValues)
                {
                    var sourceText = NativeText(value);
                    var definition = new ConfigDefinition(ShipFields.Section, ShipFields.Key(value.Prefab, value.Field));
                    if (config.Keys.Contains(definition))
                    {
                        var entry = config[definition];
                        object imported = TomlTypeConverter.ConvertToValue(sourceText, entry.SettingType);
                        if (entry.Description.AcceptableValues != null) imported = entry.Description.AcceptableValues.Clamp(imported);
                        native[Id(entry)] = imported;
                    }
                    else { registry.AddPending(value.Prefab, value.Field, sourceText); deferred = true; }
                }
                if (deferred) native[Id(pending)] = PendingShipValues.Encode(registry.Pending());
            }
            if (native.Count > 0) native[Id(marker)] = string.Join(",", marker.Value.Split(',').Where(p => p.Length > 0).Append(source.ToString()).Distinct());
            var rules = new List<ConversionRule>(); var values = new List<KeyValuePair<string, string>>();
            foreach (var pair in native)
            {
                var entry = Entry(pair.Key); var key = "source." + pair.Key;
                rules.Add(new ConversionRule(key, pair.Key, entry.SettingType.Name, actionBinding: entry.SettingType == typeof(KeyboardShortcut)));
                values.Add(new KeyValuePair<string, string>(key, TomlTypeConverter.ConvertToString(pair.Value, entry.SettingType)));
            }
            var converter = new ConfigConverter(new ConversionMapping(1, source.ToString(), 0, rules));
            var document = new PreviewDocument(this, plan, converter, converter.Preview(values), sourceHash, TargetHash(), CruciblePlugin.Session.Generation);
            var report = Path.Combine(Paths.ConfigPath, "Meadworks", "keel", "import-" + source.ToString().ToLowerInvariant() + ".txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            File.WriteAllText(report, document.ToString(), new UTF8Encoding(false));
            return document;
        }
        static string NativeText(ShipImportValue value)
        {
            if (value.Unit == ShipImportUnit.Absolute) return value.Text;
            var type = ShipCatalog.All.Single(t => t.Prefab == value.Prefab);
            float baseline;
            bool found = value.Unit == ShipImportUnit.BaseHullSpeedRatio
                ? KeelCatalog.TryVanillaSailForce(type.BaseHull, out baseline)
                : KeelCatalog.TryVanillaRudderSpeed(type.BaseHull, out baseline);
            if (!found) throw new InvalidOperationException("Vanilla hull facts are not ready for " + value.Source + "; preview after the game prefabs load.");
            return value.Resolve(baseline).ToString("R", CultureInfo.InvariantCulture);
        }
        void Editable(ConfigEntryBase entry)
        {
            if (settings.PlayerLocks?.IsLocked(entry) == true) throw new InvalidOperationException("Server-locked Local setting cannot be imported: " + Id(entry));
            if (!MeadworksConfig.TryGetMetadata(entry, out var data) || data.ProductId != Plugin.Guid || !data.Decision.Allowed || data.Decision.RequiresServerAmendment)
                throw new InvalidOperationException("Apply on the authority machine while this target is editable: " + Id(entry));
        }
        void Admission(PreviewDocument document)
        {
            Check();
            if (!ReferenceEquals(document.Owner, this)) throw new ArgumentException("Preview belongs to another importer.");
            Read(SourcePath(document.Plan.Source), out var hash);
            if (document.Generation != CruciblePlugin.Session.Generation || document.SourceSha256 != hash || document.TargetSha256 != TargetHash())
                throw new InvalidOperationException("Source, target or session changed; preview again.");
            if (document.Conversion.Errors.Count != 0) throw new ArgumentException("Preview contains invalid values.");
            foreach (var value in document.Conversion.Values) Editable(Entry(value.TargetId));
        }
        string Apply(PreviewDocument document, IReadOnlyDictionary<string,string> resolutions, bool backup)
        {
            if (!backup) throw new InvalidOperationException("Keel import requires a backup.");
            Admission(document);
            if (document.Conversion.Values.Count == 0) return "No mapped changes.";
            var choices = resolutions.Where(p => !p.Key.StartsWith("binding:", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value);
            var bindings = resolutions.Where(p => p.Key.StartsWith("binding:", StringComparison.Ordinal)).ToDictionary(p => p.Key.Substring(8), p => p.Value);
            ConversionResult? result = null;
            var amended = MeadworksConfig.AmendBatch(Plugin.Guid, document.Conversion.Values.Select(v => Entry(v.TargetId)).Distinct().ToArray(), () =>
            {
                Admission(document);
                result = document.Converter.Apply(document.Conversion, new CheckedTarget(this, document), choices, bindings);
            });
            if (!amended.Applied || result == null) throw new InvalidOperationException("Current authority or synchronization refused the import.");
            var message = KeelText.Format(KeelTextKeys.ImportDone, result.Written, document.Plan.Source.ToString()); log.Info(message); return message;
        }
        sealed class CheckedTarget : IValidatedConversionTarget
        {
            readonly KeelImport owner; readonly PreviewDocument preview; readonly IConversionTarget target;
            internal CheckedTarget(KeelImport owner, PreviewDocument preview) { this.owner = owner; this.preview = preview; target = MeadworksConversion.Target(owner.config); }
            public int SchemaVersion => target.SchemaVersion;
            public void Validate(IReadOnlyDictionary<string,object> values)
            {
                owner.Admission(preview); ((IValidatedConversionTarget)target).Validate(values);
                foreach (var pair in values)
                {
                    var entry = owner.Entry(pair.Key);
                    object value = entry.SettingType.IsInstanceOfType(pair.Value) ? pair.Value : TomlTypeConverter.ConvertToValue(Convert.ToString(pair.Value, CultureInfo.InvariantCulture), entry.SettingType);
                    if (entry.Description.AcceptableValues != null && !entry.Description.AcceptableValues.IsValid(value)) throw new ArgumentException("Invalid target: " + pair.Key);
                }
            }
            public void Backup() { owner.Admission(preview); if (!File.Exists(owner.config.ConfigFilePath)) throw new IOException("Save Keel config before importing."); target.Backup(); }
            public void WriteAtomic(IReadOnlyDictionary<string,object> values) { owner.Admission(preview); target.WriteAtomic(values); }
        }
        // Discovery consumes the carrier only on the authority, together with the actual bound entries.
        public void ApplyPending(string prefab, IReadOnlyDictionary<ShipField,ConfigEntryBase> entries)
        {
            Check();
            if (CruciblePlugin.Session.Role == SessionRole.RemoteClient || !MeadworksConfig.CanEdit(ConfigScope.ServerAuthoritative)) return;
            var registry = new ShipRegistry(); PendingShipValues.Load(pending.Value, registry); var taken = registry.TakePending(prefab);
            if (taken.Count == 0) return;
            var values = new Dictionary<string,object>(StringComparer.Ordinal);
            foreach (var pair in taken)
            {
                if (!entries.TryGetValue(pair.Key, out var entry)) throw new InvalidOperationException("Pending ship field is not bound.");
                Editable(entry); object value = TomlTypeConverter.ConvertToValue(pair.Value, entry.SettingType);
                if (entry.Description.AcceptableValues != null) value = entry.Description.AcceptableValues.Clamp(value);
                values.Add(Id(entry), value);
            }
            Editable(pending); values.Add(Id(pending), PendingShipValues.Encode(registry.Pending()));
            var target = MeadworksConversion.Target(config); ((IValidatedConversionTarget)target).Validate(values);
            var amended = MeadworksConfig.AmendBatch(Plugin.Guid, values.Keys.Select(Entry).ToArray(), () =>
            {
                foreach (var key in values.Keys) Editable(Entry(key));
                target.Backup(); target.WriteAtomic(values);
            });
            if (!amended.Applied) throw new InvalidOperationException("Pending ship import was refused; its values remain pending.");
        }
        public sealed class PreviewDocument
        {
            internal PreviewDocument(KeelImport owner, ImportPlan plan, ConfigConverter converter, ConversionPreview conversion, string source, string target, long generation)
            { Owner=owner; Plan=plan; Converter=converter; Conversion=conversion; SourceSha256=source; TargetSha256=target; Generation=generation; }
            internal KeelImport Owner { get; }
            internal ConfigConverter Converter { get; }
            internal long Generation { get; }
            public ImportPlan Plan { get; }
            public ConversionPreview Conversion { get; }
            public string SourceSha256 { get; }
            public string TargetSha256 { get; }
            public override string ToString() => Plan.Report() + "\nActual target values (after native bounds):\n"
                + string.Join("\n", Conversion.Values.Where(v => v.TargetId != Section + ".Imported Sources")
                    .Select(v => v.TargetId + " = " + Convert.ToString(v.Value, CultureInfo.InvariantCulture)))
                + "\n" + string.Join("\n", Conversion.Errors.Concat(Conversion.BindingConflicts.Select(c => "Binding collision: " + c.TargetId)))
                + "\nSource SHA256: " + SourceSha256 + "\nTarget SHA256: " + TargetSha256;
        }
        public void Dispose() { if (disposed) return; disposed=true; foreach (var item in registrations) item.Dispose(); registrations.Clear(); }
    }
}
