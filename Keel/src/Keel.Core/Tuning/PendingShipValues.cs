// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: imported per-ship values for ships not present yet, kept in one hidden C01 text until the ship
// appears (design 9.2: "kept as pending and applied when the prefab appears").
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Keel.Tuning
{
    public static class PendingShipValues
    {
        /// <summary><c>Prefab.Field=value</c> parts joined by <c>;</c>. Values are config texts (numbers, true or false).</summary>
        public static string Encode(IEnumerable<(string Prefab, ShipField Field, string Value)> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            return string.Join(";", values.Select(v => ShipFields.Key(v.Prefab, v.Field) + "=" + (v.Value ?? "").Trim()));
        }

        /// <summary>Adds every well-formed part to the registry's pending values; returns how many were added.</summary>
        public static int Load(string? text, ShipRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var added = 0;
            foreach (var part in (text ?? "").Split(';'))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0) continue;
                var key = part.Substring(0, eq).Trim();
                var dot = key.LastIndexOf('.');
                if (dot <= 0) continue;
                var prefab = key.Substring(0, dot);
                var name = key.Substring(dot + 1);
                if (!Enum.TryParse<ShipField>(name, false, out var field) || field.ToString() != name) continue;
                registry.AddPending(prefab, field, part.Substring(eq + 1).Trim());
                added++;
            }
            return added;
        }
    }
}