// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion
{
    /// <summary>
    /// Puts one tentacle's bones (<c>kt_00</c> to <c>kt_15</c>) on a pose: each bone at its point, pointing at the next,
    /// its back (the side away from the suckers) carried smoothly from bone to bone so the tentacle never twists. The
    /// bones' own axes are whatever the model's import gave them: each bone's rest rotation is taken against the
    /// tentacle's rest frame (straight along +Z, back up) once, while the copy is still at rest, and kept as an offset.
    /// </summary>
    public class TentacleRig
    {
        public const string BonePrefix = "kt_";

        private readonly Transform[] _bones;
        private readonly Quaternion[] _rest;

        private TentacleRig(Transform root, Transform[] bones)
        {
            Root = root;
            _bones = bones;
            _rest = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                _rest[i] = Quaternion.Inverse(root.rotation) * bones[i].rotation;
            }
        }

        public Transform Root { get; }

        /// <summary>The rig of a tentacle still at rest, or null when a bone is missing.</summary>
        public static TentacleRig? Find(Transform root)
        {
            var bones = new Transform[TentacleSpec.Bones];
            for (int i = 0; i < bones.Length; i++)
            {
                Transform? bone = RigBone.Named(root, BonePrefix + i.ToString("00"));
                if (bone == null)
                {
                    return null;
                }
                bones[i] = bone;
            }
            return new TentacleRig(root, bones);
        }

        /// <summary>
        /// The bones onto the pose. <paramref name="side"/> is the frame's side axis: the back starts facing away from
        /// the way the tentacle curls (the suckers inside the curl, and down when it lies flat).
        /// </summary>
        public void Pose(Vector3[] points, Vector3 side)
        {
            Vector3 previous = Along(points, 0, Vector3.up);
            Vector3 back = Start(side, previous);
            for (int i = 0; i < _bones.Length; i++)
            {
                Vector3 direction = Along(points, i, previous);
                back = Vector3.ProjectOnPlane(Quaternion.FromToRotation(previous, direction) * back, direction);
                back = back.sqrMagnitude > 1e-6f ? back.normalized : Start(side, direction);
                _bones[i].SetPositionAndRotation(points[i], Quaternion.LookRotation(direction, back) * _rest[i]);
                previous = direction;
            }
        }

        private static Vector3 Along(Vector3[] points, int i, Vector3 fallback)
        {
            Vector3 direction = points[i + 1] - points[i];
            return direction.sqrMagnitude > 1e-8f ? direction.normalized : fallback;
        }

        private static Vector3 Start(Vector3 side, Vector3 direction)
        {
            Vector3 back = Vector3.Cross(side, direction);
            if (back.sqrMagnitude < 1e-6f)
            {
                back = Vector3.ProjectOnPlane(Vector3.up, direction);
            }
            return back.sqrMagnitude > 1e-6f ? back.normalized : Vector3.Cross(direction, Vector3.right).normalized;
        }
    }
}
