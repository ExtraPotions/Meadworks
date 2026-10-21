// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Affixes
{
    /// <summary>
    /// Reads an item's <c>ecf_</c> keys into <see cref="StateData"/> (item-data.md sections 3, 4 and 8) and encodes
    /// affix entries. Culture-invariant numbers; an unparseable or duplicate segment is kept verbatim in its position;
    /// a key whose meaning is unknown is left alone. Reading never writes.
    /// </summary>
    internal static class ItemCodec
    {
        // Clone copies the dictionary but not its value strings, so a clone's affix string is the same instance as its
        // original's: keyed by string identity, a clone reuses the parsed list without touching the text.
        private static readonly ConditionalWeakTable<string, ItemSegment[]> ListCache =
            new ConditionalWeakTable<string, ItemSegment[]>();

        public static StateData Parse(Dictionary<string, string> data)
        {
            if (data.Count == 0 || !HasState(data, out bool hasVersion))
            {
                return StateData.Empty;
            }
            int format = ReadFormat(data, hasVersion);
            StateData state = new StateData
            {
                Format = format,
                Newer = format > ItemKeys.CurrentFormat,
                RarityId = NonEmpty(data, ItemKeys.Rarity),
                Segments = Segments(NonEmpty(data, ItemKeys.Affixes) ?? NonEmpty(data, ItemKeys.LegacyAffixes)),
                SealedReason = NonEmpty(data, ItemKeys.Sealed),
                ReservedTier = NonEmpty(data, ItemKeys.Tier),
                Sockets = GemCodec.ParseSockets(NonEmpty(data, ItemKeys.Sockets)),
                Gems = GemCodec.ParseGems(NonEmpty(data, ItemKeys.Gems)),
            };
            return ItemMigrations.Upgrade(state);
        }

        public static string Encode(AffixRoll roll) =>
            roll.Id + ItemKeys.FieldSeparator + Numbers.Format(roll.Tier) + ItemKeys.FieldSeparator + Numbers.Format(roll.Value);

        public static string? EncodeList(ItemSegment[] segments)
        {
            if (segments.Length == 0)
            {
                return null;
            }
            StringBuilder sb = new StringBuilder(segments.Length * 20);
            for (int i = 0; i < segments.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(ItemKeys.EntrySeparator);
                }
                sb.Append(segments[i].IsRoll ? Encode(segments[i].Roll) : segments[i].Raw);
            }
            return sb.ToString();
        }

        private static bool HasState(Dictionary<string, string> data, out bool hasVersion)
        {
            hasVersion = data.ContainsKey(ItemKeys.Version);
            if (hasVersion)
            {
                return true;
            }
            foreach (string key in ItemKeys.StateKeys)
            {
                if (data.ContainsKey(key))
                {
                    return true;
                }
            }
            return false;
        }

        // ecf_v absent while other keys exist reads as version 1 (defensive; the mod always writes it).
        private static int ReadFormat(Dictionary<string, string> data, bool hasVersion)
        {
            if (!hasVersion)
            {
                return 1;
            }
            return Numbers.TryInt(data[ItemKeys.Version], out int v) && v >= 1 ? v : ItemKeys.CurrentFormat + 1;
        }

        private static string? NonEmpty(Dictionary<string, string> data, string key) =>
            data.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value) ? value : null;

        private static ItemSegment[] Segments(string? text)
        {
            if (text == null)
            {
                return System.Array.Empty<ItemSegment>();
            }
            if (!ListCache.TryGetValue(text, out ItemSegment[] segments))
            {
                segments = ParseList(text);
                ListCache.Add(text, segments);
            }
            return segments;
        }

        private static ItemSegment[] ParseList(string text)
        {
            string[] parts = text.Split(ItemKeys.EntrySeparator);
            ItemSegment[] segments = new ItemSegment[parts.Length];
            HashSet<string> seen = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < parts.Length; i++)
            {
                bool ok = TryParseRoll(parts[i], out AffixRoll roll) && seen.Add(roll.Id);
                segments[i] = ok ? new ItemSegment(roll) : new ItemSegment(parts[i]);
            }
            return segments;
        }

        internal static bool TryParseRoll(string text, out AffixRoll roll)
        {
            roll = default;
            string[] fields = text.Split(ItemKeys.FieldSeparator);
            if (fields.Length != 3 || !Ids.IsValid(fields[0]))
            {
                return false;
            }
            if (!Numbers.TryInt(fields[1], out int tier) || tier < 1 || !Numbers.TryFloat(fields[2], out float value))
            {
                return false;
            }
            roll = new AffixRoll(fields[0], tier, value);
            return true;
        }
    }
}
