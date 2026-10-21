// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Identity rule restated from Burrow ItemCompatibility.Key (Burrow revision 166dcc45ad85d2fa8daa60cac142a84c32970b3a,
// src/Burrow.Plugin/Adopted/Storage/Shared/ItemCompatibility.cs): the complete saved identity without count, position
// or operational flags. Burrow serializes a game ItemData; Magpie.Core is Unity-free, so the plugin fills these
// portable facts from the game item and this file only compares them. No Burrow code is copied.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Magpie.Items
{
    /// <summary>
    /// The saved identity of one item, without stack count, grid position, equipped or picked-up flags (design B5).
    /// Includes the game's cheated mark and Magpie's loot mark so marked and unmarked items never compare equal (E2, 3.4).
    /// Immutable: custom data is copied on construction.
    /// </summary>
    public sealed class ItemIdentity
    {
        private readonly KeyValuePair<string, string>[] customData;

        public ItemIdentity(string prefab, int quality, int variant, long crafterId, string crafterName, float durability,
            int worldLevel, IReadOnlyDictionary<string, string>? customData, bool cheated, bool loot)
        {
            Prefab = prefab ?? "";
            Quality = quality;
            Variant = variant;
            CrafterId = crafterId;
            CrafterName = crafterName ?? "";
            // Durability rule: compared exactly (as Burrow's serialized key does); signed zero reads as zero.
            Durability = durability == 0f ? 0f : durability;
            WorldLevel = worldLevel;
            this.customData = (customData ?? new Dictionary<string, string>())
                .Select(pair => new KeyValuePair<string, string>(pair.Key ?? "", pair.Value ?? ""))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray();
            Cheated = cheated;
            Loot = loot;
        }

        /// <summary>Drop prefab name (the game's <c>m_dropPrefab</c> name), compared ordinally.</summary>
        public string Prefab { get; }
        public int Quality { get; }
        public int Variant { get; }
        public long CrafterId { get; }
        public string CrafterName { get; }
        public float Durability { get; }
        public int WorldLevel { get; }

        /// <summary>Custom data pairs, sorted by key (ordinal).</summary>
        public IReadOnlyList<KeyValuePair<string, string>> CustomData => customData;

        /// <summary>The game's cheated mark on the item (achievement guard).</summary>
        public bool Cheated { get; }

        /// <summary>Magpie's world loot mark (<c>magpie.loot</c>, design 3.4).</summary>
        public bool Loot { get; }

        /// <summary>
        /// False when the facts cannot identify an item reliably: empty prefab, negative quality, variant or world level,
        /// or a non-finite durability. Such an item has no key and never merges.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(Prefab) && Quality >= 0 && Variant >= 0 && WorldLevel >= 0
            && !float.IsNaN(Durability) && !float.IsInfinity(Durability);

        /// <summary>Canonical identity string: every field escaped, culture-invariant, durability by its exact bits.</summary>
        internal string? CanonicalKey()
        {
            if (!IsValid)
                return null;
            var inv = CultureInfo.InvariantCulture;
            var parts = new List<string>
            {
                "v1",
                "p=" + Escape(Prefab),
                "q=" + Quality.ToString(inv),
                "v=" + Variant.ToString(inv),
                "ci=" + CrafterId.ToString(inv),
                "cn=" + Escape(CrafterName),
                "d=" + BitConverter.SingleToInt32Bits(Durability).ToString("x8", inv),
                "wl=" + WorldLevel.ToString(inv),
                "ch=" + (Cheated ? "1" : "0"),
                "lt=" + (Loot ? "1" : "0"),
                "cd=" + string.Join(",", customData.Select(pair => Escape(pair.Key) + "=" + Escape(pair.Value))),
            };
            return string.Join(";", parts);
        }

        private static string Escape(string value) =>
            value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("=", "\\=");
    }
}
