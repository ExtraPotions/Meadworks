// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: C09 converter registrations for the five source files (design 10). Source files are read only;
// Apply backs up Delve's config first. Same model as Keel and Ember.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Conversion;
using ExtraPotions.Delve.Data;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Localization;
using ExtraPotions.Meadworks.Crucible;
using ModuleStepAside = ExtraPotions.Delve.Integration.ModuleStepAside;

namespace ExtraPotions.Delve.Runtime
{
    public sealed class DelveImport : IDisposable
    {
        readonly ConfigFile config;
        readonly DelveConfig settings;
        readonly MeadworksLog log;
        readonly List<IDisposable> registrations = new List<IDisposable>();
        bool disposed;

        /// <summary>Delve's data folder: <c>config/Meadworks/delve</c>.</summary>
        public static string DataDirectory => Path.Combine(Paths.ConfigPath, "Meadworks", "delve");

        public DelveImport(ConfigFile config, DelveConfig settings, MeadworksLog log)
        {
            this.config = config;
            this.settings = settings;
            this.log = log;
            foreach (ImportSource source in Enum.GetValues(typeof(ImportSource)))
            {
                var s = source;
                var from = Source(s);
                registrations.Add(MeadworksConversion.Register(new ConversionRegistration("delve." + s.ToString().ToLowerInvariant(), Plugin.Guid,
                    from.Guid, from.Name,
                    () => !disposed && File.Exists(SourcePath(s)) && !Imported(s),
                    () => Preview(s),
                    (document, resolutions, backup) => Apply(document as ImportPlan ?? throw new ArgumentException("Wrong Delve import preview."), backup))));
            }
        }

        static Integration.SourceInfo Source(ImportSource source)
        {
            switch (source)
            {
                case ImportSource.MaxDungeonRooms: return ModuleStepAside.MaxDungeonRooms;
                case ImportSource.MoreDungeonResets: return ModuleStepAside.MoreDungeonResets;
                case ImportSource.RestockRegen: return ModuleStepAside.RestockRegen;
                case ImportSource.ValheimAdminTool: return ModuleStepAside.ValheimAdminTool;
                default: return ModuleStepAside.EliteCreaturesReborn;
            }
        }

        static string SourcePath(ImportSource source) => Path.Combine(Paths.ConfigPath, SourceImport.FileName(source));

        bool Imported(ImportSource source) => settings.Get(DelveSettings.Import.ImportedSources).Split(',').Contains(source.ToString());

        public ImportPlan Preview(ImportSource source)
        {
            var plan = SourceImport.Plan(source, File.ReadAllText(SourcePath(source)));
            var report = Path.Combine(DataDirectory, "import-" + source.ToString().ToLowerInvariant() + ".txt");
            Directory.CreateDirectory(DataDirectory);
            File.WriteAllText(report, plan.Report());
            log.Info("Import preview for " + source + ": " + plan.Values.Count + " values, " + plan.DungeonModes.Count + " dungeon modes, "
                + plan.Sites.Count + " sites, " + plan.Texts.Count + " texts, " + plan.Notes.Count + " notes (" + report + ").");
            return plan;
        }

        object Apply(ImportPlan plan, bool backup)
        {
            if (!backup) throw new InvalidOperationException("Delve import requires a backup.");
            if (!MeadworksConfig.CanEdit(ConfigScope.ServerAuthoritative))
                throw new InvalidOperationException("Delve settings belong to the authority; import on the server or the host.");
            MeadworksConversion.Target(config).Backup();
            var save = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            var applied = 0;
            try
            {
                foreach (var value in plan.Values)
                {
                    var setting = DelveSettings.Find(value.SettingId);
                    if (setting == null) continue;
                    settings.Entry(setting).BoxedValue = value.Value;
                    applied++;
                }
                var marker = (ConfigEntry<string>)settings.Entry(DelveSettings.Import.ImportedSources);
                marker.Value = string.Join(",", marker.Value.Split(',').Where(p => p.Length > 0).Append(plan.Source.ToString()).Distinct());
            }
            finally
            {
                config.SaveOnConfigSet = save;
            }
            config.Save();
            applied += WriteModes(plan) + WriteSites(plan) + WriteTexts(plan);
            var text = DelveText.Format(DelveTextKeys.ImportDone, applied, plan.Source.ToString());
            log.Info(text);
            return text;
        }

        /// <summary>Merges the plan's modes into Delve.Dungeons.yml (entries already there for other types stay).</summary>
        static int WriteModes(ImportPlan plan)
        {
            if (plan.DungeonModes.Count == 0) return 0;
            var path = Path.Combine(DataDirectory, DungeonsFile.FileName);
            var current = File.Exists(path) ? DungeonsFile.Parse(File.ReadAllText(path)).Modes : new Dictionary<string, DungeonMode>();
            var merged = current.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            foreach (var m in plan.DungeonModes) merged[m.Key] = m.Value;
            Directory.CreateDirectory(DataDirectory);
            File.WriteAllText(path, DungeonsFile.Write(merged));
            return plan.DungeonModes.Count;
        }

        /// <summary>Writes Delve.Sites.yml radii (entrance and inside per generator).</summary>
        static int WriteSites(ImportPlan plan)
        {
            if (plan.Sites.Count == 0) return 0;
            var inv = CultureInfo.InvariantCulture;
            var text = new StringBuilder("# Delve site radii in meters: a player within entrance of the entrance, or within inside of the generator, holds the site.\nsites:\n");
            foreach (var s in plan.Sites.OrderBy(p => p.Key, StringComparer.Ordinal))
                text.Append("  ").Append(s.Key).Append(": {entrance: ").Append(s.Value.Entrance.ToString(inv)).Append(", inside: ").Append(s.Value.Inside.ToString(inv)).Append("}\n");
            Directory.CreateDirectory(DataDirectory);
            File.WriteAllText(Path.Combine(DataDirectory, "Delve.Sites.yml"), text.ToString());
            return plan.Sites.Count;
        }

        /// <summary>Writes the texts that differ from Delve's English catalog as an English C05 override file (merged with one already there).</summary>
        static int WriteTexts(ImportPlan plan)
        {
            var builtIn = DelveText.Translations?.Table("English") ?? new Dictionary<string, string>();
            var changed = plan.Texts.Select(t => new KeyValuePair<string, string>(t.Key, DelveTextKeys.ToCatalog(t.Key, t.Value)))
                .Where(t => !builtIn.TryGetValue(t.Key, out var text) || text != t.Value).ToList();
            if (changed.Count == 0) return 0;
            var path = Path.Combine(DelveText.OverridesDirectory, "English.json");
            var merged = new Dictionary<string, string>(StringComparer.Ordinal);
            if (File.Exists(path))
            {
                try
                {
                    foreach (var e in TranslationJson.Parse(File.ReadAllText(path))) merged[e.Key] = e.Value;
                }
                catch (Exception error) when (error is FormatException || error is ArgumentException || error is InvalidOperationException)
                {
                    // A broken override file is replaced by the imported texts; Crucible rejects it at load anyway.
                }
            }
            foreach (var t in changed) merged[t.Key] = t.Value;
            Directory.CreateDirectory(DelveText.OverridesDirectory);
            File.WriteAllText(path, TranslationJson.Write(merged));
            return changed.Count;
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
