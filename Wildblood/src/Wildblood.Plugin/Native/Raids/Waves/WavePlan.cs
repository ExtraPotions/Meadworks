// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The current wave's raiders still to arrive, kept in the host's ZDO (<see cref="RaidKeys.WavePlan"/>) so a machine
    /// that takes the host over sends the rest of the wave exactly as planned: how many of each creature (by its place in
    /// the event's spawn list) are still to come, whether the Warlord is, the regular raiders the later waves are expected
    /// to bring (for the coin split, <see cref="RaidPurse"/>) and whose turn it is between chest raider and plunderer.
    /// Written by the host's owner as the wave begins (<see cref="WaveSizes"/>) and as raiders leave it. Read every tick,
    /// so the last decoded plan is kept against the very array it came from: the ZDO hands back the same array until the
    /// plan is written again, here or by the machine that owned the host before.
    /// </summary>
    internal sealed class WavePlan
    {
        private const int Format = 1;
        private static readonly int Key = RaidKeys.WavePlan.GetStableHashCode();

        private static byte[]? _cachedFrom;
        private static WavePlan? _cached;

        private readonly List<int> _places = new List<int>();
        private readonly List<int> _counts = new List<int>();

        /// <summary>The place in the event's list of the Warlord's kind while the Warlord is still to come; -1 otherwise.</summary>
        public int WarlordPlace { get; private set; } = -1;

        /// <summary>The regular raiders (not the Warlord) the waves after this one are expected to bring.</summary>
        public int Later { get; set; }

        /// <summary>The role the next regular raider takes, a <see cref="RaiderRole"/> as an int.</summary>
        public int NextRole { get; set; } = (int)RaiderRole.ChestRaider;

        public bool WarlordDue => WarlordPlace >= 0;

        /// <summary>The regular raiders of this wave still to come.</summary>
        public int Regulars { get; private set; }

        /// <summary>Every raider of this wave still to come, the Warlord included.</summary>
        public int ToCome => Regulars + (WarlordDue ? 1 : 0);

        /// <summary>Plans <paramref name="count"/> of the creature at <paramref name="place"/> in the event's list.</summary>
        public void Add(int place, int count)
        {
            if (count > 0)
            {
                _places.Add(place);
                _counts.Add(count);
                Regulars += count;
            }
        }

        /// <summary>One of the creature at <paramref name="place"/> is the Warlord: it comes in place of a regular one.</summary>
        public void MakeWarlord(int place)
        {
            int at = _places.IndexOf(place);
            if (at >= 0 && _counts[at] > 0)
            {
                _counts[at]--;
                Regulars--;
            }
            WarlordPlace = place;
        }

        /// <summary>The Warlord leaves the plan; its kind's place in the event's list.</summary>
        public int TakeWarlord()
        {
            int place = WarlordPlace;
            WarlordPlace = -1;
            return place;
        }

        /// <summary>A regular raider leaves the plan, its kind drawn by how many of each are still to come, so the kinds
        /// arrive mixed; its place in the event's list, or -1 when none is left.</summary>
        public int TakeRegular()
        {
            if (Regulars <= 0)
            {
                return -1;
            }
            int roll = UnityEngine.Random.Range(0, Regulars);
            for (int i = 0; i < _counts.Count; i++)
            {
                if (roll < _counts[i])
                {
                    _counts[i]--;
                    Regulars--;
                    return _places[i];
                }
                roll -= _counts[i];
            }
            return -1;
        }

        /// <summary>The next regular raider's role, turning it over for the one after: chest raider, plunderer, chest raider...</summary>
        public RaiderRole TakeRole()
        {
            RaiderRole role = NextRole == (int)RaiderRole.Plunderer ? RaiderRole.Plunderer : RaiderRole.ChestRaider;
            NextRole = (int)(role == RaiderRole.ChestRaider ? RaiderRole.Plunderer : RaiderRole.ChestRaider);
            return role;
        }

        /// <summary>Nothing more comes this wave (its event is gone from the game).</summary>
        public void Clear()
        {
            _places.Clear();
            _counts.Clear();
            Regulars = 0;
            WarlordPlace = -1;
        }

        /// <summary>The plan in the host's ZDO; an empty one when it holds none or one this version cannot read.</summary>
        public static WavePlan Read(ZDO host)
        {
            byte[]? bytes = host.GetByteArray(Key);
            if (bytes != null && ReferenceEquals(bytes, _cachedFrom) && _cached != null)
            {
                return _cached;
            }
            WavePlan plan = bytes != null ? Decode(bytes) : new WavePlan();
            _cachedFrom = bytes;
            _cached = plan;
            return plan;
        }

        /// <summary>Writes the plan into the host's ZDO. Host's owner only.</summary>
        public void Write(ZDO host)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(Format);
            pkg.Write(WarlordPlace);
            pkg.Write(Later);
            pkg.Write(NextRole);
            pkg.Write(_places.Count);
            for (int i = 0; i < _places.Count; i++)
            {
                pkg.Write(_places[i]);
                pkg.Write(_counts[i]);
            }
            byte[] bytes = pkg.GetArray();
            host.Set(Key, bytes);
            _cachedFrom = bytes;
            _cached = this;
        }

        private static WavePlan Decode(byte[] bytes)
        {
            WavePlan plan = new WavePlan();
            try
            {
                ZPackage pkg = new ZPackage(bytes);
                if (pkg.ReadInt() != Format)
                {
                    return plan;
                }
                plan.WarlordPlace = pkg.ReadInt();
                plan.Later = pkg.ReadInt();
                plan.NextRole = pkg.ReadInt();
                ReadKinds(pkg, plan);
            }
            catch (Exception)
            {
                plan.Clear(); // a damaged plan sends nothing more this wave rather than failing every tick
            }
            return plan;
        }

        private static void ReadKinds(ZPackage pkg, WavePlan plan)
        {
            int kinds = pkg.ReadInt();
            for (int i = 0; i < kinds; i++)
            {
                int place = pkg.ReadInt();
                plan.Add(place, Mathf.Max(0, pkg.ReadInt()));
            }
        }
    }
}
