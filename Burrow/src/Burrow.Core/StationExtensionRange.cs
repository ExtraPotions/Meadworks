// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Station Extension Range Multiplier arithmetic (user decision 2026-10-12 23:31), kept free of the game for tests.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Burrow
{
    /// <summary>
    /// The distance an upgrade tool (the game's StationExtension: anvils, tanning rack, chopping block, adze and so on)
    /// may stand from its crafting station: the vanilla distance of its prefab times the multiplier. The vanilla
    /// distance is remembered the first time a prefab is seen and never read back from a changed value, so applying
    /// again after a reload or a setting change never compounds.
    /// </summary>
    public sealed class StationExtensionRange
    {
        public const float Default = 2f, Min = 1f, Max = 10f;

        private readonly Dictionary<string, float> vanilla = new Dictionary<string, float>(StringComparer.Ordinal);

        /// <summary>The multiplier within 1 to 10; anything not finite counts as 1 (vanilla).</summary>
        public static float Clamp(float multiplier)
            => float.IsNaN(multiplier) || float.IsInfinity(multiplier) ? Min : Math.Max(Min, Math.Min(Max, multiplier));

        /// <summary>
        /// The distance for an extension of <paramref name="prefab"/> whose field currently holds <paramref name="current"/>.
        /// The first value seen for a prefab is its vanilla distance; later values are ignored.
        /// </summary>
        public float Effective(string prefab, float current, float multiplier)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (!vanilla.TryGetValue(prefab, out float baseDistance))
                vanilla[prefab] = baseDistance = current;
            return baseDistance * Clamp(multiplier);
        }

        /// <summary>The remembered vanilla distance of a prefab.</summary>
        public bool TryGetVanilla(string prefab, out float distance) => vanilla.TryGetValue(prefab, out distance);
    }
}
