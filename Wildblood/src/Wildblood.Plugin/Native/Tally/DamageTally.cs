// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Tally
{
    /// <summary>
    /// A boss's damage tally: how much health each player has taken off it, the boss's own and its Phantom copies'
    /// together. It lives in the boss's ZDO, written by the boss's owner, so an owner hand-over mid-fight keeps the count.
    /// One entry per player, keyed by player ID, carrying the name as it was at the player's last hit. Every hit, burn and
    /// poison tick is credited, so the owner keeps the tally open in memory and writes it to the ZDO at most once every
    /// <see cref="FlushInterval"/> (each write resends the boss's ZDO to everyone near it), and at once whenever it is
    /// read here: at the death, at a Tethered hand-over, for the Fixated mark. A hand-over of the boss itself loses at
    /// most the last second's credit, never counts it twice.
    /// </summary>
    internal static class DamageTally
    {
        private const float FlushInterval = 1f;

        private static readonly int BossDamageHash = TraitKeys.BossDamage.GetStableHashCode();

        public sealed class Entry
        {
            public long Id;
            public string Name = "";
            public float Damage;
        }

        // A tally being written to: the ZDO it belongs to (the game reuses ZDO objects, so the id is checked too).
        private sealed class Open
        {
            public ZDO Zdo = null!;
            public List<Entry> Entries = null!;
        }

        private static readonly Dictionary<ZDOID, Open> Opened = new Dictionary<ZDOID, Open>();
        private static readonly List<ZDOID> Closing = new List<ZDOID>();
        private static float _nextFlush;

        internal static void Reset() { Opened.Clear(); Closing.Clear(); _nextFlush = 0f; }

        /// <summary>The tally as it stands, the hits not yet written included.</summary>
        public static List<Entry> Load(ZDO zdo)
        {
            Flush(zdo.m_uid);
            List<Entry> entries = new List<Entry>();
            byte[]? bytes = zdo.GetByteArray(BossDamageHash);
            if (bytes != null && bytes.Length > 0)
            {
                Read(new ZPackage(bytes), entries);
            }
            return entries;
        }

        /// <summary>Owner side: credits one hit's health loss to the player who dealt it.</summary>
        public static void Add(ZDO zdo, long playerId, string playerName, float amount)
        {
            if (!Opened.TryGetValue(zdo.m_uid, out Open open) || !ReferenceEquals(open.Zdo, zdo))
            {
                open = new Open { Zdo = zdo, Entries = Load(zdo) };
                Opened[zdo.m_uid] = open;
            }
            Merge(open.Entries, playerId, playerName, amount);
        }

        /// <summary>Owner side: empties the tally, once it has been handed on to a Tethered partner.</summary>
        public static void Clear(ZDO zdo)
        {
            Opened.Remove(zdo.m_uid);
            zdo.Set(BossDamageHash, new byte[0]);
        }

        /// <summary>From the plugin's frame loop: writes every open tally once its interval is up. Nothing while none is open.</summary>
        public static void FlushDue(float now)
        {
            if (Opened.Count == 0 || now < _nextFlush)
            {
                return;
            }
            _nextFlush = now + FlushInterval;
            Closing.Clear();
            Closing.AddRange(Opened.Keys);
            foreach (ZDOID id in Closing)
            {
                Flush(id);
            }
        }

        // Written only while this machine still owns that very ZDO; after a hand-over the open credit is dropped.
        private static void Flush(ZDOID id)
        {
            if (!Opened.TryGetValue(id, out Open open))
            {
                return;
            }
            Opened.Remove(id);
            ZDO? current = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(id) : null;
            if (ReferenceEquals(current, open.Zdo) && open.Zdo.m_uid == id && open.Zdo.IsOwner())
            {
                ZPackage pkg = new ZPackage();
                Write(pkg, open.Entries);
                open.Zdo.Set(BossDamageHash, pkg.GetArray());
            }
        }

        public static void Merge(List<Entry> entries, long id, string name, float amount)
        {
            if (id == 0 || name == null || name.Length > 256 || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return;
            foreach (Entry entry in entries)
            {
                if (entry.Id == id)
                {
                    entry.Name = name;
                    if (!float.IsInfinity(entry.Damage + amount)) entry.Damage += amount;
                    return;
                }
            }
            if (entries.Count < 128) entries.Add(new Entry { Id = id, Name = name, Damage = amount });
        }

        public static void Write(ZPackage pkg, List<Entry> entries)
        {
            pkg.Write(entries.Count);
            foreach (Entry entry in entries)
            {
                pkg.Write(entry.Id);
                pkg.Write(entry.Name);
                pkg.Write(entry.Damage);
            }
        }

        public static void Read(ZPackage pkg, List<Entry> into)
        {
            if (pkg.Size() > 65536) throw new InvalidOperationException("Boss tally exceeds its bound.");
            int count = pkg.ReadInt();
            if (count < 0 || count > 128) throw new InvalidOperationException("Boss tally row count exceeds its bound.");
            var decoded = new List<Entry>();
            for (int i = 0; i < count; i++)
            {
                long id = pkg.ReadLong(); string name = pkg.ReadString(); float damage = pkg.ReadSingle();
                if (id == 0 || name.Length > 256 || float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
                    throw new InvalidOperationException("Invalid boss tally row.");
                decoded.Add(new Entry { Id = id, Name = name, Damage = damage });
            }
            into.AddRange(decoded);
        }
    }
}
