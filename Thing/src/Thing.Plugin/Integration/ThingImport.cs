// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: C09 converter registration for Valheim_Admin_Tool's admin-function hotkeys (design 10). The source file
// is read only; Apply backs up Thing's config first. Same model as Tempest, Keel and Ember.
using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Config;
using ExtraPotions.Thing.Conversion;
using ExtraPotions.Thing.Localization;

namespace ExtraPotions.Thing.Runtime
{
    public sealed class ThingImport : IDisposable
    {
        public const string Section = "Import";
        const string SourceName = "Valheim_Admin_Tool";

        readonly ConfigFile config;
        readonly ThingConfig settings;
        readonly MeadworksLog log;
        readonly ConfigEntry<string> marker;
        readonly IDisposable registration;
        bool disposed;

        public ThingImport(ConfigFile config, ThingConfig settings, MeadworksLog log)
        {
            this.config = config;
            this.settings = settings;
            this.log = log;
            marker = MeadworksConfig.BindHidden(config, Section, "Imported Sources", "", "Managed by Thing: sources already imported.");
            var from = Integration.ModuleStepAside.AdminTool;
            registration = MeadworksConversion.Register(new ConversionRegistration("thing.valheimadmintool", Plugin.Guid, from.Guid, from.Name,
                () => !disposed && File.Exists(SourcePath) && !Imported && MeadworksHeadless.AllowsUi,
                () => Preview(),
                (document, resolutions, backup) => Apply(document as ImportPlan ?? throw new ArgumentException("Wrong Thing import preview."), backup)));
        }

        static string SourcePath => Path.Combine(Paths.ConfigPath, SourceImport.AdminToolFile);

        bool Imported => marker.Value.Split(',').Contains(SourceName);

        public ImportPlan Preview()
        {
            var plan = SourceImport.Plan(File.ReadAllText(SourcePath));
            var report = Path.Combine(ThingPaths.DataFolder, "import-valheimadmintool.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            File.WriteAllText(report, plan.Report());
            log.Info("Import preview for Valheim_Admin_Tool: " + plan.Values.Count + " hotkeys, " + plan.Notes.Count + " notes (" + report + ").");
            return plan;
        }

        object Apply(ImportPlan plan, bool backup)
        {
            if (!backup) throw new InvalidOperationException("Thing import requires a backup.");
            MeadworksConversion.Target(config).Backup();
            var save = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            var applied = 0;
            try
            {
                foreach (var value in plan.Values)
                {
                    if (!(ThingSettings.Find(value.SettingId) is ThingSetting<string> setting) || !setting.Hotkey) continue;
                    try
                    {
                        settings.Shortcut(setting).Value = KeyboardShortcut.Deserialize(value.Value);
                        applied++;
                    }
                    catch (Exception error)
                    {
                        log.Warning("Import skipped " + value.SettingId + ": " + error.GetType().Name);
                    }
                }
                marker.Value = string.Join(",", marker.Value.Split(',').Where(p => p.Length > 0).Append(SourceName).Distinct());
            }
            finally
            {
                config.SaveOnConfigSet = save;
            }
            config.Save();
            var text = ThingText.Format(ThingTextKeys.ImportDone, applied, SourceName);
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
