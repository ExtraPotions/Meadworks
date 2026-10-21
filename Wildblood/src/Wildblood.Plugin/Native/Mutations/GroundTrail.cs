// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The wire form of a ground trail (Frostbound's ice, Mudbound's mud), shaped like Miasmic's: the owner keeps the
    /// creature's most recent patch drops in its ZDO as one small packed blob under the kind's own key, the game
    /// replicates it, and every client reads it back to lay the same patches. Each drop carries a monotonic id, so a
    /// client lays each patch exactly once, and a shared-clock timestamp, so every machine retires it at the same
    /// instant. The blob holds only the newest few drops: a client lays a patch the moment it sees it and keeps it
    /// itself, so the blob need only outlast the gap between two updates. This is only the codec; the trail component
    /// owns the list.
    /// </summary>
    internal static class GroundTrail
    {
        /// <summary>The most drops one blob may claim to hold; anything larger is not a blob this mod wrote.</summary>
        private const int MaxCount = 64;

        public struct Drop
        {
            public long Id;
            public Vector3 Pos;
            public long TimeMs;
        }

        /// <summary>
        /// Reads the trail from a creature's ZDO into <paramref name="into"/>, which is cleared first.
        /// </summary>
        public static void Read(ZDO zdo, string key, List<Drop> into)
        {
            into.Clear();
            byte[]? bytes = zdo != null ? zdo.GetByteArray(key) : null;
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }
            ZPackage pkg = new ZPackage(bytes);
            int count = Mathf.Min(pkg.ReadInt(), MaxCount);
            for (int i = 0; i < count; i++)
            {
                Drop drop = default;
                drop.Id = pkg.ReadLong();
                drop.Pos = new Vector3(pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle());
                drop.TimeMs = pkg.ReadLong();
                into.Add(drop);
            }
        }

        /// <summary>
        /// Owner-only write of the whole trail as one blob; a non-owner write would be dropped by the game.
        /// </summary>
        public static void Write(ZDO zdo, string key, List<Drop> drops)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(drops.Count);
            foreach (Drop drop in drops)
            {
                pkg.Write(drop.Id);
                pkg.Write(drop.Pos.x); pkg.Write(drop.Pos.y); pkg.Write(drop.Pos.z);
                pkg.Write(drop.TimeMs);
            }
            zdo.Set(key, pkg.GetArray());
        }

        /// <summary>Expired drops fall off, then the oldest until at most <paramref name="keep"/> remain.</summary>
        public static void Trim(List<Drop> drops, float life, int keep)
        {
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                if (NetTime.SecondsSince(drops[i].TimeMs) >= life)
                {
                    drops.RemoveAt(i);
                }
            }
            if (drops.Count > keep)
            {
                drops.RemoveRange(0, drops.Count - keep);
            }
        }

        /// <summary>
        /// Next drop id, from a monotonic ZDO counter - never a max-of-list, which would reuse an id after the trail
        /// trims its highest drop and make a client skip the reissued patch. The counter also tells a client the trail
        /// changed.
        /// </summary>
        public static long NextId(ZDO zdo, string seqKey)
        {
            long next = zdo.GetLong(seqKey, 1L);
            zdo.Set(seqKey, next + 1L);
            return next;
        }

        /// <summary>
        /// The counter's current value, by its key's hash: it moves exactly when the owner writes a new drop.
        /// </summary>
        public static long Seq(ZDO zdo, int seqHash) => zdo.GetLong(seqHash, 0L);
    }
}
