// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>C09-owned YAML Preview/Apply. Source files are read-only; Apply requires authority, a fresh preview and backups.</summary>
    internal sealed class InscribeYamlConversion : IDisposable
    {
        private sealed class FilePlan
        {
            public string Source = "", Target = "", SourceHash = "", TargetHash = "";
            public Conversion.YamlImportPlan Import = null!;
        }
        private sealed class PreviewDocument
        {
            public List<FilePlan> Files = new List<FilePlan>();
            public string Report = "", Paths = "", TargetState = "";
            public long Generation;
            public SessionRole Role;
            public bool Consumed;
            public Dictionary<string, string> Hashes = new Dictionary<string, string>();
            public override string ToString() => Report;
        }
        private readonly IDisposable registration;
        private readonly ConfigFile config;
        private readonly MeadworksLog log;
        private PreviewDocument? current;
        private bool disposed;
        public InscribeYamlConversion(ConfigFile config, MeadworksLog log)
        {
            this.config = config; this.log = log;
            registration = MeadworksConversion.Register(new ConversionRegistration("inscribe.elitecrafting.yaml", Plugin.Guid, "com.EliteCrafting", "EliteCrafting YAML (0.7.0 / 0.8.0 / 0.9.1)",
                () => !disposed && CruciblePlugin.Session.IsAuthority && SourceFiles().Any(), Preview,
                (document, resolutions, backup) => Apply(document, backup)));
        }
        private static IEnumerable<string> SourceFiles() => Directory.Exists(Paths.ConfigPath)
            ? Directory.GetFiles(Paths.ConfigPath, "EliteCrafting_*.yml").Where(p => Path.GetFileName(p).StartsWith("EliteCrafting_economy", StringComparison.Ordinal) || Path.GetFileName(p).StartsWith("EliteCrafting_inscriptions", StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal)
            : Enumerable.Empty<string>();
        private static string Hash(string path)
        {
            if (!File.Exists(path)) return "(missing)";
            using var hash = SHA256.Create(); return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
        }
        private static IEnumerable<string> InputPaths() => SourceFiles().Concat(Directory.GetFiles(Paths.ConfigPath, "Inscribe.inscriptions*.yml"))
            .Concat(Directory.GetFiles(Paths.ConfigPath, "Inscribe.economy*.yml")).OrderBy(path => path, StringComparer.Ordinal);
        private string TargetState() => string.Join("\n", config.Keys.OrderBy(key => key.Section, StringComparer.Ordinal).ThenBy(key => key.Key, StringComparer.Ordinal)
            .Select(key => key.Section + "." + key.Key + "=" + TomlTypeConverter.ConvertToString(config[key].BoxedValue, config[key].SettingType)));
        private void SessionAdmission(PreviewDocument preview)
        {
            var session = CruciblePlugin.Session;
            var permission = MeadworksAuthority.Decide(Plugin.Guid, ConfigScope.ServerAuthoritative);
            if (disposed || !CruciblePlugin.IsReady || !session.IsAuthority || session.Role == SessionRole.RemoteClient
                || session.Generation != preview.Generation || session.Role != preview.Role || !permission.Allowed || permission.RequiresServerAmendment)
                throw new InvalidOperationException("Current authority and original import session required.");
        }
        private void Admission(PreviewDocument preview)
        {
            SessionAdmission(preview);
            if (preview.Consumed || preview.Paths != string.Join("\n", InputPaths()) || preview.TargetState != TargetState())
                throw new InvalidOperationException("Import inputs or target configuration changed; preview again.");
            foreach (var pair in preview.Hashes)
                if (Hash(pair.Key) != pair.Value) throw new InvalidOperationException("An import file changed; preview again.");
        }
        private void PublicationAdmission(PreviewDocument preview, IReadOnlyDictionary<string, string> written)
        {
            SessionAdmission(preview);
            if (preview.Consumed || preview.TargetState != TargetState()) throw new InvalidOperationException("Import target configuration changed before publication.");
            var expectedPaths = preview.Paths.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Concat(written.Keys).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal);
            if (string.Join("\n", expectedPaths) != string.Join("\n", InputPaths())) throw new InvalidOperationException("Import input file set changed before publication.");
            foreach (var file in preview.Hashes)
                if (!written.ContainsKey(file.Key) && Hash(file.Key) != file.Value) throw new InvalidOperationException("Import input changed before publication.");
            foreach (var file in written)
                if (Hash(file.Key) != file.Value) throw new InvalidOperationException("Imported file changed before publication.");
        }
        private object Preview()
        {
            if (disposed || !CruciblePlugin.Session.IsAuthority) throw new InvalidOperationException("Only the authority previews Inscribe rule imports.");
            var result = new PreviewDocument { Generation = CruciblePlugin.Session.Generation, Role = CruciblePlugin.Session.Role,
                Paths = string.Join("\n", InputPaths()), TargetState = TargetState() };
            SessionAdmission(result);
            foreach (var path in InputPaths().Append(config.ConfigFilePath)) result.Hashes[path] = Hash(path);
            var defaults = FamilyBuilder.DefaultText(FamilySpec.Economy);
            foreach (var source in SourceFiles())
            {
                var name = Path.GetFileName(source);
                var target = Path.Combine(Paths.ConfigPath, "Inscribe." + name.Substring("EliteCrafting_".Length));
                var bytes = File.ReadAllBytes(source);
                string sourceHash;
                using (var digest = SHA256.Create()) sourceHash = Convert.ToBase64String(digest.ComputeHash(bytes));
                if (sourceHash != result.Hashes[source]) throw new InvalidOperationException("Import source changed during preview.");
                string sourceText;
                using (var reader = new StreamReader(new MemoryStream(bytes))) sourceText = reader.ReadToEnd();
                string targetHash = Hash(target); result.Hashes[target] = targetHash;
                result.Files.Add(new FilePlan { Source=source, Target=target, SourceHash=sourceHash, TargetHash=targetHash, Import=Conversion.EliteCraftingYamlImport.Preview(name, sourceText, defaults, Adopted.Core.Embedded.Text("Inscribe.migrations.inscriptions-v3.yml")) });
            }
            Validate(result);
            result.Report = string.Join("\n", result.Files.SelectMany(f => new[] { Path.GetFileName(f.Source) + " -> " + Path.GetFileName(f.Target) }.Concat(f.Import.Notes)));
            Admission(result); current = result;
            log.Info("EliteCrafting YAML preview: " + result.Files.Count + " files; source files remain unchanged.");
            return result;
        }
        private static void Validate(PreviewDocument document)
        {
            foreach (var spec in new[] { FamilySpec.Affixes, FamilySpec.Economy })
            {
                var files = document.Files.Where(f => Path.GetFileName(f.Target).StartsWith(spec.Prefix, StringComparison.Ordinal)).Select(f => new SourceText(Path.GetFileName(f.Target), f.Import.Text)).ToList();
                if (files.Count == 0) continue;
                var replacementNames = new HashSet<string>(files.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
                foreach (var path in Directory.GetFiles(Paths.ConfigPath, spec.Prefix + "*.yml"))
                    if (!replacementNames.Contains(Path.GetFileName(path)))
                    {
                        var text = File.ReadAllText(path);
                        if (!RuleFormat.IsReadable(text, spec.Format)) throw new FormatException("Existing target rule file has an unsupported format: " + Path.GetFileName(path));
                        files.Add(new SourceText(Path.GetFileName(path), text));
                    }
                files = files.OrderBy(f => f.Name == spec.MainFile ? 0 : 1).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
                var issues = new RuleIssues(); var merged = FamilyBuilder.Merge(spec, files, issues);
                if (!issues.HasErrors) { if (spec == FamilySpec.Economy) EconomyParser.Parse(merged, issues); else AffixFamilyParser.Parse(merged, issues); }
                if (issues.HasErrors) throw new FormatException("The imported " + spec.Prefix + " rules were refused: " + string.Join("; ", issues.Errors));
            }
        }
        private object Apply(object document, bool backup)
        {
            if (disposed || !backup || !CruciblePlugin.Session.IsAuthority || !ReferenceEquals(document, current) || current == null)
                throw new InvalidOperationException("Rule import requires authority, this converter's current preview, and backups.");
            var preview = current;
            Admission(preview);
            Validate(preview);
            if (preview.Files.Count == 0) return "No YAML files to import.";
            Admission(preview);
            MeadworksConversion.Target(config).Backup();
            var previous = preview.Files.ToDictionary(f => f.Target, f => File.Exists(f.Target) ? File.ReadAllBytes(f.Target) : null);
            string stamp = DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (var file in preview.Files) if (File.Exists(file.Target)) File.Copy(file.Target, file.Target + ".import-" + stamp + ".bak", false);
            var written = new Dictionary<string, string>();
            try
            {
                foreach (var file in preview.Files)
                {
                    PublicationAdmission(preview, written);
                    if (Hash(file.Target) != file.TargetHash) throw new InvalidOperationException("Import target changed before write.");
                    InscribeSourceConversion.WriteBytes(file.Target, new UTF8Encoding(false).GetBytes(file.Import.Text));
                    using var digest = SHA256.Create();
                    written.Add(file.Target, Convert.ToBase64String(digest.ComputeHash(new UTF8Encoding(false).GetBytes(file.Import.Text))));
                }
                PublicationAdmission(preview, written);
                preview.Consumed = true;
                var applied = ActiveRules.ReloadLocal();
                SessionAdmission(preview);
                if (applied.Affixes == FamilyReload.Rejected || applied.Economy == FamilyReload.Rejected)
                    throw new InvalidOperationException("Imported files did not apply; the previous files were restored.");
            }
            catch (Exception original)
            {
                if (written.Count > 0) preview.Consumed = true;
                try
                {
                    SessionAdmission(preview);
                    foreach (var file in written)
                        if (Hash(file.Key) != file.Value) throw new InvalidOperationException("Imported rules changed before recovery; backups retained.");
                    foreach (var file in written)
                    {
                        if (previous[file.Key] == null) File.Delete(file.Key); else InscribeSourceConversion.WriteBytes(file.Key, previous[file.Key]!);
                    }
                    var recovered = ActiveRules.ReloadLocal();
                    if (recovered.Affixes == FamilyReload.Rejected || recovered.Economy == FamilyReload.Rejected)
                        throw new InvalidOperationException("Previous rules were rejected during recovery.");
                }
                catch (Exception recovery)
                {
                    preview.Consumed = true;
                    throw new IOException(InscribeText.Text("import_recovery_incomplete"), new AggregateException(original, recovery));
                }
                throw;
            }
            var report = "Imported " + preview.Files.Count + " EliteCrafting YAML files with backups; source files unchanged.";
            if (ReferenceEquals(current, preview)) current = null; log.Info(report); return report;
        }
        public void Dispose() { if (disposed) return; disposed = true; current = null; registration.Dispose(); }
    }
}
