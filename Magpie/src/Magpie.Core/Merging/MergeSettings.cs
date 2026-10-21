// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Magpie.Merging
{
    /// <summary>Merge settings (design 4.4), clamped to their ranges; a non-finite value reads as the default.</summary>
    public sealed class MergeSettings
    {
        public const float DefaultRadius = 3f;
        public const int DefaultMinCount = 2;
        public const int DefaultMaxMergesPerScan = 2;
        public const double DefaultSettleSeconds = 2;

        private readonly HashSet<string> ignore;

        public MergeSettings(float radius = DefaultRadius, int minCount = DefaultMinCount, int maxMergesPerScan = DefaultMaxMergesPerScan,
            double settleSeconds = DefaultSettleSeconds, IEnumerable<string>? ignore = null)
        {
            Radius = float.IsNaN(radius) || float.IsInfinity(radius) ? DefaultRadius : Math.Min(10f, Math.Max(0.5f, radius));
            MinCount = Math.Min(20, Math.Max(2, minCount));
            MaxMergesPerScan = Math.Min(20, Math.Max(1, maxMergesPerScan));
            SettleSeconds = double.IsNaN(settleSeconds) || double.IsInfinity(settleSeconds) ? DefaultSettleSeconds : Math.Min(30.0, Math.Max(0.0, settleSeconds));
            this.ignore = new HashSet<string>(
                (ignore ?? Enumerable.Empty<string>()).Select(name => (name ?? "").Trim()).Where(name => name.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Merge Radius, 0.5 to 10 m.</summary>
        public float Radius { get; }

        /// <summary>Merge Min Count: identical items within the radius of a target, the target included, 2 to 20.</summary>
        public int MinCount { get; }

        /// <summary>Max Merges Per Scan, 1 to 20.</summary>
        public int MaxMergesPerScan { get; }

        /// <summary>Merge Settle Time, 0 to 30 s.</summary>
        public double SettleSeconds { get; }

        /// <summary>Merge Ignore Items: prefab names, trimmed, case-insensitive.</summary>
        public bool Ignores(string prefab) => ignore.Contains(prefab ?? "");
    }
}
