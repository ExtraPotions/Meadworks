// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: one read of BepInEx/config/Solstice.seasons.json into settings that are still at their defaults.
using System;
using System.IO;
using System.Security.Cryptography;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Config;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// If <c>Solstice.seasons.json</c> is present, fills Solstice settings that still equal their defaults.
    /// A marker file records the file hash so a later reset of the config is not overwritten. A changed file is read again,
    /// and again only fills defaults. The season clock is not in the file. Main thread only.
    /// </summary>
    public static class SeasonImport
    {
        const string FileName = "Solstice.seasons.json";
        const string MarkerName = "com.extrapotions.solstice.seasons-import";

        public static void Run(SolsticeConfig settings, MeadworksLog log)
        {
            if (settings == null || log == null) return;
            var path = Path.Combine(BepInEx.Paths.ConfigPath, FileName);
            if (!File.Exists(path)) return;
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception error)
            {
                log.Warning("Seasons import was not read: " + error.GetType().Name + ".");
                return;
            }
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            var markerPath = Path.Combine(BepInEx.Paths.ConfigPath, MarkerName);
            if (File.Exists(markerPath) && string.Equals(ReadMarker(markerPath), hash, StringComparison.Ordinal)) return;
            var plan = SeasonConfigImport.Plan(System.Text.Encoding.UTF8.GetString(bytes));
            if (!plan.Ok)
            {
                log.Warning("Seasons import was not applied: " + plan.Error);
                return;
            }
            int applied = settings.ApplyImport(plan.Changes);
            try { File.WriteAllText(markerPath, hash); }
            catch (Exception error)
            {
                log.Warning("Seasons import marker was not saved: " + error.GetType().Name + ".");
            }
            log.Info("Seasons import applied " + applied + " default setting" + (applied == 1 ? "" : "s")
                + ". Left unmapped: " + plan.Unmapped.Count + ". Unknown fields: " + plan.Unknown.Count + ".");
        }

        static string ReadMarker(string path)
        {
            try { return File.ReadAllText(path).Trim(); }
            catch (Exception) { return ""; }
        }
    }
}
