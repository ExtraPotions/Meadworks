// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// Where an avalanche rolls: from the slam, step by step along the ground in one direction, until its strength is
    /// spent. On flat ground it rolls `Avalanche Length` metres; every metre it climbs costs it <see cref="ClimbCost"/>
    /// metres of that, and every metre it drops gives back <see cref="DropGain"/> of one, so below the giant it runs far
    /// and above it dies within a few steps. It stops at water, and after 50 metres whatever the slope.
    /// </summary>
    public static class RimeWavePath
    {
        public const float Step = 2f;
        private const float ClimbCost = 3f;
        private const float DropGain = 0.5f;
        private const int MaxSteps = 25;          // 50 metres at most, however steep

        public static List<Vector3> Trace(Vector3 start, Vector3 direction, float length)
        {
            var path = new List<Vector3>();
            direction.y = 0f;
            direction.Normalize();
            Vector3 at = Ground(start);
            float strength = length;
            for (int i = 0; i < MaxSteps && strength > 0f; i++)
            {
                Vector3 next = Ground(at + direction * Step);
                if (next.y < ZoneSystem.instance.m_waterLevel)
                {
                    break;
                }
                strength -= Cost(next.y - at.y);
                path.Add(next);
                at = next;
            }
            return path;
        }

        /// <summary>What one step costs: its length, more for a climb, less for a drop (never less than a quarter).</summary>
        private static float Cost(float rise) =>
            rise >= 0f ? Step + rise * ClimbCost : Mathf.Max(Step * 0.25f, Step + rise * DropGain);

        private static Vector3 Ground(Vector3 point)
        {
            point.y = ZoneSystem.instance.GetGroundHeight(point);
            return point;
        }
    }
}
