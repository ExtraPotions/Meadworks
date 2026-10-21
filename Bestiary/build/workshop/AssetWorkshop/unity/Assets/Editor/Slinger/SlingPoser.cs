using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// Where the right hand is in a key: hanging as in the idle; on its way to the satchel, in it, lifting a stone out;
    /// at the pouch; drawn to the cheek (and a little further); let go; following through.
    /// </summary>
    public enum RightHand { Idle, ToSatchel, InSatchel, Lift, Pouch, Cheek, FullDraw, Released, FollowThrough }

    /// <summary>
    /// One key of the shot. Yaw turns the chest (positive brings the left shoulder towards the target, the archer's
    /// stance); the left hand holds the slingshot out at LeftReach of the arm's length (below zero: the idle arm) and
    /// LeftRaise metres above the shoulder; Fist and Pinch close the left and right fingers (0 idle, 1 closed, below
    /// zero opened wide).
    /// </summary>
    public sealed class SlingKey
    {
        public float Time;
        public float Yaw;
        public float LeftReach = -1f;
        public float LeftRaise;
        public RightHand Right;
        public float Fist;
        public float Pinch;

        public SlingKey(float time) => Time = time;
    }

    /// <summary>
    /// Poses the Greydwarf skeleton for a key, starting from the first frame of its idle: turns the chest, counters with
    /// the head so it keeps looking at the target, reaches the arms with <see cref="TwoBoneIk"/> and turns the hands.
    /// Only bones the avatar maps are moved (spine3 is not one), since the pose is read back as muscles. Unity axes:
    /// the Greydwarf stands at the origin facing +Z; targets are worked out from its shoulders, head and the satchel
    /// (<see cref="SlingKit.SatchelMouth"/>, where the kit hangs it).
    /// </summary>
    public sealed class SlingPoser
    {
        private readonly GameObject greydwarf;
        private readonly AnimationClip idle;
        private Vector3 satchel;

        public SlingPoser(GameObject greydwarf, AnimationClip idle)
        {
            this.greydwarf = greydwarf;
            this.idle = idle;
        }

        public Transform Bone(string name) => SlingerReference.Bone(greydwarf, name);

        public void Pose(SlingKey key)
        {
            idle.SampleAnimation(greydwarf, 0f);
            Vector3 mouth = Bone("spine2").InverseTransformPoint(SlingKit.SatchelMouth(greydwarf));   // it hangs from spine2
            Turn(Bone("spine1"), key.Yaw * 0.4f);
            Turn(Bone("spine2"), key.Yaw * 0.6f);
            satchel = Bone("spine2").TransformPoint(mouth);
            Turn(Bone("head"), -key.Yaw * 0.85f);
            Vector3 left = Bone("l_hand").position;
            if (key.LeftReach >= 0f)
                left = ReachLeft(key.LeftReach, key.LeftRaise);
            if (key.Right != RightHand.Idle)
                ReachRight(key.Right, left);
        }

        private static void Turn(Transform bone, float degrees) =>
            bone.rotation = Quaternion.AngleAxis(degrees, Vector3.up) * bone.rotation;

        private Vector3 ReachLeft(float reach, float raise)
        {
            Vector3 shoulder = Bone("l_arm1").position;
            float length = ArmLength("l_");
            float inward = -shoulder.x * 0.6f;                     // towards the middle, in front of the eyes
            float forward = Mathf.Sqrt(Mathf.Max(0.01f, reach * reach * length * length - inward * inward - raise * raise));
            Vector3 target = shoulder + new Vector3(inward, raise, forward);
            TwoBoneIk.Solve(Bone("l_arm1"), Bone("l_arm2"), Bone("l_hand"), target, shoulder + new Vector3(-0.35f, -0.35f, 0.1f));
            Aim(Bone("l_hand"), "l_", new Vector3(0.3f, 0f, 1f), Vector3.up);
            return Bone("l_hand").position;
        }

        /// <summary>
        /// The drawing arm: up over the right shoulder into the satchel on the back, elbow high and out, fingers pointing
        /// down into it; then forward to the pouch and up to the cheek with the elbow high behind.
        /// </summary>
        private void ReachRight(RightHand right, Vector3 leftHand)
        {
            Vector3 shoulder = Bone("r_arm1").position;
            bool bag = right == RightHand.ToSatchel || right == RightHand.InSatchel;
            Vector3 elbow = bag ? new Vector3(0.35f, 0.4f, 0.1f) : right == RightHand.Lift ? new Vector3(0.45f, 0.1f, 0.05f) : new Vector3(0.45f, 0.1f, -0.4f);
            TwoBoneIk.Solve(Bone("r_arm1"), Bone("r_arm2"), Bone("r_hand"), RightTarget(right, leftHand), shoulder + elbow);
            if (bag)
                Aim(Bone("r_hand"), "r_", new Vector3(-0.2f, -1f, -0.35f), Vector3.right);
            else
                Aim(Bone("r_hand"), "r_", new Vector3(-0.25f, 0f, 1f), Vector3.up);
        }

        /// <summary>
        /// Where the right wrist goes: up past the shoulder, then just over the satchel's mouth with the fingers in it;
        /// brought forward over the shoulder; at the pouch just behind and above the slingshot hand; drawn, beside the
        /// cheek; let go, it springs back and out.
        /// </summary>
        private Vector3 RightTarget(RightHand right, Vector3 leftHand)
        {
            Vector3 face = Vector3.Lerp(Bone("head").position, Bone("head_end").position, 0.5f);   // "head" is the neck's base
            Vector3 cheek = face + new Vector3(0.1f, -0.04f, -0.02f);
            switch (right)
            {
                case RightHand.ToSatchel: return Bone("r_arm1").position + new Vector3(0.1f, 0.3f, 0.05f);
                case RightHand.InSatchel: return satchel + new Vector3(0.04f, 0.1f, 0.02f);
                case RightHand.Lift: return Bone("r_arm1").position + new Vector3(0.15f, 0.2f, 0.25f);
                case RightHand.Pouch: return leftHand + new Vector3(0.04f, 0.1f, -0.12f);
                case RightHand.FullDraw: return cheek + new Vector3(0.01f, 0f, -0.03f);
                case RightHand.Released: return cheek + new Vector3(0.06f, 0.01f, -0.1f);
                case RightHand.FollowThrough: return cheek + new Vector3(0.14f, -0.05f, -0.2f);
                default: return cheek;
            }
        }

        /// <summary>
        /// Turns a hand so its knuckles point along `knuckles` and its index finger sits on the `across` side of its
        /// little finger: a fist held upright round the slingshot's handle, the drawing hand pinching the pouch, or the
        /// fingers pointing down into the satchel.
        /// </summary>
        private void Aim(Transform hand, string side, Vector3 knuckles, Vector3 across)
        {
            Vector3 along = Bone(side + "middle1").position - hand.position;
            Vector3 spread = Bone(side + "index1").position - Bone(side + "pinky1").position;
            Quaternion now = Quaternion.LookRotation(along, spread);
            Quaternion wanted = Quaternion.LookRotation(knuckles.normalized, across);
            hand.rotation = wanted * Quaternion.Inverse(now) * hand.rotation;
        }

        private float ArmLength(string side) =>
            Vector3.Distance(Bone(side + "arm1").position, Bone(side + "arm2").position)
            + Vector3.Distance(Bone(side + "arm2").position, Bone(side + "hand").position);
    }
}
