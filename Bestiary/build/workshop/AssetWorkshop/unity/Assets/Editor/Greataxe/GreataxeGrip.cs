using System;
using UnityEngine;
using Workshop.Headsman;
using Workshop.Slinger;

namespace Workshop.Greataxe
{
    /// <summary>
    /// Keeps the player's left fist on the greataxe's haft after each pose, as the mod does on every peer: the game's
    /// Battleaxe clips hold the left fist where the Battleaxe's straight haft is, and the borrowed atgeir and sledge
    /// swings hold it wider still (13 to 18 cm off the haft on average, GreataxeFit). The fist's grip (LeftHand_Attach)
    /// goes to the point of the haft, between the right fist and the head, nearest where the clip has it among the points
    /// the left arm can reach from its shoulder (the arm's full reach where none is in reach), turned so its +Z runs
    /// along the haft (the way the clips hold it), and the forearm and upper arm follow (two-bone IK, the elbow kept on
    /// its side). Always: the user wants both hands on the axe, in the borrowed swings too (the atgeir's spin holds the
    /// left fist below the right, where this haft ends).
    /// </summary>
    public sealed class GreataxeGrip
    {
        /// <summary>
        /// The stretch of haft the fist may hold, up the model: from against the right fist (at 0.08; a fist is about
        /// 0.08 wide at this scale), where it can always reach, to the head's start (0.85).
        /// </summary>
        public const float Low = 0.16f, High = 0.8f;

        private readonly Transform arm, forearm, hand, grip, axe;
        private readonly Func<float, Vector3> spine;
        private readonly float low, high;

        /// <summary>How far the fist ended from the haft point it went for last time (out of reach).</summary>
        public float Missed { get; private set; }

        public GreataxeGrip(GameObject player, Transform axeModel) : this(player, axeModel, HeadsmanAxe.Spine, Low, High)
        {
        }

        /// <summary>Any two-handed weapon: `frame` its model in the right hand, `haftPoint` its centre line by height up it in that frame.</summary>
        public GreataxeGrip(GameObject player, Transform frame, Func<float, Vector3> haftPoint, float lowest, float highest)
        {
            (spine, low, high) = (haftPoint, lowest, highest);
            (arm, forearm, hand) = (GreataxePlayer.Bone(player, "LeftArm"), GreataxePlayer.Bone(player, "LeftForeArm"), GreataxePlayer.Bone(player, "LeftHand"));
            grip = GreataxePlayer.Bone(player, "LeftHand_Attach");
            axe = frame;
        }

        /// <summary>After the Animator has posed the player.</summary>
        public void Apply()
        {
            Vector3 at = grip.position;
            Vector3 on = Chosen(at, out Vector3 along);
            // As the mod's HaftGrip: where the clip itself lets go (off the haft past Holds), the hand goes with it.
            float wanted = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Holds, Lets, (on - at).magnitude));
            weight = Mathf.MoveTowards(weight, wanted, Ease / 30f);
            Missed = weight <= 0.001f ? 0f : (grip.position - on).magnitude;
            if (weight <= 0.001f)
                return;
            Quaternion turn = Quaternion.Slerp(hand.rotation, Wrapped(along) * hand.rotation, weight);
            Vector3 offset = turn * (Quaternion.Inverse(hand.rotation) * (at - hand.position));
            Vector3 elbow = forearm.position, middle = (arm.position + hand.position) * 0.5f;
            TwoBoneIk.Solve(arm, forearm, hand, Vector3.Lerp(at, on, weight) - offset, elbow + (elbow - middle).normalized * 0.3f);
            hand.rotation = turn;
        }

        private const float Holds = 0.5f, Lets = 0.75f, Ease = 6f;
        private float weight = 1f;

        /// <summary>The turn that lays the grip's +Z along the haft, whichever way along it the clip holds it.</summary>
        private Quaternion Wrapped(Vector3 along)
        {
            Vector3 z = grip.forward;
            return Quaternion.FromToRotation(z, Vector3.Dot(z, along) >= 0f ? along : -along);
        }

        /// <summary>
        /// The point on the haft's centre line (the model's spine, 1 cm steps) the left fist takes: of those the arm reaches
        /// from its shoulder, the nearest to where the clip has it; failing any, the nearest to the shoulder. And the
        /// haft's way there.
        /// </summary>
        private Vector3 Chosen(Vector3 point, out Vector3 along)
        {
            Vector3 shoulder = arm.position;
            float reach = 0.98f * ((forearm.position - shoulder).magnitude + (hand.position - forearm.position).magnitude + (grip.position - hand.position).magnitude);
            (Vector3 best, float h, float score) = (Vector3.zero, low, float.MaxValue);
            for (float height = low; height <= high; height += 0.01f)
            {
                Vector3 p = axe.TransformPoint(spine(height));
                float fromShoulder = (p - shoulder).magnitude;
                float s = fromShoulder <= reach ? (p - point).sqrMagnitude : 1000f + fromShoulder;
                if (s < score)
                    (best, h, score) = (p, height, s);
            }
            along = axe.TransformDirection(spine(h + 0.002f) - spine(h - 0.002f)).normalized;
            return best;
        }
    }
}
