// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Product mappings over the qualified C09 converter/atomic writer and C01 amendment batch.
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
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Timber.Runtime
{
    public sealed class ReferenceImport : IDisposable
    {
        static readonly Dictionary<string, (string File, string Guid, string Name)> Sources = new()
        {
            ["cartur"] = ("com.jekkle.valheim.carturbuildcamera.cfg", "com.jekkle.valheim.carturbuildcamera", "Cartur Build Camera"),
            ["azumatt"] = ("Azumatt.BuildCameraCHE.cfg", "Azumatt.BuildCameraCHE", "Build Camera Custom Hammers Edition"),
            ["flooring"] = ("com.jekkle.valheim.carturflooring.cfg", "com.jekkle.valheim.carturflooring", "Cartur Flooring"),
            ["openkeep"] = ("milkyteam.openkeep.cfg", "milkyteam.openkeep", "OpenKeep"),
        };
        readonly ConfigFile config;
        readonly List<IDisposable> registrations = new();
        readonly Dictionary<string, PreviewDocument> console = new(StringComparer.Ordinal);
        readonly int thread = Thread.CurrentThread.ManagedThreadId;
        bool disposed;
        public ReferenceImport(ConfigFile config)
        {
            this.config = config;
            foreach (var pair in Sources)
            {
                string id = pair.Key;
                registrations.Add(MeadworksConversion.Register(new ConversionRegistration("timber." + id, Plugin.Guid,
                    pair.Value.Guid, pair.Value.Name, () => !disposed && File.Exists(PathFor(id)), () => Preview(id),
                    (document, resolutions, backup) =>
                    {
                        if (!backup) throw new InvalidOperationException("Timber imports require a target backup.");
                        return Apply(document as PreviewDocument ?? throw new ArgumentException("Wrong Timber preview"), resolutions);
                    })));
            }
        }
        string PathFor(string id) => Path.Combine(Paths.ConfigPath, Sources[id].File);
        void Check()
        { if (disposed || thread != Thread.CurrentThread.ManagedThreadId || !Plugin.Ready) throw new InvalidOperationException("Timber importer is unavailable on this thread."); }
        public void Dispose()
        { if (disposed) return; disposed = true; foreach (var registration in registrations) registration.Dispose(); registrations.Clear(); console.Clear(); }

        static string Read(string path) => Read(path, out _);
        static string Read(string path, out string hash)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 1024 * 1024) throw new IOException("Reference config exceeds 1 MiB");
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            stream.Position = 0;
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
            return reader.ReadToEnd();
        }
        static string Hash(string text)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", ""); }
        string TargetHash() => Hash((File.Exists(config.ConfigFilePath) ? Read(config.ConfigFilePath) : "missing") + "\n" +
            string.Join("\n", config.Keys.OrderBy(k => k.Section, StringComparer.Ordinal).ThenBy(k => k.Key, StringComparer.Ordinal)
                .Select(k => k.Section + "." + k.Key + "=" + TomlTypeConverter.ConvertToString(config[k].BoxedValue, config[k].SettingType))));
        ConfigEntryBase Entry(string target) => config[config.Keys.Single(k => k.Section + "." + k.Key == target)];

        public PreviewDocument Preview(string id)
        {
            Check();
            if (!Sources.ContainsKey(id)) throw new ArgumentException("Use cartur, azumatt, flooring or openkeep");
            string text = Read(PathFor(id), out string sourceHash);
            var raw = CfgText.Read(text, "source").ToDictionary(p => p.Key.Substring(7), p => p.Value, StringComparer.Ordinal);
            var notes = new List<string>(); var errors = new List<string>(); var values = new Dictionary<string,string>(StringComparer.Ordinal);
            if (id == "cartur" || id == "azumatt")
            {
                var root = Hud.instance?.m_rootObject?.transform as RectTransform;
                foreach (var pair in ReferenceCameraValues.Map(raw, root != null ? root.rect.width : Screen.width,
                    root != null ? root.rect.height : Screen.height, notes, errors)) values.Add(pair.Key, pair.Value);
            }
            else if (id == "flooring")
            {
                foreach (var pair in new[] { ("FloorsAreRoofs", "Floors Are Roofs"), ("FiresOnFloors", "Fires On Floors"),
                    ("SmeltersOnFloors", "Smelters On Floors"), ("FloorsDontDecay", "Floors Dont Decay") })
                    if (raw.TryGetValue("General." + pair.Item1, out var value)) values.Add("Flooring." + pair.Item2, value);
            }
            else
            {
                bool skipped = false;
                foreach (var pair in raw)
                {
                    if (pair.Key.StartsWith("11. Build Camera.", StringComparison.Ordinal)) values["Build Camera." + pair.Key.Substring(17)] = pair.Value;
                    else if (pair.Key == "8. Homestead.Area Repair") values["Repair.Area Repair"] = pair.Value;
                    else if (pair.Key == "8. Homestead.Build On Wood") values["Flooring.Fires On Floors"] = (!string.IsNullOrWhiteSpace(pair.Value)).ToString();
                    // OpenKeep section 14 is a permanently excluded feature: dropped, reported once.
                    else if (pair.Key.StartsWith("14. ", StringComparison.Ordinal)) skipped = true;
                }
                if (skipped) notes.Add(TimberText.Text("import_openkeep_skipped"));
            }
            var rules = new List<ConversionRule>(); var settings = new List<KeyValuePair<string,string>>();
            var known = new HashSet<string>(config.Keys.Select(k => k.Section + "." + k.Key), StringComparer.Ordinal);
            foreach (var pair in values)
            {
                if (!known.Contains(pair.Key)) { notes.Add(TimberText.Format("import_unknown_target", pair.Key)); continue; }
                var entry = Entry(pair.Key); string value = pair.Value;
                try
                {
                    if (entry.SettingType == typeof(bool)) value = ReferenceCameraValues.Toggle(value).ToString();
                    object native = TomlTypeConverter.ConvertToValue(value, entry.SettingType);
                    if (entry.Description.AcceptableValues != null && !entry.Description.AcceptableValues.IsValid(native)) throw new FormatException("Outside Timber's accepted range");
                    if (Equals(native, entry.BoxedValue)) { notes.Add(TimberText.Format("import_unchanged", pair.Key, value)); continue; }
                }
                catch (Exception e) when (e is ArgumentException || e is FormatException || e is OverflowException) { errors.Add(pair.Key + ": " + e.Message); continue; }
                string type = entry.SettingType == typeof(KeyboardShortcut) ? "KeyboardShortcut" : entry.SettingType.IsEnum ? entry.SettingType.Name
                    : entry.SettingType == typeof(Vector2) || entry.SettingType == typeof(Vector3) ? "String" : entry.SettingType.Name;
                string constraints = entry.SettingType.IsEnum ? string.Join(",", Enum.GetNames(entry.SettingType)) : "";
                string source = id + "." + pair.Key;
                rules.Add(new ConversionRule(source, pair.Key, type, constraints, actionBinding: entry.SettingType == typeof(KeyboardShortcut)));
                settings.Add(new KeyValuePair<string,string>(source, value));
            }
            var converter = new ConfigConverter(new ConversionMapping(1, Sources[id].Name, 0, rules));
            return new PreviewDocument(this, id, converter, converter.Preview(settings), notes, errors, sourceHash, TargetHash(), CruciblePlugin.Session.Generation);
        }

        void Admission(PreviewDocument document)
        {
            Check();
            if (!ReferenceEquals(document.Owner, this)) throw new ArgumentException("Preview belongs to another Timber importer");
            Read(PathFor(document.Id), out string sourceHash);
            if (document.Generation != CruciblePlugin.Session.Generation || document.SourceSha256 != sourceHash || document.TargetSha256 != TargetHash())
                throw new InvalidOperationException("Source, target or session changed; preview again.");
            if (document.Errors.Count != 0) throw new ArgumentException("Preview contains invalid reference values");
            foreach (var value in document.Plan.Values)
            {
                var entry = Entry(value.TargetId);
                if (Plugin.PlayerLocks?.IsLocked(entry) == true)
                    throw new InvalidOperationException("Server-locked Local setting cannot be imported: " + value.TargetId);
                if (!MeadworksConfig.TryGetMetadata(entry, out var metadata) || metadata.ProductId != Plugin.Guid || !metadata.Decision.Allowed || metadata.Decision.RequiresServerAmendment)
                    throw new InvalidOperationException("Apply on the authority machine while this target is editable: " + value.TargetId);
            }
        }
        public string Apply(PreviewDocument document, IReadOnlyDictionary<string,string> resolutions)
        {
            Admission(document);
            if (document.Plan.Values.Count == 0) return TimberText.Text("import_no_changes");
            var choices = resolutions.Where(p => !p.Key.StartsWith("binding:", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value);
            var bindings = resolutions.Where(p => p.Key.StartsWith("binding:", StringComparison.Ordinal)).ToDictionary(p => p.Key.Substring(8), p => p.Value);
            ConversionResult? result = null;
            var amended = MeadworksConfig.AmendBatch(Plugin.Guid, document.Plan.Values.Select(v => Entry(v.TargetId)).Distinct().ToArray(), () =>
            {
                Admission(document);
                result = document.Converter.Apply(document.Plan, new CheckedTarget(this, document), choices, bindings);
            });
            if (!amended.Applied || result == null) throw new InvalidOperationException("Current authority or synchronization refused the import.");
            return TimberText.Format("import_applied", result.Written);
        }
        sealed class CheckedTarget : IValidatedConversionTarget
        {
            readonly ReferenceImport owner; readonly PreviewDocument preview; readonly IConversionTarget target;
            internal CheckedTarget(ReferenceImport owner, PreviewDocument preview) { this.owner = owner; this.preview = preview; target = MeadworksConversion.Target(owner.config); }
            public int SchemaVersion => target.SchemaVersion;
            public void Validate(IReadOnlyDictionary<string,object> values)
            {
                owner.Admission(preview);
                ((IValidatedConversionTarget)target).Validate(values);
                foreach (var pair in values)
                {
                    var entry = owner.Entry(pair.Key);
                    object native = entry.SettingType.IsInstanceOfType(pair.Value) ? pair.Value : TomlTypeConverter.ConvertToValue(Convert.ToString(pair.Value, CultureInfo.InvariantCulture), entry.SettingType);
                    if (entry.Description.AcceptableValues != null && !entry.Description.AcceptableValues.IsValid(native)) throw new ArgumentException("Invalid target: " + pair.Key);
                }
            }
            public void Backup()
            { owner.Admission(preview); if (!File.Exists(owner.config.ConfigFilePath)) throw new IOException("Save Timber config before importing"); target.Backup(); }
            public void WriteAtomic(IReadOnlyDictionary<string,object> values) { owner.Admission(preview); target.WriteAtomic(values); }
        }
        public IEnumerable<string> Command(string[] args)
        {
            try
            {
                string id = args.Length > 1 ? args[1].ToLowerInvariant() : "";
                if (!Sources.ContainsKey(id)) return new[] { "timber import cartur|azumatt|flooring|openkeep [apply]" };
                if (args.Length > 2 && args[2] == "apply")
                    return new[] { console.TryGetValue(id, out var current) ? Apply(current, new Dictionary<string,string>()) : TimberText.Text("import_preview_first") };
                var document = Preview(id); console[id] = document; return document.ToString().Split('\n');
            }
            catch (Exception e) { return new[] { TimberText.Format("import_stopped", e.Message) }; }
        }
        public sealed class PreviewDocument
        {
            internal PreviewDocument(ReferenceImport owner, string id, ConfigConverter converter, ConversionPreview plan,
                IReadOnlyList<string> notes, IReadOnlyList<string> errors, string source, string target, long generation)
            { Owner = owner; Id = id; Converter = converter; Plan = plan; Notes = notes; Errors = errors.Concat(plan.Errors).ToArray(); SourceSha256 = source; TargetSha256 = target; Generation = generation; }
            internal ReferenceImport Owner { get; }
            internal string Id { get; }
            internal ConfigConverter Converter { get; }
            internal long Generation { get; }
            public ConversionPreview Plan { get; }
            public IReadOnlyList<string> Notes { get; }
            public IReadOnlyList<string> Errors { get; }
            public string SourceSha256 { get; }
            public string TargetSha256 { get; }
            public override string ToString() => string.Join("\n", new[] { TimberText.Format("import_review", Id) }
                .Concat(Plan.Values.Select(v => v.SourceId + " → " + v.TargetId + " = " + v.Value))
                .Concat(Notes).Concat(Errors.Select(e => TimberText.Format("import_invalid", e)))
                .Concat(Plan.BindingConflicts.Select(c => TimberText.Format("import_collision", c.TargetId)))
                .Concat(new[] { TimberText.Format("import_source_hash", SourceSha256), TimberText.Format("import_target_hash", TargetSha256) }));
        }
    }
}

