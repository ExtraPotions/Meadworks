// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the discovery model of design A2 and 9.2 (ships register once; imported values for ships not
// present this session wait until the ship appears).
using System;
using System.Collections.Generic;

namespace ExtraPotions.Keel.Tuning
{
    public sealed class ShipRegistry
    {
        private readonly Dictionary<string, ShipDefaults> ships = new Dictionary<string, ShipDefaults>(StringComparer.Ordinal);
        private readonly List<string> order = new List<string>();
        private readonly Dictionary<string, Dictionary<ShipField, string>> pending = new Dictionary<string, Dictionary<ShipField, string>>(StringComparer.Ordinal);

        /// <summary>Registered prefab names in discovery order.</summary>
        public IReadOnlyList<string> Names => order;

        /// <summary>Registers a ship prefab with its vanilla values; false when it was already known (the first defaults win).</summary>
        public bool Register(string prefab, ShipDefaults defaults)
        {
            if (string.IsNullOrWhiteSpace(prefab)) throw new ArgumentException("Expected a prefab name.", nameof(prefab));
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));
            if (ships.ContainsKey(prefab)) return false;
            ships.Add(prefab, defaults);
            order.Add(prefab);
            return true;
        }

        public bool TryGet(string prefab, out ShipDefaults defaults) => ships.TryGetValue(prefab ?? "", out defaults!);

        /// <summary>Keeps an imported value (config text) for a ship not present yet; a later value for the same field wins.</summary>
        public void AddPending(string prefab, ShipField field, string value)
        {
            if (string.IsNullOrWhiteSpace(prefab)) throw new ArgumentException("Expected a prefab name.", nameof(prefab));
            if (!pending.TryGetValue(prefab, out var fields)) pending[prefab] = fields = new Dictionary<ShipField, string>();
            fields[field] = value ?? "";
        }

        /// <summary>Every value still waiting, ships and fields in insertion order.</summary>
        public IEnumerable<(string Prefab, ShipField Field, string Value)> Pending()
        {
            foreach (var ship in pending)
                foreach (var field in ship.Value)
                    yield return (ship.Key, field.Key, field.Value);
        }

        /// <summary>The pending values of a ship, removed from the registry; empty when there are none.</summary>
        public IReadOnlyDictionary<ShipField, string> TakePending(string prefab)
        {
            if (prefab == null || !pending.TryGetValue(prefab, out var fields)) return new Dictionary<ShipField, string>();
            pending.Remove(prefab);
            return fields;
        }
    }
}