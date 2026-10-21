// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The wire form of a Miasmic creature's recent cloud drops. The owner keeps the authoritative list and writes it
    /// into the creature's ZDO as one small packed blob; the game replicates it, and every client reads it back to draw
    /// the same trail. Each drop carries a monotonic id (so a client spawns each cloud exactly once) and a shared-clock
    /// timestamp (so every machine retires it at the same instant). This is only the codec; the field component owns
    /// the list.
    /// </summary>
    public static class MiasmaTrail
    {
        public struct Drop
        {
            public long Id;
            public Vector3 Pos;
            public long TimeMs;
        }

        private static readonly int TrailHash = TraitKeys.MiasmaTrail.GetStableHashCode();
        private static readonly int SeqHash = TraitKeys.MiasmaSeq.GetStableHashCode();

        /// <summary>
        /// Reads the trail from a creature's ZDO into <paramref name="into"/>, which is cleared first. Works on any machine
        /// - the ZDO is shared. Empty when unset.
        /// </summary>
        public static void Read(ZDO zdo, List<Drop> into)
        {
            into.Clear();
            byte[]? bytes = zdo != null ? zdo.GetByteArray(TrailHash) : null;
            if (bytes != null && bytes.Length > 0)
            {
                Decode(new ZPackage(bytes), into);
            }
        }

        /// <summary>The drop counter's current value: it moves exactly when the owner writes a new drop.</summary>
        public static long Seq(ZDO zdo) => zdo.GetLong(SeqHash, 0L);

        private static void Decode(ZPackage pkg, List<Drop> drops)
        {
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                Drop drop = default;
                drop.Id = pkg.ReadLong();
                drop.Pos = new Vector3(pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle());
                drop.TimeMs = pkg.ReadLong();
                drops.Add(drop);
            }
        }

        /// <summary>Owner-only write of the whole trail as one blob; a non-owner write would be dropped by the game.</summary>
        public static void Write(ZDO zdo, List<Drop> drops)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(drops.Count);
            foreach (Drop drop in drops)
            {
                pkg.Write(drop.Id);
                pkg.Write(drop.Pos.x); pkg.Write(drop.Pos.y); pkg.Write(drop.Pos.z);
                pkg.Write(drop.TimeMs);
            }
            zdo.Set(TraitKeys.MiasmaTrail, pkg.GetArray());
        }

        /// <summary>Drops still within their lifetime survive a trim; expired ones fall off so the blob stays small.</summary>
        public static void TrimExpired(List<Drop> drops, float life)
        {
            drops.RemoveAll(drop => NetTime.SecondsSince(drop.TimeMs) >= life);
        }

        /// <summary>
        /// Next drop id, from a monotonic ZDO counter - never a max-of-list, which would reuse an id after the trail
        /// trims its highest drop and make a client skip (leaving a cloud that still hurts) the reissued cloud.
        /// </summary>
        public static long NextId(ZDO zdo)
        {
            long next = zdo.GetLong(SeqHash, 1L);
            zdo.Set(SeqHash, next + 1L);
            return next;
        }
    }
}
