// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Edge-owned C09 preview/apply callbacks. Shared validated config transactions are used without replacing that service.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Conversion;
using ExtraPotions.Edge.Secondaries.Adopted;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Edge.Runtime;

internal sealed class EdgeSourceConversion : IDisposable
{
    private sealed class Plan
    {
        internal long Generation;
        internal string SourceName = "";
        internal readonly Dictionary<string, string> Hashes = new(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, object> Settings = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, object> Before = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> Files = new(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, string[]> Listings = new(StringComparer.OrdinalIgnoreCase);
        internal readonly List<string> Notes = new();
        public override string ToString() => string.Join("\n", Notes.Concat(Settings.Select(p => "Set " + p.Key + " = " + p.Value)).Concat(Files.Keys.Select(p => "Write " + Path.GetFileName(p))));
    }
    private readonly ConfigFile config;
    private readonly EdgeConfig settings;
    private readonly EdgeRuleFiles rules;
    private readonly MeadworksLog log;
    private readonly List<IDisposable> registrations = new();
    private bool disposed;
    private static string RuleDirectory => Path.Combine(Paths.ConfigPath, "Edge");

    internal EdgeSourceConversion(ConfigFile config, EdgeConfig settings, EdgeRuleFiles rules, MeadworksLog log)
    {
        this.config = config; this.settings = settings; this.rules = rules; this.log = log;
        foreach (ImportSource source in Enum.GetValues(typeof(ImportSource)))
        {
            var selected = source; Plan? current = null;
            string sourceId = source == ImportSource.SecondaryAttacksConfig ? "sighsorry.SecondaryAttacks"
                : source == ImportSource.SlopeCombatAssistanceConfig ? "digitalroot.mods.slopecombatassistance" : "lazly.valheim.keepweaponsafterswim";
            registrations.Add(MeadworksConversion.Register(new ConversionRegistration("edge." + source, Plugin.Guid, sourceId, source.ToString(),
                () => CanPreview && File.Exists(Path.Combine(Paths.ConfigPath, SourceImport.FileName(selected))),
                () => current = PreviewConfig(selected), (document, choices, backup) => Apply(document, current, backup))));
        }
        Plan? yaml = null;
        registrations.Add(MeadworksConversion.Register(new ConversionRegistration("edge.secondaries.yaml", Plugin.Guid, "sighsorry.SecondaryAttacks", "SecondaryAttacks YAML",
            () => CanPreview && Directory.Exists(Path.Combine(Paths.ConfigPath, "SecondaryAttacks")),
            () => yaml = PreviewYaml(), (document, choices, backup) => Apply(document, yaml, backup))));
    }
    private bool CanPreview => !disposed && Plugin.Ready && CruciblePlugin.Session.IsAuthority;
    private static string Hash(string path)
    { if (!File.Exists(path)) return "missing"; using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
    private string Read(Plan plan, string path)
    {
        string observed = Hash(path);
        if (plan.Hashes.TryGetValue(path, out var before) && before != observed) throw new InvalidOperationException("Import file changed during Preview: " + path);
        string text = File.Exists(path) ? File.ReadAllText(path) : "";
        if (Hash(path) != observed) throw new InvalidOperationException("Import file changed while reading: " + path);
        plan.Hashes[path] = observed; return text;
    }
    private Plan NewPlan()
    {
        if (!CanPreview) throw new InvalidOperationException("The current authority is required to preview Edge imports.");
        var plan = new Plan { Generation = CruciblePlugin.Session.Generation };
        Read(plan, config.ConfigFilePath);
        foreach (var setting in EdgeSettings.All) plan.Before[setting.Id] = settings.Entry(setting).BoxedValue;
        plan.Notes.Add("Source files are retained. Apply requires this preview and backups.");
        return plan;
    }
    private void Mark(Plan plan, string id)
    {
        string before = Convert.ToString(plan.Before[EdgeSettings.Import.ImportedSources.Id], CultureInfo.InvariantCulture) ?? "";
        plan.Settings[EdgeSettings.Import.ImportedSources.Id] = string.Join(";", before.Split(';').Where(p => p.Length > 0).Concat(new[] { id }).Distinct(StringComparer.Ordinal));
    }
    private Plan PreviewConfig(ImportSource source)
    {
        var plan = NewPlan(); plan.SourceName = SourceImport.FileName(source);
        string sourcePath = Path.Combine(Paths.ConfigPath, plan.SourceName);
        string sourceText = Read(plan, sourcePath);
        if (plan.Hashes[sourcePath] == "missing") throw new FileNotFoundException("Source configuration is missing.", sourcePath);
        var import = SourceImport.Plan(source, sourceText, authority: true);
        foreach (var value in import.Values) plan.Settings[value.SettingId] = value.Value;
        plan.Notes.AddRange(import.Notes.Select(n => n.ToString()));
        Mark(plan, source.ToString());
        ((IValidatedConversionTarget)MeadworksConversion.Target(config)).Validate(plan.Settings);
        return plan;
    }
    private static string[] List(string directory) => Directory.Exists(directory) ? Directory.GetFiles(directory, "*.yml").OrderBy(p => p, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
    private Plan PreviewYaml()
    {
        var plan = NewPlan(); plan.SourceName = "SecondaryAttacks YAML"; string source = Path.Combine(Paths.ConfigPath, "SecondaryAttacks");
        plan.Listings[source] = List(source); plan.Listings[RuleDirectory] = List(RuleDirectory);
        var mapping = YamlImport.Plan(plan.Listings[source]);
        plan.Notes.AddRange(mapping.Notes.Select(n => n.ToString()));
        if (mapping.Copies.Count == 0) throw new FormatException("No SecondaryAttacks rule families found.");
        foreach (var file in mapping.Copies)
        {
            string target = Path.Combine(RuleDirectory, file.To);
            if (plan.Files.ContainsKey(target)) throw new FormatException("Two source files map to the same Edge target: " + file.To);
            string sourceText = Read(plan, file.From);
            if (plan.Hashes[file.From] == "missing") throw new FileNotFoundException("Source rule file is missing.", file.From);
            plan.Files.Add(target, sourceText); Read(plan, target);
        }
        var existing = plan.Listings[RuleDirectory].Where(p => EdgeDataFiles.Classify(p) != null).ToDictionary(p => p, p => Read(plan, p));
        RuleImportDocuments.Compose(existing, plan.Files);
        Mark(plan, "SecondaryAttacksYaml");
        ((IValidatedConversionTarget)MeadworksConversion.Target(config)).Validate(plan.Settings);
        return plan;
    }
    private object Apply(object document, Plan? current, bool backup)
    {
        if (!CanPreview || !backup || current == null || !ReferenceEquals(document, current) || current.Generation != CruciblePlugin.Session.Generation)
            throw new InvalidOperationException("Apply requires the current authority, this converter's fresh preview and backups.");
        foreach (var pair in current.Before)
            if (!Equals(settings.Entry(EdgeSettings.All.Single(s => s.Id == pair.Key)).BoxedValue, pair.Value)) throw new InvalidOperationException("Edge settings changed after Preview; preview again.");
        foreach (var pair in current.Hashes) if (Hash(pair.Key) != pair.Value) throw new InvalidOperationException("An import file changed after Preview; preview again.");
        foreach (var pair in current.Listings) if (!pair.Value.SequenceEqual(List(pair.Key))) throw new InvalidOperationException("Rule file membership changed after Preview; preview again.");
        if (current.Files.Count > 0)
            RuleImportDocuments.Compose(current.Listings[RuleDirectory].Where(p => EdgeDataFiles.Classify(p) != null).ToDictionary(p => p, File.ReadAllText), current.Files);
        var target = (IValidatedConversionTarget)MeadworksConversion.Target(config);
        target.Validate(current.Settings); target.Backup();
        var oldFiles = current.Files.Keys.ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
        var oldSettings = current.Settings.Keys.ToDictionary(k => k, k => current.Before[k]);
        string stamp = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
        foreach (var file in oldFiles) if (file.Value != null) File.Copy(file.Key, file.Key + ".import-" + stamp + ".bak", false);
        try
        {
            foreach (var file in current.Files) Write(file.Key, Encoding.UTF8.GetBytes(file.Value));
            target.WriteAtomic(current.Settings);
            if (current.Files.Count > 0 && !rules.ReloadForImport()) throw new InvalidOperationException("Rule reload rejected the import.");
            current.Generation = -1; // A successful preview can never be replayed.
            EdgeText.Centre(EdgeText.Format("import_done", current.Settings.Count - 1 + current.Files.Count, current.SourceName), log);
            return "Applied with backups. Source files retained.";
        }
        catch
        {
            foreach (var file in oldFiles) { if (file.Value == null) File.Delete(file.Key); else Write(file.Key, file.Value); }
            target.WriteAtomic(oldSettings);
            if (current.Files.Count > 0) rules.ReloadForImport();
            current.Generation = -1; throw;
        }
    }
    private static void Write(string path, byte[] bytes)
    {
        string temporary = path + ".import-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Dispose() { disposed = true; foreach (var registration in registrations) registration.Dispose(); registrations.Clear(); }
}
