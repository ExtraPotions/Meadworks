// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the NoFogBruh import of the weather event switches (Cairn Q2, design 9): a C09 converter registration
// (Almanac's Convert tools) and the console command `tempest import [apply]`. The source file is read only; Apply backs up
// Tempest's config first. Same model as Keel and Ember.
using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Tempest.Config;
using ExtraPotions.Tempest.Conversion;
using ExtraPotions.Tempest.Localization;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>
    /// Offered in Almanac's Convert tools and from the console, never in a mod window or the F8 page. Preview writes
    /// <c>config/Meadworks/tempest/import-nofogbruh.txt</c>.
    /// </summary>
    public sealed class TempestImport : IDisposable
    {
        public const string Section = "Import";
        const string SourceName = "NoFogBruh";

        readonly ConfigFile config;
        readonly TempestConfig settings;
        readonly MeadworksLog log;
        readonly ConfigEntry<string> marker;
        readonly IDisposable registration;
        bool disposed;

        public TempestImport(ConfigFile config, TempestConfig settings, MeadworksLog log)
        {
            this.config = config;
            this.settings = settings;
            this.log = log;
            marker = MeadworksConfig.BindHidden(config, Section, "Imported Sources", "", "Managed by Tempest: sources already imported.");
            var from = Integration.ModuleStepAside.NoFogBruh;
            registration = MeadworksConversion.Register(new ConversionRegistration("tempest.nofogbruh", Plugin.Guid, from.Guid, from.Name,
                () => Available,
                () => Preview(),
                (document, resolutions, backup) => Apply(document as ImportPlan ?? throw new ArgumentException("Wrong Tempest import preview."), backup)));
        }

        static string SourcePath => Path.Combine(Paths.ConfigPath, SourceImport.NoFogBruhFile);

        static string ReportPath => Path.Combine(Paths.ConfigPath, "Meadworks", "tempest", "import-nofogbruh.txt");

        bool Imported => marker.Value.Split(',').Contains(SourceName);

        /// <summary>A NoFogBruh config exists and was not imported yet.</summary>
        public bool Available => !disposed && File.Exists(SourcePath) && !Imported;

        /// <summary>The console lines for <c>tempest import</c> (preview) or <c>tempest import apply</c>.</summary>
        public string Run(bool apply)
        {
            if (!Available) return TempestText.Text(TempestTextKeys.ImportUnavailable);
            if (!MeadworksConfig.CanEdit(ConfigScope.ServerAuthoritative)) return TempestText.Text(TempestTextKeys.ImportHostOnly);
            var plan = Preview();
            return apply ? Apply(plan, true) : TempestText.Format(TempestTextKeys.ImportPreview, SourceName, plan.Values.Count, plan.OtherDisabled.Count, ReportPath);
        }

        public ImportPlan Preview()
        {
            var plan = SourceImport.Plan(File.ReadAllText(SourcePath));
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
            File.WriteAllText(ReportPath, plan.Report());
            log.Info("Import preview for NoFogBruh: " + plan.Values.Count + " switches, " + plan.OtherDisabled.Count + " other environments, "
                + plan.Notes.Count + " notes (" + ReportPath + ").");
            return plan;
        }

        string Apply(ImportPlan plan, bool backup)
        {
            if (!backup) throw new InvalidOperationException("Tempest import requires a backup.");
            if (!MeadworksConfig.CanEdit(ConfigScope.ServerAuthoritative)) throw new InvalidOperationException("The weather filter is set by the server.");
            MeadworksConversion.Target(config).Backup();
            var save = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            var applied = 0;
            try
            {
                foreach (var value in plan.Values)
                {
                    var setting = TempestSettings.Find(value.SettingId);
                    if (setting == null) continue;
                    settings.Entry(setting).BoxedValue = value.Value;
                    applied++;
                }
                if (plan.OtherDisabled.Count > 0)
                {
                    var other = (ConfigEntry<string>)settings.Entry(TempestSettings.Filter.OtherDisabled);
                    other.Value = SourceImport.MergeOther(other.Value, plan.OtherDisabled);
                    applied += plan.OtherDisabled.Count;
                }
                marker.Value = string.Join(",", marker.Value.Split(',').Where(p => p.Length > 0).Append(SourceName).Distinct());
            }
            finally
            {
                config.SaveOnConfigSet = save;
            }
            config.Save();
            var text = TempestText.Format(TempestTextKeys.ImportDone, applied, SourceName);
            log.Info(text);
            return text;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            registration.Dispose();
        }
    }
}
