using System;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// How far a wrist is bent from the avatar's neutral hand, without the twist (which the forearm's twist muscle
    /// takes): the forearm's line seen from the hand, against where it lies at the neutral, split into bending towards
    /// the palm (the "Down-Up" muscle, 80 degrees each way) and towards the thumb or the little finger ("In-Out", only
    /// 40). <see cref="Cost"/> is the two as fractions of their limits, the sideways one counted twice; the arm's
    /// elbow is turned to where it is least (<see cref="HeadsmanStance"/>).
    /// </summary>
    public sealed class HeadsmanWrist
    {
        private readonly Vector3 forearm, thumb, palm;
        private readonly string side;
        private float twist;

        private HeadsmanWrist(string side, Vector3 forearm, Vector3 thumb, Vector3 palm)
        {
            (this.side, this.forearm, this.thumb, this.palm) = (side, forearm, thumb, palm);
        }

        /// <summary>Measures the neutral hand: the rest pose with both hands' Down-Up and In-Out muscles at zero.</summary>
        public static HeadsmanWrist Neutral(HeadsmanStance stance, HumanPoseHandler handler, string side)
        {
            stance.Rest();
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            pose.muscles[Array.IndexOf(HumanTrait.MuscleName, side + " Hand Down-Up")] = 0f;
            pose.muscles[Array.IndexOf(HumanTrait.MuscleName, side + " Hand In-Out")] = 0f;
            handler.SetHumanPose(ref pose);
            Transform hand = stance.Bone(side + "Hand");
            Vector3 Local(Vector3 world) => (Quaternion.Inverse(hand.rotation) * world).normalized;
            Vector3 forearm = Local(hand.position - stance.Bone(side + "ForeArm").position);
            Vector3 thumb = Local(stance.Bone(side + "HandIndex1").position - stance.Bone(side + "HandPinky1").position);
            Vector3 palm = Vector3.Cross(thumb, forearm).normalized;
            var wrist = new HeadsmanWrist(side, forearm, Vector3.Cross(forearm, palm).normalized, palm);
            wrist.twist = wrist.Roll(stance);
            stance.Rest();
            return wrist;
        }

        /// <summary>
        /// The bend now, as fractions of the muscles' limits (sideways counted twice), plus the hand's roll about the
        /// forearm away from the neutral's (the forearm's twist muscle, 90 degrees each way).
        /// </summary>
        public float Cost(HeadsmanStance stance)
        {
            var (flex, deviation) = Bend(stance);
            float roll = Mathf.DeltaAngle(twist, Roll(stance));
            return Mathf.Abs(flex) / 80f + 2f * Mathf.Abs(deviation) / 40f + Mathf.Abs(roll) / 90f;
        }

        /// <summary>The thumb's roll about the forearm, from the elbow's bending plane.</summary>
        private float Roll(HeadsmanStance stance)
        {
            Vector3 upper = stance.Bone(side + "Arm").position, elbow = stance.Bone(side + "ForeArm").position;
            Transform hand = stance.Bone(side + "Hand");
            Vector3 along = (hand.position - elbow).normalized;
            Vector3 bend = Vector3.Cross(elbow - upper, along);
            Vector3 thumbNow = stance.Bone(side + "HandIndex1").position - stance.Bone(side + "HandPinky1").position;
            return Vector3.SignedAngle(Vector3.ProjectOnPlane(bend, along), Vector3.ProjectOnPlane(thumbNow, along), along);
        }

        /// <summary>Degrees bent towards the palm and towards the thumb.</summary>
        public (float flex, float deviation) Bend(HeadsmanStance stance)
        {
            Transform hand = stance.Bone(side + "Hand");
            Vector3 now = (Quaternion.Inverse(hand.rotation) * (hand.position - stance.Bone(side + "ForeArm").position)).normalized;
            float flex = Vector3.SignedAngle(Vector3.ProjectOnPlane(forearm, thumb), Vector3.ProjectOnPlane(now, thumb), thumb);
            float deviation = Vector3.SignedAngle(Vector3.ProjectOnPlane(forearm, palm), Vector3.ProjectOnPlane(now, palm), palm);
            return (flex, deviation);
        }
    }
}
