// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Clean-room config names/units from TheGreatestShips README 8aa408759078a666b1d485cdd28d7a2db5cb5d09 only.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Keel.Catalog;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Tuning;

namespace ExtraPotions.Keel.Conversion
{
    internal static class TgsImport
    {
        static readonly Dictionary<string, KeelSetting> general = new Dictionary<string, KeelSetting>(StringComparer.Ordinal)
        {
            { "Boat Camera Max Distance", KeelSettings.Display.BoatCameraMaxDistance },
            { "Sail Opacity", KeelSettings.Display.SailOpacity },
            { "Sail Opacity Both Sides", KeelSettings.Display.SailOpacityBothSides },
            { "Sail Opacity On Deck", KeelSettings.Display.SailOpacityOnDeck },
            { "Name Vanilla Ships", KeelSettings.Display.NameVanillaShips },
        };
        internal static void Entry(SourceEntry entry, List<ImportValue> values, List<ShipImportValue> ships, List<ImportNote> notes)
        {
            string from = entry.Section + "." + entry.Key;
            if (entry.Section == "General" && general.TryGetValue(entry.Key, out var display)) { Add(display, entry, values, notes); return; }
            var type = ShipCatalog.All.FirstOrDefault(t => t.Name == entry.Section);
            if (type == null)
            {
                notes.Add(new ImportNote(ImportNoteKind.Skipped, from, "not a documented ship section or supported General key"));
                return;
            }
            var settings = KeelSettings.Catalog(type.Ship);
            KeelSetting? target = null;
            switch (entry.Key)
            {
                case "Recipe": target = settings.Recipe; break;
                case "Hull Width": target = settings.Width; break;
                case "Hull Length": target = settings.Length; break;
                case "Hull Scale": target = settings.Scale; break;
                case "Sail Color": target = settings.SailColor; break;
                case "Hull Color": target = settings.HullColor; break;
                case "Hull Stripes": target = settings.HullStripes; break;
            }
            if (target != null) { Add(target, entry, values, notes); return; }
            ShipField field;
            ShipImportUnit unit;
            switch (entry.Key)
            {
                case "Health": field = ShipField.Health; unit = ShipImportUnit.Absolute; break;
                case "Top Speed Multiplier": field = ShipField.SailForce; unit = ShipImportUnit.BaseHullSpeedRatio; break;
                case "Rudder Speed": field = ShipField.RudderSpeed; unit = ShipImportUnit.BaseHullRudderRatio; break;
                default: notes.Add(new ImportNote(ImportNoteKind.Skipped, from, "key not documented at the pinned README")); return;
            }
            if (!float.TryParse(entry.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value < 0 || float.IsNaN(value) || float.IsInfinity(value))
            {
                notes.Add(new ImportNote(ImportNoteKind.Fallback, from, "invalid finite nonnegative ship value; not imported"));
                return;
            }
            ships.RemoveAll(v => v.Prefab == type.Prefab && v.Field == field);
            ships.Add(new ShipImportValue(type.Prefab, field, value, from, unit));
        }
        static void Add(KeelSetting target, SourceEntry entry, List<ImportValue> values, List<ImportNote> notes)
        {
            string from = entry.Section + "." + entry.Key;
            values.RemoveAll(v => v.SettingId == target.Id);
            values.Add(new ImportValue(target.Id, SourceImport.Convert(target, entry.Value, from, notes), from));
        }
    }
}
