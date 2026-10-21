// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Reuses Timber ReferenceImport's C09 preview/stale-check/validated atomic-target adapter.
// Reference configs are data only; no Valheim_Admin_Tool or AchievementSync source/assembly accessed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Hex.Runtime.Native;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Hex.Runtime;

internal sealed class HexImport : IDisposable
{
    readonly ConfigFile config;
    readonly List<IDisposable> registrations = new();
    readonly int thread = Thread.CurrentThread.ManagedThreadId;
    bool disposed;
    static readonly string[] kinds = { "vat", "as", "as-guard", "vat-preset" };
    internal HexImport(ConfigFile config)
    {
        this.config = config;
        try
        {
            foreach (var kind in kinds)
            {
                var id = kind;
                registrations.Add(MeadworksConversion.Register(new ConversionRegistration("hex." + id, Plugin.Guid,
                    id.StartsWith("vat", StringComparison.Ordinal) ? "com.icecub.valheimadmintool" : MeadworksAchievementGuard.AchievementSyncGuid,
                    id.StartsWith("vat", StringComparison.Ordinal) ? "Valheim_Admin_Tool" : "AchievementSync" + (id == "as-guard" ? " (Crucible guard)" : " (Hex preferences)"),
                    () => !disposed && File.Exists(SourcePath(id)) && MeadworksHeadless.AllowsUi,
                    () => Preview(id), (value, resolutions, backup) =>
                    {
                        if (!backup) throw new InvalidOperationException(HexText.Text("import_backup_required"));
                        return Apply(value as PreviewDocument ?? throw new ArgumentException("Wrong Hex import preview."), resolutions);
                    })));
            }
        }
        catch { Dispose(); throw; }
    }
    public void Dispose() { if (disposed) return; disposed = true; foreach (var lease in registrations) lease.Dispose(); registrations.Clear(); }
    void Check() { if (disposed || !Plugin.Ready || thread != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException(HexText.Text("import_unavailable")); }
    static string SourcePath(string id) => Path.Combine(Paths.ConfigPath, id == "vat" ? HexSourceImport.VatFile : id == "vat-preset" ? HexSourceImport.VatPresetFile : HexSourceImport.AsFile);
    static string Read(string path, out string hash)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 1024 * 1024) throw new IOException("Reference config exceeds 1 MiB.");
        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        stream.Position = 0;
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        return reader.ReadToEnd();
    }
    static string Hash(string text) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", ""); }
    ConfigFile Owner(string id)
    {
        if (id != "as-guard") return config;
        if (!Chainloader.PluginInfos.TryGetValue(CruciblePlugin.Guid, out var info) || info.Instance == null
            || !info.Instance.Config.TryGetEntry<bool>("Achievements", "SyncPastStats", out _)
            || !info.Instance.Config.TryGetEntry<KeyboardShortcut>("Achievements", "ResyncKey", out _))
            throw new InvalidOperationException(HexText.Text("import_guard_unavailable"));
        return info.Instance.Config;
    }
    static string EntryId(ConfigEntryBase entry) => entry.Definition.Section + "." + entry.Definition.Key;
    static ConfigEntryBase Entry(ConfigFile owner, string target) => owner[owner.Keys.Single(key => key.Section + "." + key.Key == target)];
    ConfigEntryBase MappedEntry(string id, string logical)
    {
        if (id == "as-guard") return Entry(Owner(id), logical);
        if (logical == "menu") return Plugin.Settings!.MenuKey;
        if (logical.StartsWith("action:", StringComparison.Ordinal)) return Plugin.Settings!.ActionHotkeys[logical.Substring(7)];
        if (logical.StartsWith("hotkey:", StringComparison.Ordinal)) return Plugin.Settings!.Hotkey(logical.Substring(7));
        return Plugin.Settings!.PreferenceEntries()[logical.Substring(11)];
    }
    static string ConfigHash(ConfigFile owner) => Hash((File.Exists(owner.ConfigFilePath) ? Read(owner.ConfigFilePath, out _) : "missing") + "\n" +
        string.Join("\n", owner.Keys.OrderBy(key => key.Section, StringComparer.Ordinal).ThenBy(key => key.Key, StringComparer.Ordinal)
            .Select(key => key.Section + "." + key.Key + "=" + TomlTypeConverter.ConvertToString(owner[key].BoxedValue, owner[key].SettingType))));
    string TargetHash(string id) => id == "vat-preset" ? Hash(HexPresets.ImportDisk()) : ConfigHash(Owner(id));

    internal PreviewDocument Preview(string id)
    {
        Check();
        if (!kinds.Contains(id)) throw new ArgumentException("Unknown Hex source import.");
        var text = Read(SourcePath(id), out var sourceHash);
        var raw = CfgText.Read(text, "source");
        var rules = new List<ConversionRule>(); var settings = new List<KeyValuePair<string, string>>();
        var notes = new List<string>(); var errors = new List<string>();
        if (id == "vat-preset") notes.Add(HexText.Text("import_preset_note"));
        if (raw.Count == 0 && text.Any(character => !char.IsWhiteSpace(character)) && !text.TrimStart().StartsWith("#", StringComparison.Ordinal)) errors.Add(HexText.Text("import_format_unknown"));
        foreach (var pair in raw)
        {
            var source = pair.Key.Substring(7);
            var logical = id == "vat-preset" ? HexSourceImport.PresetKey(source) : HexSourceImport.Target(id, source);
            if (logical == null)
            {
                var referral = HexSourceImport.Referral(source);
                notes.Add(referral != null ? HexText.Format("import_owner", source, referral) : HexText.Format("import_unknown", source));
                continue;
            }
            var entry = id == "vat-preset" ? Plugin.Settings!.PreferenceEntries()[logical] : MappedEntry(id, logical);
            if (entry.SettingType == typeof(KeyboardShortcut) && string.IsNullOrWhiteSpace(pair.Value)) { notes.Add(HexText.Format("import_unassigned", source)); continue; }
            var target = id == "vat-preset" ? logical : EntryId(entry);
            var type = entry.SettingType == typeof(KeyboardShortcut) ? "KeyboardShortcut" : entry.SettingType.Name;
            var constraints = entry.Description.AcceptableValues is AcceptableValueRange<int> range ? "From " + range.MinValue + " to " + range.MaxValue : "";
            rules.Add(new ConversionRule(pair.Key, target, type, constraints, actionBinding: entry.SettingType == typeof(KeyboardShortcut)));
            settings.Add(pair);
        }
        var converter = new ConfigConverter(new ConversionMapping(1, id, 0, rules));
        var plan = converter.Preview(settings);
        if (id != "vat-preset")
            foreach (var collision in Collisions(Owner(id), plan.Values.GroupBy(item => item.TargetId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Value)))
                notes.Add(HexText.Format("import_collision", collision));
        return new PreviewDocument(this, id, converter, plan, notes.Distinct().ToArray(), errors, sourceHash, TargetHash(id), CruciblePlugin.Session.Generation);
    }
    void Admission(PreviewDocument preview)
    {
        Check();
        if (!ReferenceEquals(preview.Owner, this)) throw new ArgumentException("Preview belongs to another Hex import.");
        Read(SourcePath(preview.Id), out var source);
        if (source != preview.SourceSha256 || preview.TargetSha256 != TargetHash(preview.Id) || preview.Generation != CruciblePlugin.Session.Generation)
            throw new InvalidOperationException(HexText.Text("import_stale"));
        if (preview.Errors.Count != 0) throw new InvalidOperationException(HexText.Text("import_invalid"));
        if (preview.Id == "vat-preset") return;
        var ownerId = preview.Id == "as-guard" ? CruciblePlugin.Guid : Plugin.Guid;
        foreach (var value in preview.Plan.Values)
        {
            var entry = Entry(Owner(preview.Id), value.TargetId);
            if (!MeadworksConfig.TryGetMetadata(entry, out var metadata) || metadata.ProductId != ownerId || metadata.Scope != ConfigScope.Local
                || !metadata.Decision.Allowed || metadata.Decision.RequiresServerAmendment) throw new InvalidOperationException(HexText.Text("import_unavailable"));
        }
    }
    object Apply(PreviewDocument preview, IReadOnlyDictionary<string, string> resolutions)
    {
        Admission(preview);
        if (preview.Plan.Values.Count == 0) return HexText.Text("import_no_changes");
        var choices = resolutions.Where(pair => !pair.Key.StartsWith("binding:", StringComparison.Ordinal)).ToDictionary(pair => pair.Key, pair => pair.Value);
        var bindings = resolutions.Where(pair => pair.Key.StartsWith("binding:", StringComparison.Ordinal)).ToDictionary(pair => pair.Key.Substring(8), pair => pair.Value);
        if (preview.Id == "vat-preset")
        {
            var presetResult = preview.Converter.Apply(preview.Plan, new PresetTarget(this, preview), choices);
            return HexText.Format("import_applied", presetResult.Written);
        }
        ConversionResult? result = null;
        var ownerId = preview.Id == "as-guard" ? CruciblePlugin.Guid : Plugin.Guid;
        var amended = MeadworksConfig.AmendBatch(ownerId, preview.Plan.Values.Select(value => Entry(Owner(preview.Id), value.TargetId)).Distinct().ToArray(), () =>
        {
            Admission(preview);
            result = preview.Converter.Apply(preview.Plan, new CheckedTarget(this, preview), choices, bindings);
        });
        if (!amended.Applied || result == null) throw new InvalidOperationException(HexText.Text("import_unavailable"));
        return HexText.Format("import_applied", result.Written);
    }
    IEnumerable<string> Collisions(ConfigFile owner, IReadOnlyDictionary<string, object> values)
    {
        // Known Hex/C14 owner boundary, not a claim that arbitrary external bindings have been enumerated.
        var owners = new List<ConfigFile> { config };
        if (Chainloader.PluginInfos.TryGetValue(CruciblePlugin.Guid, out var info) && info.Instance != null) owners.Add(info.Instance.Config);
        foreach (var pair in values)
        {
            if (Entry(owner, pair.Key).SettingType != typeof(KeyboardShortcut)) continue;
            var chord = HotkeyChord.Parse(Convert.ToString(pair.Value, CultureInfo.InvariantCulture));
            if (chord.IsNone) continue;
            foreach (var other in owners.Distinct()) foreach (var key in other.Keys)
            {
                var entry = other[key];
                if (entry.SettingType != typeof(KeyboardShortcut) || ReferenceEquals(other, owner) && values.ContainsKey(EntryId(entry))) continue;
                if (chord == MeadworksInput.Chord((KeyboardShortcut)entry.BoxedValue)) yield return pair.Key + " / " + EntryId(entry);
            }
        }
    }
    sealed class CheckedTarget : IValidatedConversionTarget
    {
        readonly HexImport owner; readonly PreviewDocument preview; readonly IConversionTarget target;
        internal CheckedTarget(HexImport owner, PreviewDocument preview) { this.owner = owner; this.preview = preview; target = MeadworksConversion.Target(owner.Owner(preview.Id)); }
        public int SchemaVersion => target.SchemaVersion;
        public void Validate(IReadOnlyDictionary<string, object> values)
        {
            owner.Admission(preview); ((IValidatedConversionTarget)target).Validate(values);
            foreach (var pair in values)
            {
                var entry = Entry(owner.Owner(preview.Id), pair.Key);
                var native = entry.SettingType.IsInstanceOfType(pair.Value) ? pair.Value : TomlTypeConverter.ConvertToValue(Convert.ToString(pair.Value, CultureInfo.InvariantCulture), entry.SettingType);
                if (entry.Description.AcceptableValues != null && !entry.Description.AcceptableValues.IsValid(native)) throw new ArgumentException(HexText.Text("import_invalid"));
            }
            if (owner.Collisions(owner.Owner(preview.Id), values).Any()) throw new InvalidOperationException(HexText.Text("import_collision_refused"));
        }
        public void Backup() { owner.Admission(preview); target.Backup(); }
        public void WriteAtomic(IReadOnlyDictionary<string, object> values) { owner.Admission(preview); target.WriteAtomic(values); }
    }
    sealed class PresetTarget : IValidatedConversionTarget
    {
        readonly HexImport owner; readonly PreviewDocument preview; readonly string expected;
        string proposal = "";
        internal PresetTarget(HexImport owner, PreviewDocument preview) { this.owner = owner; this.preview = preview; expected = HexPresets.ImportDisk(); }
        public int SchemaVersion => 0;
        public void Validate(IReadOnlyDictionary<string, object> values) { owner.Admission(preview); proposal = HexPresets.ImportProposal(expected, values); }
        public void Backup() { owner.Admission(preview); HexPresets.ImportBackup(expected); }
        public void WriteAtomic(IReadOnlyDictionary<string, object> values) { owner.Admission(preview); if (proposal.Length == 0) throw new InvalidOperationException(); HexPresets.ImportWrite(expected, proposal); }
    }
    internal sealed class PreviewDocument
    {
        internal PreviewDocument(HexImport owner, string id, ConfigConverter converter, ConversionPreview plan, IReadOnlyList<string> notes,
            IReadOnlyList<string> errors, string source, string target, long generation)
        { Owner = owner; Id = id; Converter = converter; Plan = plan; Notes = notes; Errors = errors.Concat(plan.Errors).ToArray(); SourceSha256 = source; TargetSha256 = target; Generation = generation; }
        internal HexImport Owner { get; }
        internal string Id { get; }
        internal ConfigConverter Converter { get; }
        internal long Generation { get; }
        public ConversionPreview Plan { get; }
        public IReadOnlyList<string> Notes { get; }
        public IReadOnlyList<string> Errors { get; }
        public string SourceSha256 { get; }
        public string TargetSha256 { get; }
        public override string ToString() => string.Join("\n", new[] { HexText.Format("import_review", Id) }
            .Concat(Plan.Values.Select(value => value.SourceId + " → " + value.TargetId + " = " + value.Value))
            .Concat(Notes).Concat(Errors.Select(error => HexText.Format("import_error", error)))
            .Concat(Plan.BindingConflicts.Select(conflict => HexText.Format("import_collision", conflict.TargetId)))
            .Concat(new[] { "Source SHA256: " + SourceSha256, "Target SHA256: " + TargetSha256 }));
    }
}
