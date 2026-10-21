// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: named hubs (design B1 to B5, user decision Q1: several named hubs per world). PortalSelector's
// "main portal" is the behaviour reference for one hub per world (setting a new hub moves it).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Portals;

namespace ExtraPotions.Threshold.Access
{
    /// <summary>Why a hub change is refused (C03 reasons for the picker).</summary>
    public enum HubRefusal { None, NotCreatorOrAdmin, NotAdmin, LimitReached, NameTaken, EmptyName, TaggedPortal }

    /// <summary>The player asking the server to change a hub: player id and whether the server counts them as an admin (the host does).</summary>
    public readonly struct HubEditor
    {
        public HubEditor(long playerId, bool isAdminOrHost)
        {
            PlayerId = playerId;
            IsAdminOrHost = isAdminOrHost;
        }

        public long PlayerId { get; }
        public bool IsAdminOrHost { get; }
    }

    /// <summary>A hub by its portal's position key (<see cref="PortalKey"/>) and name.</summary>
    public readonly struct HubSlot
    {
        public HubSlot(string key, string name)
        {
            Key = key ?? "";
            Name = HubRules.NormalizeName(name);
        }

        public string Key { get; }
        public string Name { get; }
    }

    /// <summary>What a Set Hub request does: refused with a reason, or allowed and clearing these hub keys in the same step.</summary>
    public sealed class HubPlan
    {
        internal HubPlan(HubRefusal refusal, IReadOnlyList<string> clear)
        {
            Refusal = refusal;
            Clear = clear;
        }

        public HubRefusal Refusal { get; }
        public IReadOnlyList<string> Clear { get; }
    }

    public static class HubRules
    {
        public const int MaxNameLength = 32;

        /// <summary>Checked on the server with the sender's identity (design section 3). The creator is the piece creator id
        /// (0 when unknown, which matches nobody).</summary>
        public static HubRefusal MayEdit(HubEditing mode, long creator, HubEditor editor)
        {
            switch (mode)
            {
                case HubEditing.Anyone:
                    return HubRefusal.None;
                case HubEditing.CreatorOrAdmin:
                    return editor.IsAdminOrHost || (creator != 0L && creator == editor.PlayerId) ? HubRefusal.None : HubRefusal.NotCreatorOrAdmin;
                default:
                    return editor.IsAdminOrHost ? HubRefusal.None : HubRefusal.NotAdmin;
            }
        }

        /// <summary>Trimmed, control characters as spaces, at most <see cref="MaxNameLength"/> characters.</summary>
        public static string NormalizeName(string? name)
        {
            if (name == null) return "";
            var text = new StringBuilder(name.Length);
            foreach (var c in name) text.Append(char.IsControl(c) ? ' ' : c);
            var trimmed = text.ToString().Trim();
            return trimmed.Length > MaxNameLength ? trimmed.Substring(0, MaxNameLength).TrimEnd() : trimmed;
        }

        /// <summary>
        /// Plan for making <paramref name="target"/> a hub (or renaming it) given the world's current hubs. With one hub per
        /// world the previous hub is cleared in the same step (PortalSelector parity); with several, the limit refuses a new
        /// one. Names are unique regardless of case. A hub is an untagged portal.
        /// </summary>
        public static HubPlan PlanSet(IReadOnlyList<HubSlot> current, HubSlot target, int maxHubs, bool targetTagged)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            var none = Array.Empty<string>();
            if (targetTagged) return new HubPlan(HubRefusal.TaggedPortal, none);
            if (target.Name.Length == 0) return new HubPlan(HubRefusal.EmptyName, none);
            var others = current.Where(h => h.Key != target.Key).ToList();
            if (others.Any(h => string.Equals(h.Name, target.Name, StringComparison.OrdinalIgnoreCase)))
                return new HubPlan(HubRefusal.NameTaken, none);
            bool rename = others.Count != current.Count;
            if (rename) return new HubPlan(HubRefusal.None, none);
            int limit = ThresholdSettings.Hubs.MaxHubs.Normalize(maxHubs);
            if (limit == 1) return new HubPlan(HubRefusal.None, others.Select(h => h.Key).ToList());
            return others.Count >= limit ? new HubPlan(HubRefusal.LimitReached, none) : new HubPlan(HubRefusal.None, none);
        }

        /// <summary>The hub nearest on the map plane; a tie goes to the name first in ordinal order. Null without hubs.</summary>
        public static PortalEntry? NearestHub(MapPoint from, IEnumerable<PortalEntry> portals) =>
            (portals ?? Enumerable.Empty<PortalEntry>()).Where(p => p.IsHub)
                .OrderBy(p => p.Position.FlatDistance(from)).ThenBy(p => p.HubName, StringComparer.Ordinal).FirstOrDefault();
    }
}
