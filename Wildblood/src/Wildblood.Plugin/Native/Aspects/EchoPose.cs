// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// One moment of an Echoing boss on its tape (<see cref="EchoTape"/>): where it stood, how it faced, where it looked,
    /// and the centre of the creature it had in its sights, if any - what its throws and breaths were aimed at.
    /// </summary>
    internal struct EchoPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Look;
        public Vector3 Aim;
        public bool Aimed;

        public EchoPose(Vector3 position, Quaternion rotation, Vector3 look, Character? target)
        {
            Position = position;
            Rotation = rotation;
            Look = look;
            Aimed = target != null;
            Aim = target != null ? target.GetCenterPoint() : Vector3.zero;
        }

        /// <summary>Between two samples: place, facing and look blended; the aim of the earlier one.</summary>
        public static EchoPose Blend(EchoPose from, EchoPose to, float share)
        {
            EchoPose pose = from;
            pose.Position = Vector3.Lerp(from.Position, to.Position, share);
            pose.Rotation = Quaternion.Slerp(from.Rotation, to.Rotation, share);
            pose.Look = Vector3.Slerp(from.Look, to.Look, share);
            return pose;
        }
    }
}
