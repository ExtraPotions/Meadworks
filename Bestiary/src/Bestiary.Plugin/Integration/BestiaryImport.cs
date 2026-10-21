// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: C09 converter registration for EliteCreaturesPack's config (design 11.2). The source file is read
// only; Apply backs up Bestiary's config first. The CreatureManager workshop import (X22) comes with the workshop.
using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Bestiary.Config;
using ExtraPotions.Bestiary.Conversion;
using ExtraPotions.Bestiary.Runtime.Settings;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Bestiary.Runtime.Integration
{
    internal sealed class BestiaryImport : IDisposable
    {
        private readonly ConfigFile config;
        private readonly BestiaryConfig settings;
        private readonly MeadworksLog log;
        private readonly ConfigEntry<bool> imported;
        private readonly IDisposable registration;
        private bool disposed;

        public BestiaryImport(ConfigFile config, BestiaryConfig settings, MeadworksLog log)
        {
            this.config = config;
            this.settings = settings;
            this.log = log;
            imported = MeadworksConfig.BindHidden(config, "Import", "Elite Creatures Pack Imported", false, "Managed by Bestiary: the Elite Creatures Pack settings were imported once.");
            registration = MeadworksConversion.Register(new ConversionRegistration("bestiary.elitecreaturespack", Plugin.Guid,
                StepAsideRules.EcpGuid, "Elite Creatures Pack",
                () => !disposed && File.Exists(SourcePath) && !imported.Value,
                Preview,
                (document, resolutions, backup) => Apply(document as ImportPlan ?? throw new ArgumentException("Wrong Bestiary import preview."), backup)));
        }

        private static string SourcePath => Path.Combine(Paths.ConfigPath, EcpImport.FileName);

        public ImportPlan Preview()
        {
            var plan = EcpImport.Plan(File.ReadAllText(SourcePath));
            var report = Path.Combine(Paths.ConfigPath, "Meadworks", "bestiary", "import-elitecreaturespack.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            File.WriteAllText(report, plan.Report());
            log.Info("Import preview for Elite Creatures Pack: " + plan.Values.Count + " values, " + plan.Notes.Count + " notes (" + report + ").");
            return plan;
        }

        private object Apply(ImportPlan plan, bool backup)
        {
            if (!backup) throw new InvalidOperationException("Bestiary import requires a backup.");
            MeadworksConversion.Target(config).Backup();
            var save = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            var applied = 0;
            try
            {
                foreach (var value in plan.Values)
                {
                    var spec = BestiarySettings.Find(value.SettingId);
                    if (spec == null) continue;
                    if (spec.Scope != SettingScope.Local && !MeadworksConfig.CanEdit(BestiaryConfig.Map(spec.Scope))) continue;
                    settings.Raw(spec).BoxedValue = value.Value is BiomeMask mask ? (object)(Heightmap.Biome)(int)mask : value.Value;
                    applied++;
                }
                imported.Value = true;
            }
            finally
            {
                config.SaveOnConfigSet = save;
            }
            config.Save();
            var text = "Imported " + applied + " Elite Creatures Pack settings into Bestiary.";
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
