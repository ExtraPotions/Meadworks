// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Threshold's C09 preview and apply for the PortalSelector import (design 9.2, T07b), the same
// registration shape as Edge's EdgeSourceConversion (expDARE). PortalSelector's config file is read as data only and
// never edited; Threshold's own config is written through Crucible's validated config target with a backup.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Conversion;
using ExtraPotions.Threshold.Localization;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Threshold.Runtime.Conversion
{
    /// <summary>Registers "PortalSelector" with Crucible's converter while its config file exists and this machine holds
    /// the authority (single player, host or dedicated server). Apply needs this converter's fresh preview, unchanged
    /// files and settings, and backups; a preview is never replayed.</summary>
    public sealed class ThresholdSourceConversion : IDisposable
    {
        sealed class Plan
        {
            internal long Generation;
            internal string SourceHash = "";
            internal string ConfigHash = "";
            internal readonly Dictionary<string, object> Settings = new Dictionary<string, object>(StringComparer.Ordinal);
            internal readonly Dictionary<string, object> Before = new Dictionary<string, object>(StringComparer.Ordinal);
            internal IReadOnlyList<string> Lines = Array.Empty<string>();
            public override string ToString() => string.Join("\n", Lines);
        }

        readonly bool wayfare;
        string SourceName => wayfare ? WayfareImport.SourceName : PortalSelectorImport.SourceName;
        readonly ConfigFile config;
        readonly ThresholdConfig settings;
        readonly MeadworksLog log;
        IDisposable? registration;
        Plan? current;
        bool disposed;

        string SourcePath => Path.Combine(Paths.ConfigPath, wayfare ? WayfareImport.FileName : PortalSelectorImport.FileName);

        public ThresholdSourceConversion(ConfigFile config, ThresholdConfig settings, MeadworksLog log, bool wayfare = false)
        {
            this.wayfare = wayfare;
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            registration = MeadworksConversion.Register(new ConversionRegistration(wayfare ? WayfareImport.RegistrationId : PortalSelectorImport.RegistrationId, Plugin.Guid,
                wayfare ? WayfareImport.SourceGuid : PortalSelectorImport.SourceGuid, SourceName,
                () => CanPreview && File.Exists(SourcePath),
                () => current = Preview(),
                (document, choices, backup) => Apply(document, backup)));
        }

        bool CanPreview => !disposed && Plugin.Ready && CruciblePlugin.Session.IsAuthority;

        static string Hash(string path)
        {
            if (!File.Exists(path)) return "missing";
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }

        Plan Preview()
        {
            if (!CanPreview) throw new InvalidOperationException("The current authority is required to preview the " + SourceName + " import.");
            var plan = new Plan { Generation = CruciblePlugin.Session.Generation, SourceHash = Hash(SourcePath), ConfigHash = Hash(config.ConfigFilePath) };
            if (plan.SourceHash == "missing") throw new FileNotFoundException(SourceName + "'s configuration is missing.", SourcePath);
            var text = File.ReadAllText(SourcePath);
            if (Hash(SourcePath) != plan.SourceHash) throw new InvalidOperationException(SourceName + "'s configuration changed while reading.");
            foreach (var setting in ThresholdSettings.All) plan.Before[setting.Id] = settings.Entry(setting).BoxedValue;
            var marker = Convert.ToString(plan.Before[ThresholdSettings.Internal.ImportedFrom.Id], CultureInfo.InvariantCulture);
            var import = wayfare ? WayfareImport.Plan(text) : PortalSelectorImport.Plan(text);
            foreach (var item in import.Items) plan.Settings[item.Setting.Id] = item.Value;
            if (import.Items.Count > 0) plan.Settings[ThresholdSettings.Internal.ImportedFrom.Id] = ImportMarker.Add(marker, SourceName);
            if (plan.Settings.Count == 0) throw new FormatException(SourceName + "'s configuration holds no setting Threshold imports.");
            plan.Lines = import.Notes.Select(note => ThresholdText.Text(ThresholdTextKeys.Note(note)))
                .Concat(plan.Settings.Select(pair => "Set " + pair.Key + " = " + pair.Value)).ToArray();
            ((IValidatedConversionTarget)MeadworksConversion.Target(config)).Validate(plan.Settings);
            return plan;
        }

        object Apply(object document, bool backup)
        {
            var plan = current;
            if (!CanPreview || !backup || plan == null || !ReferenceEquals(document, plan) || plan.Generation != CruciblePlugin.Session.Generation)
                throw new InvalidOperationException("Apply requires the current authority, this converter's fresh preview and backups.");
            foreach (var pair in plan.Before)
                if (!Equals(settings.Entry(ThresholdSettings.All.Single(s => s.Id == pair.Key)).BoxedValue, pair.Value))
                    throw new InvalidOperationException("Threshold settings changed after Preview; preview again.");
            if (Hash(SourcePath) != plan.SourceHash || Hash(config.ConfigFilePath) != plan.ConfigHash)
                throw new InvalidOperationException("A configuration file changed after Preview; preview again.");
            var target = (IValidatedConversionTarget)MeadworksConversion.Target(config);
            target.Validate(plan.Settings);
            target.Backup();
            plan.Generation = -1; // a preview is never replayed, whatever the outcome
            target.WriteAtomic(plan.Settings);
            var text = ThresholdText.Format(ThresholdTextKeys.ImportDone, plan.Settings.Count - 1, SourceName);
            ThresholdText.Centre(text, log);
            log.Info(text);
            return "Applied with a backup. " + SourceName + "'s file is retained.";
        }

        public void Dispose()
        {
            disposed = true;
            current = null;
            registration?.Dispose();
            registration = null;
        }
    }
}
