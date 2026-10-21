// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using System.Text;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Affixes
{
    /// <summary>
    /// One filled socket (sockets.md section 2): the gem's id and the inscription it gave this item, rolled when it was
    /// socketed (grade and value stored like an inscription's, so the stored roll is what applies). Immutable.
    /// </summary>
    public readonly struct GemRoll
    {
        public GemRoll(string gemId, AffixRoll roll)
        {
            GemId = gemId;
            Roll = roll;
        }

        public string GemId { get; }

        /// <summary>The inscription the gem gives on this item, with its grade and value.</summary>
        public AffixRoll Roll { get; }

        public override string ToString() => GemCodec.Encode(this);
    }

    /// <summary>One position of the gem list: a parsed gem, or an unreadable entry kept verbatim.</summary>
    internal readonly struct GemSegment
    {
        public GemSegment(GemRoll gem)
        {
            Gem = gem;
            Raw = null;
        }

        public GemSegment(string raw)
        {
            Gem = default;
            Raw = raw;
        }

        public GemRoll Gem { get; }

        /// <summary>Non-null for an unreadable entry, written back unchanged in its socket.</summary>
        public string? Raw { get; }

        public bool IsGem => Raw == null;
    }

    /// <summary>Reads and writes <c>ecf_sockets</c> and <c>ecf_gems</c>; culture-invariant, a bad entry kept verbatim.</summary>
    internal static class GemCodec
    {
        /// <summary>The most sockets an item holds; a stored count above it reads as this.</summary>
        public const int MaxSockets = 3;

        public static int ParseSockets(string? text) =>
            text != null && Numbers.TryInt(text, out int count) ? Math.Max(0, Math.Min(count, MaxSockets)) : 0;

        public static GemSegment[] ParseGems(string? text)
        {
            if (text == null)
            {
                return Array.Empty<GemSegment>();
            }
            string[] parts = text.Split(ItemKeys.EntrySeparator);
            GemSegment[] gems = new GemSegment[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                gems[i] = TryParse(parts[i], out GemRoll gem) ? new GemSegment(gem) : new GemSegment(parts[i]);
            }
            return gems;
        }

        public static string Encode(GemRoll gem) => gem.GemId + ItemKeys.FieldSeparator + ItemCodec.Encode(gem.Roll);

        public static string? EncodeGems(GemSegment[] gems)
        {
            if (gems.Length == 0)
            {
                return null;
            }
            StringBuilder sb = new StringBuilder(gems.Length * 32);
            for (int i = 0; i < gems.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(ItemKeys.EntrySeparator);
                }
                sb.Append(gems[i].IsGem ? Encode(gems[i].Gem) : gems[i].Raw);
            }
            return sb.ToString();
        }

        public static string? EncodeSockets(int count) => count > 0 ? Numbers.Format(count) : null;

        private static bool TryParse(string text, out GemRoll gem)
        {
            gem = default;
            int split = text.IndexOf(ItemKeys.FieldSeparator);
            if (split <= 0 || !Ids.IsValid(text.Substring(0, split)))
            {
                return false;
            }
            if (!ItemCodec.TryParseRoll(text.Substring(split + 1), out AffixRoll roll))
            {
                return false;
            }
            gem = new GemRoll(text.Substring(0, split), roll);
            return true;
        }

        /// <summary>The parsed gems, unreadable entries left out; <paramref name="sockets"/> holds the socket each sits in.</summary>
        public static GemRoll[] Readable(GemSegment[] gems, out int[] sockets)
        {
            List<GemRoll> list = new List<GemRoll>(gems.Length);
            List<int> at = new List<int>(gems.Length);
            for (int i = 0; i < gems.Length; i++)
            {
                if (gems[i].IsGem)
                {
                    list.Add(gems[i].Gem);
                    at.Add(i);
                }
            }
            sockets = at.Count == 0 ? Array.Empty<int>() : at.ToArray();
            return list.Count == 0 ? Array.Empty<GemRoll>() : list.ToArray();
        }
    }
}
