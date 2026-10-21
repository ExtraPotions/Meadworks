// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: lists the AutoConnector import under Almanac's Convert tools through Crucible's conversion registry.
// Preview and Apply are the console verbs' own logic; blanking the source stays on the console (`beacon import blank-source confirm`).
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using ExtraPotions.Beacon.Conversion;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Beacon.Runtime
{
    internal static class BeaconConversion
    {
        static IDisposable? registration;

        /// <summary>A password-free report. Almanac shows it through <see cref="ToString"/>.</summary>
        sealed class Report
        {
            internal readonly string[] Lines;
            internal readonly (string? Source, string? List) Stamp;
            internal Report(string[] lines, (string?, string?) stamp) { Lines = lines; Stamp = stamp; }
            public override string ToString() => string.Join("\n", Lines);
        }

        static string SourcePath => Path.Combine(Paths.ConfigPath, AutoConnectorImport.SourceFile.Replace('/', Path.DirectorySeparatorChar));

        public static void Register()
        {
            if (registration != null) return;
            registration = MeadworksConversion.Register(new ConversionRegistration("beacon.autoconnector", Plugin.Guid,
                AutoConnectorImport.SourceGuid, "AutoConnector",
                () => Plugin.Ready && !BeaconImportSecrets.Busy && File.Exists(SourcePath),
                () => new Report(BeaconCommand.Preview(), BeaconCommand.PreviewStamp),
                Apply));
        }

        static object Apply(object document, IReadOnlyDictionary<string, string> choices, bool backup)
        {
            if (!(document is Report report)) throw new ArgumentException("Wrong Beacon import preview.");
            // A newer preview (console or Almanac) or a changed list makes this plan stale.
            if (report.Stamp.Source == null || report.Stamp != BeaconCommand.PreviewStamp)
                return new Report(new[] { BeaconText.Text("import_preview_again") }, default);
            if (backup && File.Exists(BeaconServers.FilePath)) File.Copy(BeaconServers.FilePath, BeaconServers.FilePath + ".bak", true);
            return new Report(BeaconCommand.Apply(Plugin.Log), default);
        }

        public static void Reset()
        {
            registration?.Dispose();
            registration = null;
        }
    }
}
