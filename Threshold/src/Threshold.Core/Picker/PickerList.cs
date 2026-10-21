// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the picker list (design A2, A3, A7, A9). Group order and favorites follow PotalMap's documented
// behavior (Favorites, then Private, Public; a favorite leaves its own group and keeps its category label); hubs are a
// Threshold group and Admin portals come last. No Guild group (user decision U2, 2026-10-08). Accent folding as in Kindle's search (A4).
// The Stone group (Highlight Stone Portals) gathers the portals that let every item through, so ore destinations stand out.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ExtraPotions.Threshold.Access;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Portals;

namespace ExtraPotions.Threshold.Picker
{
    /// <summary>List groups in display order. Stone holds the non-hub portals that let every item through while Highlight
    /// Stone Portals is on.</summary>
    public enum PickerCategory { Favorites, Hubs, Stone, Private, Public, Admin }

    public sealed class PickerRow
    {
        internal PickerRow(PortalEntry portal, PickerCategory category, string label, double distance, bool favourite)
        {
            Portal = portal;
            Category = category;
            Label = label;
            Distance = distance;
            IsFavourite = favourite;
        }

        public PortalEntry Portal { get; }

        /// <summary>The portal's own category, also for a favorite (shown as "Category - Name").</summary>
        public PickerCategory Category { get; }

        /// <summary>Hub name for a hub, else the tag as typed (empty for an untagged portal).</summary>
        public string Label { get; }

        /// <summary>Map distance from the player, meters.</summary>
        public double Distance { get; }

        public bool IsFavourite { get; }
    }

    public sealed class PickerGroup
    {
        internal PickerGroup(PickerCategory category, IReadOnlyList<PickerRow> rows)
        {
            Category = category;
            Rows = rows;
        }

        public PickerCategory Category { get; }
        public IReadOnlyList<PickerRow> Rows { get; }
    }

    /// <summary>Everything the list reads. <paramref name="explored"/> is null when Only Explored Portals is off.</summary>
    public sealed class PickerQuery
    {
        public PickerQuery(IReadOnlyList<PortalEntry> world, PortalEntry? current, MapPoint player, AccessViewer viewer, bool accessModesEnabled,
            string search, SortOrder sort, FavouriteSet favourites, bool listUntagged, Func<MapPoint, bool>? explored, PrefabFilter prefabs,
            bool stoneGroup = false)
        {
            World = world ?? Array.Empty<PortalEntry>();
            Current = current;
            Player = player;
            Viewer = viewer;
            AccessModesEnabled = accessModesEnabled;
            Search = search ?? "";
            Sort = sort;
            Favourites = favourites ?? new FavouriteSet(Array.Empty<string>());
            ListUntagged = listUntagged;
            Explored = explored;
            Prefabs = prefabs ?? PrefabFilter.Parse("");
            StoneGroup = stoneGroup;
        }

        public IReadOnlyList<PortalEntry> World { get; }
        public PortalEntry? Current { get; }
        public MapPoint Player { get; }
        public AccessViewer Viewer { get; }
        public bool AccessModesEnabled { get; }
        public string Search { get; }
        public SortOrder Sort { get; }
        public FavouriteSet Favourites { get; }
        public bool ListUntagged { get; }
        public Func<MapPoint, bool>? Explored { get; }
        public PrefabFilter Prefabs { get; }

        /// <summary>Highlight Stone Portals: portals that let every item through get the Stone group.</summary>
        public bool StoneGroup { get; }
    }

    public static class PickerList
    {
        public static PickerCategory CategoryOf(PortalEntry portal, bool accessModesEnabled, bool stoneGroup = false)
        {
            if (portal.IsHub) return PickerCategory.Hubs;
            if (stoneGroup && portal.AllowsAllItems) return PickerCategory.Stone;
            switch (PortalAccessRules.EffectiveMode(portal.Mode, accessModesEnabled))
            {
                case AccessMode.Private: return PickerCategory.Private;
                case AccessMode.Admin: return PickerCategory.Admin;
                default: return PickerCategory.Public;
            }
        }

        public static string LabelOf(PortalEntry portal) => portal.IsHub ? portal.HubName : portal.Tag;

        /// <summary>Non-empty groups in <see cref="PickerCategory"/> order.</summary>
        public static IReadOnlyList<PickerGroup> Build(PickerQuery query)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var words = TextFold.Fold(query.Search).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var rows = new List<PickerRow>();
            foreach (var p in query.World)
            {
                if (query.Current != null && p.Id.Equals(query.Current.Id)) continue;
                if (!query.Prefabs.Includes(p.Prefab)) continue;
                if (!p.IsHub && !p.IsTagged && !query.ListUntagged) continue;
                if (!PortalAccessRules.MayTarget(p, query.Viewer, query.AccessModesEnabled)) continue;
                if (query.Explored != null && !query.Explored(p.Position)) continue;
                var label = LabelOf(p);
                if (words.Length > 0)
                {
                    var folded = TextFold.Fold(label);
                    if (!words.All(w => folded.Contains(w))) continue;
                }
                rows.Add(new PickerRow(p, CategoryOf(p, query.AccessModesEnabled, query.StoneGroup), label, p.Position.FlatDistance(query.Player),
                    query.Favourites.Contains(PortalKey.Of(p.Position))));
            }
            var groups = new List<PickerGroup>();
            foreach (PickerCategory category in Enum.GetValues(typeof(PickerCategory)))
            {
                var members = category == PickerCategory.Favorites
                    ? rows.Where(r => r.IsFavourite)
                    : rows.Where(r => !r.IsFavourite && r.Category == category);
                var ordered = Order(members, query.Sort).ToList();
                if (ordered.Count > 0) groups.Add(new PickerGroup(category, ordered));
            }
            return groups;
        }

        private static IEnumerable<PickerRow> Order(IEnumerable<PickerRow> rows, SortOrder sort) =>
            sort == SortOrder.Distance
                ? rows.OrderBy(r => r.Distance).ThenBy(r => TextFold.Fold(r.Label), StringComparer.Ordinal)
                : rows.OrderBy(r => TextFold.Fold(r.Label), StringComparer.Ordinal).ThenBy(r => r.Distance);
    }

    /// <summary>Culture-invariant search folding: lower case, accents removed, sharp s as ss, runs of white space as one space.</summary>
    public static class TextFold
    {
        public static string Fold(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var decomposed = text!.Normalize(NormalizationForm.FormD);
            var result = new StringBuilder(decomposed.Length);
            bool space = false;
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsWhiteSpace(c)) { space = result.Length > 0; continue; }
                if (space) { result.Append(' '); space = false; }
                if (c == 'ß') result.Append("ss");
                else result.Append(char.ToLowerInvariant(c));
            }
            return result.ToString();
        }
    }
}
