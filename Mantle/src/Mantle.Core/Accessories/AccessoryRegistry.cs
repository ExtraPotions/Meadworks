// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace Mantle.Accessories
{
    /// <summary>
    /// The data behind the ring and belt slots: which prefabs each slot takes and what each gives. It is built from the
    /// built-in entries (<c>AccessoryCatalog</c>) with Mantle.Accessories.yml laid over them, so a slot takes whatever the
    /// registry names and new items need no slot code. A slot takes only an item with an enabled entry for it; a prefab
    /// no entry names is an ordinary item. All members are plain data and have no game types.
    /// </summary>
    public sealed class AccessoryRegistry
    {
        private readonly Dictionary<string, AccessoryEntry> entries = new Dictionary<string, AccessoryEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The registry in use. Replaced as a whole by <see cref="Load"/>.</summary>
        public static AccessoryRegistry Current { get; private set; } = new AccessoryRegistry();

        public IReadOnlyCollection<AccessoryEntry> All => entries.Values;

        /// <summary>The built-in entries with the file's overrides over them; unusable overrides are named in <paramref name="warnings"/>.</summary>
        public static AccessoryRegistry Build(IEnumerable<AccessoryEntry> builtIn, IEnumerable<AccessoryOverride> overrides, ICollection<string> warnings = null)
        {
            AccessoryRegistry registry = new AccessoryRegistry();
            foreach (AccessoryEntry entry in builtIn)
                registry.entries[entry.Id] = entry;
            foreach (AccessoryOverride change in overrides ?? Array.Empty<AccessoryOverride>())
            {
                if (string.IsNullOrWhiteSpace(change.Id))
                    continue;
                string id = change.Id.Trim();
                if (registry.entries.TryGetValue(id, out AccessoryEntry known))
                    registry.entries[known.Id] = known.With(change);
                else if (change.Slot.HasValue)
                    registry.entries[id] = new AccessoryEntry(id, change.Slot.Value, change.ApplyTo(AccessoryStats.None), enabled: change.Enabled ?? true);
                else
                    warnings?.Add($"{id}: not one of Mantle's items; add slot: ring or slot: belt to tag it");
            }
            return registry;
        }

        /// <summary>Makes a registry the one in use.</summary>
        public static void Load(AccessoryRegistry registry) => Current = registry ?? new AccessoryRegistry();

        public AccessoryEntry Find(string prefab) =>
            prefab != null && entries.TryGetValue(prefab, out AccessoryEntry entry) ? entry : null;

        /// <summary>The slot an item belongs to, or null for an item the registry does not tag (or one it disabled).</summary>
        public AccessorySlot? SlotOf(string prefab)
        {
            AccessoryEntry entry = Find(prefab);
            return entry != null && entry.Enabled ? entry.Slot : (AccessorySlot?)null;
        }

        /// <summary>
        /// What the items lying in the accessory cells give together: each item adds its entry's stats once, and only
        /// while it lies in a cell of its own kind, so two identical rings give exactly double and an item in the wrong cell
        /// (a broken save) gives nothing.
        /// </summary>
        public AccessoryStats Total(IEnumerable<KeyValuePair<AccessorySlot, string>> inCells)
        {
            AccessoryStats total = AccessoryStats.None;
            foreach (KeyValuePair<AccessorySlot, string> cell in inCells)
                total = Add(total, cell.Key, cell.Value);
            return total;
        }

        /// <summary>One item added to a running total (<see cref="Total"/> without a list, for the per-frame path).</summary>
        public AccessoryStats Add(AccessoryStats total, AccessorySlot cell, string prefab)
        {
            AccessoryEntry entry = Find(prefab);
            return entry != null && entry.Enabled && entry.Slot == cell ? total.Plus(entry.Stats) : total;
        }
    }
}
