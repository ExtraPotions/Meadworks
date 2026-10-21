// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Core
{
    /// <summary>
    /// A two-handed weapon's haft in the frame of its model in the right hand: its centre line by the distance up it from
    /// the right fist, and the stretch of it the left fist may hold.
    /// </summary>
    public sealed class Haft
    {
        public readonly Func<float, Vector3> Point;
        public readonly float Low, High;

        public Haft(Func<float, Vector3> point, float low, float high) => (Point, Low, High) = (point, low, high);

        /// <summary>The haft's direction at a height, up it.</summary>
        public Vector3 Along(float height) => (Point(height + 0.002f) - Point(height - 0.002f)).normalized;
    }

    /// <summary>
    /// Keeps a player's left fist on a two-handed weapon's haft after each pose (AssetWorkshop Greataxe/GreataxeGrip, the
    /// same solve the previews show): the game's clips hold the left fist where its own weapons' hafts are, which a bone
    /// weapon's haft may not be (the Executioner's greataxe), and some leave it off the haft altogether (the atgeir's).
    /// The fist's grip (LeftHand_Attach) goes to the point of the haft, within the stretch the fist may hold, nearest
    /// where the clip has it among the points the left arm reaches from its shoulder (the nearest to the shoulder when
    /// none is), turned so its +Z runs along the haft; the forearm and upper arm follow (two-bone IK, the elbow on its side).
    /// Where the clip itself lets go (the atgeir's third swing throws the left hand 0.7 to 1.1 m off the haft for a
    /// moment) the hand goes with the clip: the grip holds fully up to <see cref="Holds"/> off the haft, lets go by
    /// <see cref="Lets"/>, and eases between the two over a few frames.
    /// </summary>
    public sealed class HaftGrip
    {
        private const float Step = 0.01f, Holds = 0.5f, Lets = 0.75f, Ease = 6f;

        private readonly Transform arm, forearm, hand, grip;
        private float weight = 1f;

        private HaftGrip(Transform arm, Transform forearm, Transform hand, Transform grip) =>
            (this.arm, this.forearm, this.hand, this.grip) = (arm, forearm, hand, grip);

        /// <summary>The grip for a player's rig, or null when it lacks the left arm's bones.</summary>
        public static HaftGrip? Of(Transform visual)
        {
            Transform? Bone(string name) => ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs.GameMaterials.Find(visual, name);
            Transform? arm = Bone("LeftArm"), forearm = Bone("LeftForeArm"), hand = Bone("LeftHand"), grip = Bone("LeftHand_Attach");
            return arm == null || forearm == null || hand == null || grip == null ? null : new HaftGrip(arm, forearm, hand, grip);
        }

        /// <summary>After the animator has posed the player; `frame` the weapon's model in the right hand, `haft` in its frame.</summary>
        public void Apply(Transform frame, Haft haft)
        {
            Vector3 at = grip.position;
            Vector3 on = Chosen(frame, haft, at, out Vector3 along);
            float wanted = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Holds, Lets, (on - at).magnitude));
            weight = Mathf.MoveTowards(weight, wanted, Ease * Time.deltaTime);
            if (weight <= 0.001f)
            {
                return;
            }
            Quaternion turn = Quaternion.Slerp(hand.rotation, Wrapped(along) * hand.rotation, weight);
            Vector3 offset = turn * (Quaternion.Inverse(hand.rotation) * (at - hand.position));
            Vector3 elbow = forearm.position, middle = (arm.position + hand.position) * 0.5f;
            TwoBoneIk.Solve(arm, forearm, hand, Vector3.Lerp(at, on, weight) - offset, elbow + (elbow - middle).normalized * 0.3f);
            hand.rotation = turn;
        }

        /// <summary>The turn that lays the grip's +Z along the haft, whichever way along it the clip holds it.</summary>
        private Quaternion Wrapped(Vector3 along)
        {
            Vector3 z = grip.forward;
            return Quaternion.FromToRotation(z, Vector3.Dot(z, along) >= 0f ? along : -along);
        }

        /// <summary>The haft's point the fist takes (of those in reach, the nearest to `point`), and the haft's way there.</summary>
        private Vector3 Chosen(Transform frame, Haft haft, Vector3 point, out Vector3 along)
        {
            Vector3 shoulder = arm.position;
            float reach = 0.98f * ((forearm.position - shoulder).magnitude + (hand.position - forearm.position).magnitude + (grip.position - hand.position).magnitude);
            (Vector3 best, float height, float score) = (Vector3.zero, haft.Low, float.MaxValue);
            for (float h = haft.Low; h <= haft.High; h += Step)
            {
                Vector3 p = frame.TransformPoint(haft.Point(h));
                float fromShoulder = (p - shoulder).magnitude;
                float s = fromShoulder <= reach ? (p - point).sqrMagnitude : 1000f + fromShoulder;
                if (s < score)
                {
                    (best, height, score) = (p, h, s);
                }
            }
            along = frame.TransformDirection(haft.Along(height)).normalized;
            return best;
        }
    }
}
