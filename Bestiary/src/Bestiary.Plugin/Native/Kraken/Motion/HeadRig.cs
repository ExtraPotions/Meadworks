// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion
{
    /// <summary>One frame of the head: where it is, and how its neck, beak, siphon, mantle and body are set.</summary>
    public struct HeadPose
    {
        public Vector3 Position;     // the base of the head (where the body joins it), in the world
        public Quaternion Rotation;  // the face looks along its +Z
        public float Lean;           // degrees forward at the neck; negative rears back
        public float Beak;           // 0 shut, 1 wide open
        public float Siphon;         // 0 at rest, 1 swollen
        public float Time;           // drives the breathing
        public float Life;           // 1 alive, 0 dead: how much it breathes
        public Vector3? BodyEnd;     // where the body's lower end stays, in the world; null: it hangs straight down
    }

    /// <summary>
    /// Puts the head's bones (AssetWorkshop assets/ecp_kraken) on a <see cref="HeadPose"/>: the neck leans the face and
    /// mantle, the two halves of the beak open on their hinge, the siphon swells, the mantle breathes and sways, and the
    /// body under the head (a column on three spine bones) bends in a smooth curve from the head down to where its lower
    /// end stays, so a head lunging over a ship's rail arcs its body back into the water outside the hull. It also hands
    /// out the marked points the fight needs: the beak tip (the bite and the ink) and where each long tentacle joins the
    /// body while it swims. A missing bone is left still.
    /// </summary>
    public class HeadRig
    {
        public const int TentacleJoints = 6;
        private const float BeakOpen = 35f;
        private const float BodyTip = 1.2f;      // metres from the last spine bone to the body's lower end
        private const int CurveSteps = 24;

        private readonly Transform _root;
        private readonly RigBone? _neck, _mantle, _beakUpper, _beakLower, _siphon;
        private readonly RigBone?[] _body = new RigBone?[3];
        private readonly float[] _reach = new float[3];      // metres along the body from its first bone to each aim point
        private readonly Transform?[] _joints = new Transform?[TentacleJoints];
        private readonly Vector3[] _curve = new Vector3[CurveSteps + 1];

        public HeadRig(Transform root)
        {
            _root = root;
            _neck = RigBone.Find(root, "kh_neck");
            _mantle = RigBone.Find(root, "kh_mantle");
            _beakUpper = RigBone.Find(root, "kh_beak_upper");
            _beakLower = RigBone.Find(root, "kh_beak_lower");
            _siphon = RigBone.Find(root, "kh_siphon");
            Mouth = RigBone.Named(root, "kh_mouth") ?? root;
            FindBody();
            for (int i = 0; i < TentacleJoints; i++)
            {
                _joints[i] = RigBone.Named(root, "kh_tentacle_" + i);
            }
        }

        public Transform Root => _root;

        /// <summary>The beak's tip: where a bite lands and the ink comes out.</summary>
        public Transform Mouth { get; }

        /// <summary>Where long tentacle <paramref name="i"/> joins the body while it swims (the base when unmarked).</summary>
        public Transform Joint(int i) => _joints[i % TentacleJoints] ?? _root;

        /// <summary>How far below the head's base the body's lower end hangs when nothing bends it, in metres.</summary>
        public float BodyLength => _body[0] != null ? -_root.InverseTransformPoint(_body[0]!.Bone.position).y + _reach[2] : 0f;

        public void Apply(HeadPose pose)
        {
            _root.SetPositionAndRotation(pose.Position, pose.Rotation);
            _neck?.Turn(_root, Vector3.right, pose.Lean);
            _mantle?.Turn(_root, Vector3.forward, 3f * pose.Life * Mathf.Sin(pose.Time * 0.7f));
            _mantle?.Scale(1f + 0.035f * pose.Life * Mathf.Sin(pose.Time * 1.6f));
            _beakUpper?.Turn(_root, Vector3.right, -BeakOpen * pose.Beak);
            _beakLower?.Turn(_root, Vector3.right, BeakOpen * pose.Beak);
            _siphon?.Scale(1f + 0.45f * pose.Siphon);
            Bend(pose.BodyEnd);
        }

        // Straight down from the head, then, if its end must stay somewhere, each spine bone turned to point along a curve
        // leaving the head straight down and arriving at that end.
        private void Bend(Vector3? end)
        {
            foreach (RigBone? bone in _body)
            {
                bone?.Turn(_root, Vector3.right, 0f);
            }
            if (end == null || _body[0] == null || _body[1] == null || _body[2] == null)
            {
                return;
            }
            Trace(_body[0]!.Bone.position, -_root.up, end.Value);
            for (int i = 0; i < _body.Length; i++)
            {
                Transform bone = _body[i]!.Bone;
                Vector3 want = Along(_reach[i]) - bone.position;
                bone.rotation = Quaternion.FromToRotation(-bone.up, want) * bone.rotation;
            }
        }

        // A curve from `start`, leaving along `tangent`, to `end`: sampled into _curve.
        private void Trace(Vector3 start, Vector3 tangent, Vector3 end)
        {
            Vector3 control = start + tangent * (Vector3.Distance(start, end) * 0.5f);
            for (int i = 0; i <= CurveSteps; i++)
            {
                float t = i / (float)CurveSteps;
                _curve[i] = Vector3.Lerp(Vector3.Lerp(start, control, t), Vector3.Lerp(control, end, t), t);
            }
        }

        // The point `distance` metres along the traced curve (past its end, its end).
        private Vector3 Along(float distance)
        {
            for (int i = 1; i <= CurveSteps; i++)
            {
                float step = Vector3.Distance(_curve[i - 1], _curve[i]);
                if (step >= distance)
                {
                    return Vector3.Lerp(_curve[i - 1], _curve[i], step > 1e-5f ? distance / step : 1f);
                }
                distance -= step;
            }
            return _curve[CurveSteps];
        }

        // The spine bones and how far along the body each one aims: at the next bone's rest place, the last at the end.
        private void FindBody()
        {
            for (int i = 0; i < _body.Length; i++)
            {
                _body[i] = RigBone.Find(_root, $"kh_body_{i + 1}");
            }
            if (_body[0] == null || _body[1] == null || _body[2] == null)
            {
                return;
            }
            float first = Vector3.Distance(_body[0]!.Bone.position, _body[1]!.Bone.position);
            float second = Vector3.Distance(_body[1]!.Bone.position, _body[2]!.Bone.position);
            (_reach[0], _reach[1], _reach[2]) = (first, first + second, first + second + BodyTip);
        }
    }
}
