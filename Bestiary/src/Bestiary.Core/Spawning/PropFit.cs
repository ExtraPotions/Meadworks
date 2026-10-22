// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Bestiary.Spawning
{
    /// <summary>
    /// Local scale and grip for a prop whose parent bone is not one meter per unit.
    /// Divide the wanted world scale by the bone lossy scale on each axis.
    /// </summary>
    public static class PropFit
    {
        public const float MinAxis = 0.0001f;

        public readonly struct Axes
        {
            public readonly float X;
            public readonly float Y;
            public readonly float Z;

            public Axes(float x, float y, float z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        /// <summary>Uniform world scale that makes a mesh of <paramref name="unitLength"/> meters at scale 1 come out <paramref name="meters"/> long.</summary>
        public static float WorldScale(float meters, float unitLength)
        {
            float length = Math.Abs(unitLength);
            return length < MinAxis ? meters : meters / length;
        }

        public static float Axis(float wanted, float bone)
        {
            float denom = Math.Abs(bone) < MinAxis ? (bone < 0f ? -MinAxis : MinAxis) : bone;
            return wanted / denom;
        }

        public static Axes Scale(float worldScale, Axes bone) =>
            new Axes(Axis(worldScale, bone.X), Axis(worldScale, bone.Y), Axis(worldScale, bone.Z));

        /// <summary>
        /// A world-meter offset as a bone-local position.
        /// <paramref name="right"/>, <paramref name="up"/> and <paramref name="forward"/> are the bone axes in world space, unit length, without scale.
        /// </summary>
        public static Axes IntoBone(Axes worldMeters, Axes right, Axes up, Axes forward, Axes boneScale)
        {
            float x = worldMeters.X * right.X + worldMeters.Y * right.Y + worldMeters.Z * right.Z;
            float y = worldMeters.X * up.X + worldMeters.Y * up.Y + worldMeters.Z * up.Z;
            float z = worldMeters.X * forward.X + worldMeters.Y * forward.Y + worldMeters.Z * forward.Z;
            return new Axes(Axis(x, boneScale.X), Axis(y, boneScale.Y), Axis(z, boneScale.Z));
        }

        /// <summary>
        /// Prop-local face of a plate. The axis is the thinnest side of <paramref name="extent"/>.
        /// The sign is the side <paramref name="center"/> sits on: on both bucklers that is the dome, and the other side is the grip.
        /// A center within 2% of the thickness has no dome to read, and the face is the negative axis.
        /// </summary>
        public static Axes Front(Axes extent, Axes center)
        {
            float x = Math.Abs(extent.X);
            float y = Math.Abs(extent.Y);
            float z = Math.Abs(extent.Z);
            if (x + y + z < MinAxis) return new Axes(0f, 0f, 0f);
            int axis = x <= y && x <= z ? 0 : y <= z ? 1 : 2;
            float along = axis == 0 ? center.X : axis == 1 ? center.Y : center.Z;
            float thick = axis == 0 ? x : axis == 1 ? y : z;
            float sign = thick < MinAxis || Math.Abs(along) <= thick * 0.02f ? -1f : along > 0f ? 1f : -1f;
            return axis == 0 ? new Axes(sign, 0f, 0f) : axis == 1 ? new Axes(0f, sign, 0f) : new Axes(0f, 0f, sign);
        }

        public static bool FacesNegative(Axes front) => front.X < 0f || front.Y < 0f || front.Z < 0f;

        public const string StripJoint = "Joint";
        public const string StripBody = "Rigidbody";
        public const string StripRest = "Rest";

        /// <summary>Joints, then rigidbodies, then colliders, item logic and network components.</summary>
        public static readonly string[] StripPasses = { StripJoint, StripBody, StripRest };

        public static int StripRank(string pass)
        {
            for (int i = 0; i < StripPasses.Length; i++)
                if (StripPasses[i] == pass) return i;
            return StripPasses.Length;
        }
    }
}
