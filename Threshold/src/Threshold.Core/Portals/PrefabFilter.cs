// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Portal Prefabs setting (design A6, 4.1). PortalSelector's option of the same meaning is used by
// name and default value only.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Threshold.Config;

namespace ExtraPotions.Threshold.Portals
{
    /// <summary>Which portal prefabs the list includes. Empty: every prefab with a portal component (widened as Wayfare's
    /// discovery does); otherwise exactly the named prefabs.</summary>
    public sealed class PrefabFilter
    {
        /// <summary>PortalSelector's default value, the three vanilla portals.</summary>
        public static readonly IReadOnlyList<string> PortalSelectorDefault = Array.AsReadOnly(new[] { "portal_wood", "portal_stone", "portal" });

        private readonly HashSet<string>? names;

        private PrefabFilter(HashSet<string>? names) => this.names = names;

        public bool IncludesEveryPortal => names == null;

        public IReadOnlyCollection<string> Names => (IReadOnlyCollection<string>?)names ?? Array.Empty<string>();

        public static PrefabFilter Parse(string? value)
        {
            var list = ThresholdSettings.NameList(value ?? "");
            return new PrefabFilter(list.Length == 0 ? null : new HashSet<string>(list.Split(','), StringComparer.Ordinal));
        }

        /// <summary>Whether a portal prefab of this name is listed. Prefab names are exact (the game hashes them as typed).</summary>
        public bool Includes(string prefab) => !string.IsNullOrEmpty(prefab) && (names == null || names.Contains(prefab));

        /// <summary>A value naming exactly PortalSelector's default set imports as empty (every portal).</summary>
        public static bool IsPortalSelectorDefault(string? value)
        {
            var parsed = Parse(value);
            return !parsed.IncludesEveryPortal && parsed.Names.Count == PortalSelectorDefault.Count && PortalSelectorDefault.All(parsed.Includes);
        }
    }
}
