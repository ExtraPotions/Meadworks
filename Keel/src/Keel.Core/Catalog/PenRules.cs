// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: deck pen rules (design E1 to E5). Pen behavior follows the TheGreatestShips 0.9.10 README
// (behavior reference only, no code); the containment threshold and the at-rest speed are Keel's.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Keel.Catalog
{
    public enum GateDecision { Accepted, RefusedMoving, InvalidGate }

    /// <summary>
    /// An open gate's shape in the pen's root-local side plane (distance outboard from the centreline, height): a boarding
    /// ramp from the deck inside the gate up over the hull's rail to the apex, and the gate itself folded outboard and down
    /// from the apex to below the deck. Both slopes are in degrees below or above the horizontal.
    /// </summary>
    public readonly struct RampPose
    {
        public RampPose(float hingeOut, float hingeHeight, float length, float boardingStart, float floor)
        {
            HingeOut = hingeOut;
            HingeHeight = hingeHeight;
            Length = length;
            BoardingStart = boardingStart;
            Floor = floor;
        }

        /// <summary>Where the gate's hinge sits when open: the hull's outer edge.</summary>
        public float HingeOut { get; }
        /// <summary>The apex: the hinge's height, clear of the rail.</summary>
        public float HingeHeight { get; }
        /// <summary>The open gate's length along its slope (the closed gate's height, stretched when the apex is high).</summary>
        public float Length { get; }
        /// <summary>Where the boarding ramp leaves the deck (inboard of the hinge; the deck when there is no rail).</summary>
        public float BoardingStart { get; }
        public float Floor { get; }

        public float EndOut => HingeOut + Length * (float)Math.Cos(PenRules.RampSlope * Math.PI / 180);
        public float EndHeight => HingeHeight - Length * (float)Math.Sin(PenRules.RampSlope * Math.PI / 180);
        public float BoardingRun => HingeOut - BoardingStart;
        public float BoardingRise => HingeHeight - Floor;
        public float BoardingLength => (float)Math.Sqrt(BoardingRun * BoardingRun + BoardingRise * BoardingRise);
        /// <summary>The boarding ramp's slope; 0 when the apex is the deck.</summary>
        public float BoardingSlope => BoardingRise <= 0f ? 0f : BoardingRun <= 0f ? 90f : (float)(Math.Atan2(BoardingRise, BoardingRun) * 180 / Math.PI);
        /// <summary>Whether an animal can climb both slopes (the game's creatures climb about 45 degrees; Keel keeps 35).</summary>
        public bool Walkable => BoardingSlope <= PenRules.MaxWalkableSlope && PenRules.RampSlope <= PenRules.MaxWalkableSlope;
    }

    public static class PenRules
    {
        /// <summary>A gate on each side of the pen; each folds outboard and down as a ramp.</summary>
        public const int GatesPerPen = 2;

        /// <summary>Slope of the open gate and of the boarding ramp, degrees from the horizontal.</summary>
        public const float RampSlope = 30f;

        /// <summary>The steepest slope Keel offers animals (the game's creatures climb about 45 degrees).</summary>
        public const float MaxWalkableSlope = 35f;

        /// <summary>The open gate ends this far below the deck, so it reaches a dock or the ground at deck level or lower.</summary>
        public const float RampEndBelowDeck = 0.25f;

        /// <summary>The open gate is stretched to at most this many times its closed height.</summary>
        public const float MaxRampStretch = 3f;

        /// <summary>Clearance kept between the ramps and the hull's rail.</summary>
        public const float RailClearance = 0.05f;

        /// <summary>The boarding ramp starts no closer than this to the centreline.</summary>
        public const float MinBoardingStart = 0.2f;

        /// <summary>
        /// The open gate's shape. <paramref name="rail"/> is the hull's top surface outboard of the pen side at the gate
        /// (distance outboard from the centreline, height; NaN heights are skipped). The hinge goes to the outermost hull
        /// point at the lowest apex from which a <see cref="RampSlope"/> boarding ramp clears every rail point; the gate
        /// folds outboard and down from there, long enough to end <see cref="RampEndBelowDeck"/> below the deck.
        /// </summary>
        public static RampPose OpenRamp(float penSide, float floor, float gateLength, IEnumerable<(float Out, float Height)> rail)
        {
            if (rail == null) throw new ArgumentNullException(nameof(rail));
            var tan = (float)Math.Tan(RampSlope * Math.PI / 180);
            var sin = (float)Math.Sin(RampSlope * Math.PI / 180);
            var outer = penSide;
            var points = new List<(float Out, float Height)>();
            foreach (var point in rail)
            {
                if (float.IsNaN(point.Out) || float.IsNaN(point.Height) || float.IsInfinity(point.Out) || float.IsInfinity(point.Height)) continue;
                points.Add(point);
                outer = Math.Max(outer, point.Out);
            }
            var apex = floor;
            foreach (var point in points)
                if (point.Height > floor) apex = Math.Max(apex, point.Height + RailClearance + (outer - point.Out) * tan);
            var start = apex > floor ? Math.Max(MinBoardingStart, outer - (apex - floor) / tan) : outer;
            var length = Math.Min(gateLength * MaxRampStretch, Math.Max(gateLength, (apex - floor + RampEndBelowDeck) / sin));
            return new RampPose(outer, apex, length, start, floor);
        }

        /// <summary>Thickness of the pen's floor slab, whose top is the measured deck.</summary>
        public const float FloorThickness = 0.1f;

        /// <summary>
        /// How far outboard the pen's floor slab reaches across a gate's gap: the pen side, or the foot of the boarding ramp
        /// (the deck-level hinge when the hull has no raised rail) when that lies further out. The game's navigation mesh is
        /// built from piece colliders; in game (run r2) a Boar pathing to a point over the pen center ended in the water
        /// beside the hull, not on the deck, so the slab gives animals a piece surface joined to the ramp across the pen.
        /// </summary>
        public static float FloorReach(float penSide, RampPose pose) => Math.Max(penSide, pose.BoardingStart);

        /// <summary>How far below the deck the pen's floor is solid, past the waterline of the hull.</summary>
        public const float FloorDepth = 1.0f;

        /// <summary>
        /// Center height and height of the solid under the pen's footprint, top at the deck. The navigation mesh ignores hull
        /// colliders, so the water surface inside the hull was swim mesh: in game (c0312-r2) a Boar's path to the pen center
        /// ended on the water under a thin deck slab (Byrding waterline 0.42 m under the deck) and the Boar swam beneath the
        /// hull. A solid reaching past the waterline leaves only its top, the pen floor, on the mesh there.
        /// </summary>
        public static (float Centre, float Height) FloorSolid(float deck) => (deck - FloorDepth / 2f, FloorDepth);

        /// <summary>
        /// The gate's rotation about the ship's forward axis, degrees: upright when closed; open, folded outboard past the
        /// horizontal to <see cref="RampSlope"/> below it. Gate 0 is on the port (negative x) side, so it turns positive.
        /// </summary>
        public static float GateAngle(int gate, bool open) => open ? Side(gate) * (90f + RampSlope) : 0f;

        /// <summary>The boarding ramp's rotation from upright, degrees, with the same sign rule as <see cref="GateAngle"/>.</summary>
        public static float BoardingAngle(int gate, RampPose pose) => Side(gate) * (90f - pose.BoardingSlope);

        private static float Side(int gate) => gate == 0 ? 1f : -1f;

        /// <summary>Below this speed (m/s) a ship counts as at rest: the lip drops so animals can be lifted in or walk off.</summary>
        public const float AtRestSpeed = 0.1f;

        /// <summary>
        /// The speed the pen rules compare with <see cref="AtRestSpeed"/> and Containment Speed: the horizontal part of the
        /// hull's world velocity. A moored ship rides the waves up and down (the game scales wave height with wind
        /// intensity), and that heave is no movement over the water.
        /// </summary>
        public static float RestSpeed(float x, float y, float z) => (float)Math.Sqrt(x * x + z * z);

        /// <summary>Shepherd's pen contract (Shepherd design 8.3), which Keel calls to register each pen.</summary>
        public const string Capability = "meadworks.livestock.pens";
        public const int CapabilityMajor = 1;

        /// <summary>
        /// Whether the invisible lip along the rails holds animals in: above Containment Speed, or with every gate closed
        /// while the ship is not at rest. At rest it is gone.
        /// </summary>
        public static bool LipActive(float speed, float containmentSpeed, bool everyGateClosed)
        {
            if (float.IsNaN(speed)) return false;
            var s = Math.Abs(speed);
            if (s <= AtRestSpeed) return false;
            return s > Math.Max(0f, containmentSpeed) || everyGateClosed;
        }

        /// <summary>The lip's gate condition: true only when no gate of the pen is open.</summary>
        public static bool EveryGateClosed(bool[] gateOpen)
        {
            if (gateOpen == null) throw new ArgumentNullException(nameof(gateOpen));
            foreach (var open in gateOpen) if (open) return false;
            return true;
        }

        /// <summary>
        /// A request to open or close a gate, decided by the ship's owner: the gate must exist; closing always works;
        /// with Gates Open At Rest Only, opening is refused while the ship moves faster than Containment Speed.
        /// </summary>
        public static GateDecision Gate(int gate, int gateCount, bool open, float speed, float containmentSpeed, bool openAtRestOnly)
        {
            if (gate < 0 || gate >= gateCount) return GateDecision.InvalidGate;
            if (open && openAtRestOnly && !float.IsNaN(speed) && Math.Abs(speed) > Math.Max(0f, containmentSpeed)) return GateDecision.RefusedMoving;
            return GateDecision.Accepted;
        }

        /// <summary>bool on the ship's ZDO, written by its owner: gate <paramref name="gate"/> is open.</summary>
        public static string GateKey(int gate)
        {
            if (gate < 0 || gate >= GatesPerPen) throw new ArgumentOutOfRangeException(nameof(gate));
            return GateKeys[gate];
        }

        // Read every frame by every loaded pen; built once.
        private static readonly string[] GateKeys = BuildGateKeys();

        private static string[] BuildGateKeys()
        {
            var keys = new string[GatesPerPen];
            for (var i = 0; i < keys.Length; i++) keys[i] = KeelCore.ZdoPrefix + "gate." + i;
            return keys;
        }

        /// <summary>The herd limit Keel registers with Shepherd: the pen capacity, never negative.</summary>
        public static int HerdLimit(int penCapacity) => Math.Max(0, penCapacity);
    }
}