// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Threshold.Config;
namespace ExtraPotions.Threshold.Conversion
{
    public static class WayfareImport
    {
        public const string SourceGuid = "com.Wayfare", SourceName = "Wayfare", FileName = "com.Wayfare.cfg", RegistrationId = "threshold.Wayfare";
        public static ImportPlan Plan(string? text)
        {
            var value = SourceConfig.Parse(text).Where(entry => entry.Section == "General" && entry.Key == "Teleport Mode").Select(entry => entry.Value).FirstOrDefault();
            var items = new List<ImportItem>();var notes = new List<ImportNote>();
            if (value != null)
            {
                if (Enum.TryParse<TeleportMode>(value, false, out var mode) && Enum.IsDefined(typeof(TeleportMode), mode) && !int.TryParse(value, out _))
                    items.Add(new ImportItem(ThresholdSettings.General.TeleportMode, mode.ToString(), "General.Teleport Mode"));
                else notes.Add(ImportNote.InvalidValue);
            }
            return new ImportPlan(items, notes);
        }
    }
}
