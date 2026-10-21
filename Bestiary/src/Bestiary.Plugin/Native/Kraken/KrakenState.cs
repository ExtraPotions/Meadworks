// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>What the kraken is doing, as every machine reads it from its ZDO.</summary>
    public enum KrakenPhase
    {
        Lurk = 0,       // swimming under the surface, looking for a ship
        Hunt = 1,       // chasing a ship at the surface
        Dive = 2,       // caught up: diving under the ship, which is held from now on
        Tentacles = 3,  // under the ship; six tentacles up round it, slamming the deck one at a time
        Head = 4,       // the head up beside the ship: bites, ink, and a tentacle smashing the ship now and then
        Leave = 5,      // giving up: diving away, gone for good in a few seconds
    }

    /// <summary>
    /// The kraken's shared state in its ZDO: its phase, the ship it hunts or holds, where it holds that ship, and which
    /// side of the ship and how far along it the head comes up. Only the owner writes it; every machine draws from it.
    /// </summary>
    public class KrakenState
    {
        private static readonly int PhaseKey = "ecp_kraken_phase".GetStableHashCode();
        private static readonly KeyValuePair<int, int> ShipKey = ZDO.GetHashZDOID("ecp_kraken_ship");
        private static readonly int AnchorKey = "ecp_kraken_anchor".GetStableHashCode();
        private static readonly int SideKey = "ecp_kraken_side".GetStableHashCode();
        private static readonly int AlongKey = "ecp_kraken_along".GetStableHashCode();

        private readonly ZNetView _nview;

        public KrakenState(ZNetView nview) => _nview = nview;

        private ZDO? Zdo => _nview != null && _nview.IsValid() ? _nview.GetZDO() : null;

        public bool IsOwner => Zdo != null && _nview.IsOwner();

        public KrakenPhase Phase => (KrakenPhase)(Zdo?.GetInt(PhaseKey) ?? 0);

        /// <summary>The ship it hunts (Hunt) or holds (Dive, Tentacles, Head).</summary>
        public ZDOID Ship => Zdo?.GetZDOID(ShipKey) ?? ZDOID.None;

        /// <summary>Where the held ship is kept: its position when the kraken caught it.</summary>
        public Vector3 Anchor => Zdo?.GetVec3(AnchorKey, Vector3.zero) ?? Vector3.zero;

        /// <summary>The side of the ship the head comes up on: +1 its right (starboard), -1 its left.</summary>
        public int Side => Zdo?.GetInt(SideKey, 1) >= 0 ? 1 : -1;

        /// <summary>How far along the ship (its local z, metres) the head comes up.</summary>
        public float Along => Zdo?.GetFloat(AlongKey) ?? 0f;

        /// <summary>Whether it holds a ship now.</summary>
        public bool Grips => IsGrip(Phase);

        public static bool IsGrip(KrakenPhase phase) =>
            phase == KrakenPhase.Dive || phase == KrakenPhase.Tentacles || phase == KrakenPhase.Head;

        /// <summary>Whether the fight is on: the boss bar shows.</summary>
        public bool Fighting => Phase != KrakenPhase.Lurk && Phase != KrakenPhase.Leave;

        /// <summary>OWNER.</summary>
        public void SetPhase(KrakenPhase phase) => Zdo?.Set(PhaseKey, (int)phase);

        /// <summary>OWNER.</summary>
        public void SetShip(ZDOID ship) => Zdo?.Set(ShipKey, ship);

        /// <summary>OWNER.</summary>
        public void SetAnchor(Vector3 anchor) => Zdo?.Set(AnchorKey, anchor);

        /// <summary>OWNER.</summary>
        public void SetHead(int side, float along)
        {
            Zdo?.Set(SideKey, side >= 0 ? 1 : -1);
            Zdo?.Set(AlongKey, along);
        }
    }
}
