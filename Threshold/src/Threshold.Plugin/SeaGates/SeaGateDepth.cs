// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGateDepth.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>One measured spot of a gate's water: where (at sea level), how deep, whether it is between the pillars
    /// (side 0) or out on one side, and whether it is deep enough.</summary>
    internal readonly struct DepthSample
    {
        public readonly Vector3 Surface;
        public readonly float Depth;
        public readonly int Side;
        public readonly bool Ok;

        public DepthSample(Vector3 surface, float depth, int side, bool ok)
        {
            Surface = surface;
            Depth = depth;
            Side = side;
            Ok = ok;
        }
    }

    /// <summary>How deep the water is between two pillars and on each side of the surface, from the terrain alone.
    /// Depth is sea level minus the terrain height: the loaded heightmap's own vertex heights
    /// (<c>Heightmap.GetHeight</c>, player terrain edits included, pieces and docks never), else a ray against the
    /// terrain layer only (<c>ZoneSystem.GetGroundHeight</c>). Where neither knows the ground the depth counts as
    /// zero, so unknown water never passes as deep. A survey measures nine spots: the midpoint and both quarter points
    /// between the pillars, and 5, 10 and 15 m out on each side; the placement preview draws exactly these.</summary>
    internal static class SeaGateDepth
    {
        private static readonly float[] SideDistances = { 5f, 10f, 15f };
        private static readonly int[] Sides = { SeaGateFields.SideFront, SeaGateFields.SideBack };

        /// <summary>The water depth at a point, zero or less on land and where the ground is unknown.</summary>
        public static float At(Vector3 point)
        {
            return TerrainHeight(point, out float ground) ? SeaGateFields.WaterLevel - ground : 0f;
        }

        private static bool TerrainHeight(Vector3 point, out float height)
        {
            try
            {
                if (Heightmap.GetHeight(point, out height))
                    return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                // A heightmap that has woken but not built its heights yet: ask the terrain colliders instead.
            }
            height = 0f;
            return ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(point, out height);
        }

        /// <summary>Measures the gate's nine spots into <paramref name="into"/> (cleared first).</summary>
        public static void Survey(GateGeometry gate, float minDepth, List<DepthSample> into)
        {
            into.Clear();
            for (int quarter = -1; quarter <= 1; quarter++)
                Add(into, gate.ToWorld(new Vector3(gate.Width * 0.25f * quarter, 0f, 0f)), 0, minDepth);
            foreach (int side in Sides)
            {
                foreach (float distance in SideDistances)
                    Add(into, gate.Mid + gate.OutOf(side) * distance, side, minDepth);
            }
        }

        private static void Add(List<DepthSample> into, Vector3 surface, int side, float minDepth)
        {
            float depth = At(surface);
            into.Add(new DepthSample(surface, depth, side, depth >= minDepth));
        }

        /// <summary>The verdict token for the water between the pillars, or null when it is deep enough everywhere: no
        /// water at all when every spot is dry, else too shallow, with the shallowest depth found.</summary>
        public static string SpanProblem(List<DepthSample> samples, out float shallowest)
        {
            shallowest = float.MaxValue;
            float deepest = float.MinValue;
            bool ok = true;
            foreach (DepthSample sample in samples)
            {
                if (sample.Side != 0)
                    continue;
                shallowest = Mathf.Min(shallowest, sample.Depth);
                deepest = Mathf.Max(deepest, sample.Depth);
                ok &= sample.Ok;
            }
            if (deepest <= 0f)
                return SeaGateWords.PairNoWater;
            return ok ? null : SeaGateWords.PairShallow;
        }

        /// <summary>The sides a ship may come out on, in the gate's own frame: a side counts when all its spots are deep
        /// enough. <paramref name="shallowest"/> is the shallowest spot of the better side, for the reason text.</summary>
        public static int UsableSides(List<DepthSample> samples, out float shallowest)
        {
            float front = Shallowest(samples, SeaGateFields.SideFront);
            float back = Shallowest(samples, SeaGateFields.SideBack);
            shallowest = Mathf.Max(front, back);
            int sides = 0;
            if (SideOk(samples, SeaGateFields.SideFront))
                sides |= SeaGateFields.SideFront;
            if (SideOk(samples, SeaGateFields.SideBack))
                sides |= SeaGateFields.SideBack;
            return sides;
        }

        /// <summary>Whether every spot on one side (or, for side 0, between the pillars) is deep enough.</summary>
        public static bool SideOk(List<DepthSample> samples, int side)
        {
            foreach (DepthSample sample in samples)
            {
                if (sample.Side == side && !sample.Ok)
                    return false;
            }
            return true;
        }

        private static float Shallowest(List<DepthSample> samples, int side)
        {
            float shallowest = float.MaxValue;
            foreach (DepthSample sample in samples)
            {
                if (sample.Side == side)
                    shallowest = Mathf.Min(shallowest, sample.Depth);
            }
            return shallowest;
        }
    }
}
