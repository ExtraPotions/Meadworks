// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGateFields.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>Where a jumping ship is. Kept in the ship's ZDO so whoever owns the ship carries the jump on.</summary>
    public enum JumpState
    {
        None = 0,
        Frozen = 1,  // stopped at the source gate; the crew measures its deck spots against it
        Moved = 2,   // placed at the destination, still frozen, waiting for the destination to load
        Settled = 3, // checked clear at the destination; the crew may land
        Choosing = 4 // stopped in the source gate while the helmsman picks the destination on the map
    }

    /// <summary>Every sea gate key, RPC name and id rule, so the modules never disagree about a name. Pillars,
    /// pairs and gates use ids of our own (<see cref="IdKey"/>): the game renumbers every ZDOID on each
    /// world load (<c>ZDO.Load</c>: <c>m_uid.SetID(++ZDOID.m_loadID)</c>), so a stored ZDOID would point at
    /// something else after a restart. The anchor of a pair is the pillar with the smaller id: it holds the gate's
    /// own fields (name, and Threshold's portal mode and owner keys), and its id is the gate's id. Every gate
    /// reaches every other: the destination is picked on the map each time a ship sails in.</summary>
    public static class SeaGateFields
    {
        public const string PillarPrefab = "Threshold_SeaGatePillar";
        public static readonly int PillarHash = PillarPrefab.GetStableHashCode();

        // The ZDO keys are kept as the hashes the game stores them under (the same a ZDO's string overloads work out
        // on every call): ships read theirs every physics step and on every hit.

        // Pillar ZDO keys.
        public static readonly int IdKey = Hash("threshold.sg.id");            // long, this pillar's own id
        public static readonly int PartnerKey = Hash("threshold.sg.partner");  // long, the partner pillar's id
        public static readonly int SidesKey = Hash("threshold.sg.sides");      // int, SideFront | SideBack: which sides are deep enough to exit on
        public static readonly int NameKey = Hash("threshold.sg.name");        // string, the gate's name (anchor only)

        // Ship ZDO keys.
        public static readonly int JumpKey = Hash("threshold.sg.jump");        // int, the current jump's id
        public static readonly int StateKey = Hash("threshold.sg.state");      // int, JumpState
        public static readonly int SpeedKey = Hash("threshold.sg.speed");      // float, speed to give back on release
        public static readonly int CrewKey = Hash("threshold.sg.crew");        // int, players aboard when the jump began
        public static readonly int LandedKey = Hash("threshold.sg.landed");    // int, crew members who reported landing
        public static readonly int LandedAtKey = Hash("threshold.sg.landedat");    // long ticks, when the latest crew report was counted
        public const string LandedPlayerPrefix = "threshold.sg.landed."; // + player id: int, the jump that player's report counted for
        public static readonly int MoveAtKey = Hash("threshold.sg.moveat");    // long ticks, when the ship moves to the destination
        public static readonly int UntilKey = Hash("threshold.sg.until");      // long ticks, give up waiting for the crew
        public static readonly int SafeKey = Hash("threshold.sg.safe");        // long ticks, the ship takes no damage until then
        public static readonly int DestPosKey = Hash("threshold.sg.dpos");     // Vector3, the ship's destination position
        public static readonly int DestRotKey = Hash("threshold.sg.drot");     // Quaternion, the ship's destination rotation
        public static readonly int DestAnchorKey = Hash("threshold.sg.danchor");   // Vector3, the destination anchor pillar
        public static readonly int DestPartnerKey = Hash("threshold.sg.dpartner"); // Vector3, the destination partner pillar
        public static readonly int StopKey = Hash("threshold.sg.stop");        // int, the current stop's id, so a client tells one stop from the next
        public static readonly int StopGateKey = Hash("threshold.sg.sgate");   // long, the gate the ship stopped in
        public static readonly int StopSideKey = Hash("threshold.sg.sside");   // int, the side of that gate the ship's centre was on
        public static readonly int PickedKey = Hash("threshold.sg.picked");    // long, the gate the helmsman picked; 0 while choosing
        public static readonly int SailOnKey = Hash("threshold.sg.sailon");    // long, a gate the ship sails on through after the map closed
        public static readonly int SailOnSideKey = Hash("threshold.sg.sailside"); // int, the side it came from: no stop there until it leaves or crosses

        // Player ZDO key, written by each client for its own player.
        public static readonly int HeavyKey = Hash("threshold.sg.heavy");      // bool, carrying something that may not teleport

        // RPCs on a pillar's ZNetView.
        public const string PairRpc = "threshold.SeaGatePair";
        public const string UnpairRpc = "threshold.SeaGateUnpair";
        public const string SetNameRpc = "threshold.SeaGateSetName";
        public const string SetModeRpc = "threshold.SeaGateSetMode";

        // RPC on a ship's ZNetView: (int jump id, long player id), counted once per player per jump.
        public const string LandedRpc = "threshold.SeaGateLanded";

        // RPC on a ship's ZNetView, helmsman to owner: (int stop id, long picked gate id; 0 sails on).
        public const string PickRpc = "threshold.SeaGatePick";

        public const int SideFront = 1;
        public const int SideBack = 2;
        public const int BothSides = SideFront | SideBack;

        private static int Hash(string key) => key.GetStableHashCode();

        public static bool IsPillar(ZDO zdo) => zdo != null && zdo.GetPrefab() == PillarHash;

        public static long GetId(ZDO zdo) => zdo != null ? zdo.GetLong(IdKey, 0L) : 0L;
        public static long GetPartner(ZDO zdo) => zdo != null ? zdo.GetLong(PartnerKey, 0L) : 0L;
        public static int GetSides(ZDO zdo) => zdo != null ? zdo.GetInt(SidesKey, 0) : 0;
        public static string GetName(ZDO zdo) => zdo != null ? zdo.GetString(NameKey, "") : "";

        /// <summary>A pair counts only when each pillar names the other: a one-sided link (a pairing race lost, a
        /// partner destroyed while unloaded) reads as unpaired, never as a gate.</summary>
        public static bool IsMutual(ZDO a, ZDO b)
        {
            long idA = GetId(a);
            long idB = GetId(b);
            return idA != 0L && idB != 0L && idA != idB && GetPartner(a) == idB && GetPartner(b) == idA;
        }

        /// <summary>The anchor of a mutual pair: the smaller id.</summary>
        public static bool IsAnchor(ZDO self, ZDO partner) => GetId(self) < GetId(partner);

        /// <summary>A random non-zero id.</summary>
        public static long NewId()
        {
            long id = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
            return id != 0L ? id : 1L;
        }

        public static JumpState GetState(ZDO ship) => ship != null ? (JumpState)ship.GetInt(StateKey, 0) : JumpState.None;
        public static int GetJump(ZDO ship) => ship != null ? ship.GetInt(JumpKey, 0) : 0;

        /// <summary>A jump under way, from the freeze to the release; not a ship stopped to choose.</summary>
        public static bool IsJumping(ZDO ship)
        {
            JumpState state = GetState(ship);
            return state == JumpState.Frozen || state == JumpState.Moved || state == JumpState.Settled;
        }

        /// <summary>Held still by a sea gate: choosing or jumping.</summary>
        public static bool IsStopped(ZDO ship) => GetState(ship) != JumpState.None;

        /// <summary>Server time in ticks, the clock every machine shares (ZDO times are stored in it).</summary>
        public static long Now => ZNet.instance != null ? ZNet.instance.GetTime().Ticks : 0L;

        public static long After(float seconds) => Now + (long)(seconds * TimeSpan.TicksPerSecond);

        public static float WaterLevel => ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
    }
}
