// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The owner's latest word on where a wave's tornadoes are: the wave it belongs to, the moment it was taken on the
    /// shared clock, and each tornado's place on the ground plane and velocity. A machine that does not move the
    /// tornadoes itself carries each one on along its velocity from that moment - it changes course only gently, so a
    /// few samples a second keep the guess within a step of the owner's - but never past the end of the hunt and never
    /// more than <see cref="MaxLead"/> ahead of the sample, should the owner fall silent.
    /// </summary>
    internal sealed class TornadoTrack
    {
        /// <summary>The longest a sample is carried forward before the tornado is held where it was last seen.</summary>
        private const float MaxLead = 1f;

        public long WaveAt;
        public long Stamp;
        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<Vector3> Velocities = new List<Vector3>();

        public void Add(Vector3 position, Vector3 velocity)
        {
            Positions.Add(position);
            Velocities.Add(velocity);
        }

        /// <summary>Where tornado <paramref name="index"/> is at <paramref name="nowMs"/> by this sample.</summary>
        public bool TryPredict(int index, long nowMs, long endMs, out Vector3 at)
        {
            at = Vector3.zero;
            if (index < 0 || index >= Positions.Count)
            {
                return false;
            }
            float lead = Mathf.Clamp((Mathf.Min(nowMs, endMs) - Stamp) / 1000f, 0f, MaxLead);
            at = Positions[index] + Velocities[index] * lead;
            return true;
        }
    }
}
