// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The raiders of a raid alive now (features/raids.md section 5: the owner counts the living raiders), by their ZDOIDs
    /// kept in the host's ZDO, so whichever machine runs the raid counts the same raiders, whoever owns each one, without
    /// ever searching the world: at most <see cref="RaidTable.MaxAlive"/> lookups a tick. A raider is alive while its ZDO
    /// exists, since a creature's ZDO is destroyed as it dies and the destruction reaches every machine. A machine never
    /// drops a ZDO it was sent until it is destroyed, so a raider this machine has seen and no longer knows is dead. One it
    /// has never seen - just after taking the raid over, before the server has sent it everything around - counts as alive
    /// for a short grace and then as gone (one far beyond every player that this machine is never sent). A raider the
    /// players have tamed is theirs now and leaves the list, so it never holds a wave open.
    /// <para>
    /// The list is read every tick, so the last decoded list is kept against the very array it came from: the ZDO hands
    /// back the same array until the list is written again. Written only when it changes: a raider added or one gone.
    /// </para>
    /// </summary>
    internal static class RaidRoster
    {
        /// <summary>The waves' own key on the host's ZDO: the ZDOIDs of the raiders alive at the last count.</summary>
        public const string RosterKey = "ecr_raid_roster";

        private const float GraceSeconds = 15f;
        private static readonly int Key = RosterKey.GetStableHashCode();

        private static readonly List<ZDOID> Ids = new List<ZDOID>();
        private static readonly HashSet<ZDOID> Seen = new HashSet<ZDOID>();
        private static readonly Dictionary<ZDOID, float> UnseenSince = new Dictionary<ZDOID, float>();
        private static byte[]? _readFrom;
        private static ZDO? _readHost;
        private static ZDOMan? _session;

        /// <summary>The raiders of raid <paramref name="raid"/> alive now; drops the gone from the host's list.</summary>
        public static int Count(ZDO host, long raid, float now)
        {
            Load(host);
            int before = Ids.Count;
            for (int i = Ids.Count - 1; i >= 0; i--)
            {
                if (!Alive(Ids[i], raid, now))
                {
                    Forget(i);
                }
            }
            if (Ids.Count != before)
            {
                Save(host);
            }
            return Ids.Count;
        }

        /// <summary>A raider just spawned here joins the list; <see cref="Save"/> writes it.</summary>
        public static void Add(ZDO host, ZDOID raider)
        {
            Load(host);
            Ids.Add(raider);
            Seen.Add(raider);
        }

        /// <summary>A new raid starts at the host with nobody on its list.</summary>
        public static void Clear(ZDO host)
        {
            Load(host);
            for (int i = Ids.Count - 1; i >= 0; i--)
            {
                Forget(i);
            }
            Save(host);
        }

        /// <summary>Writes the list into the host's ZDO. Host's owner only.</summary>
        public static void Save(ZDO host)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(Ids.Count);
            foreach (ZDOID id in Ids)
            {
                pkg.Write(id);
            }
            byte[] bytes = pkg.GetArray();
            host.Set(Key, bytes);
            _readFrom = bytes;
            _readHost = host;
        }

        private static bool Alive(ZDOID id, long raid, float now)
        {
            ZDO? zdo = ZDOMan.instance.GetZDO(id);
            if (zdo != null)
            {
                Seen.Add(id);
                UnseenSince.Remove(id);
                return RaiderTag.Raid(zdo) == raid && !zdo.GetBool(ZDOVars.s_tamed);
            }
            if (Seen.Contains(id))
            {
                return false; // known here once, gone now: destroyed, so dead
            }
            if (!UnseenSince.TryGetValue(id, out float since))
            {
                UnseenSince[id] = since = now;
            }
            return now - since < GraceSeconds;
        }

        // Swap-remove: the order means nothing.
        private static void Forget(int at)
        {
            Seen.Remove(Ids[at]);
            UnseenSince.Remove(Ids[at]);
            Ids[at] = Ids[Ids.Count - 1];
            Ids.RemoveAt(Ids.Count - 1);
        }

        private static void Load(ZDO host)
        {
            if (!ReferenceEquals(_session, ZDOMan.instance))
            {
                _session = ZDOMan.instance; // a new world or server: what was seen in the last one means nothing here
                Seen.Clear();
                UnseenSince.Clear();
                _readFrom = null;
            }
            byte[]? bytes = host.GetByteArray(Key);
            if (bytes != null && ReferenceEquals(bytes, _readFrom) && ReferenceEquals(host, _readHost))
            {
                return;
            }
            Decode(bytes);
            _readFrom = bytes;
            _readHost = host;
        }

        private static void Decode(byte[]? bytes)
        {
            Ids.Clear();
            if (bytes == null)
            {
                return;
            }
            try
            {
                ZPackage pkg = new ZPackage(bytes);
                for (int n = pkg.ReadInt(); n > 0; n--)
                {
                    Ids.Add(pkg.ReadZDOID());
                }
            }
            catch (Exception)
            {
                Ids.Clear(); // a damaged list counts nobody rather than failing every tick
            }
        }
    }
}
