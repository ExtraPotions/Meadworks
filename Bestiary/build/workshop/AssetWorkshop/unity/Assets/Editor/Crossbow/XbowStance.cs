using UnityEngine;
using Workshop.Slinger;

namespace Workshop.Crossbow
{
    /// <summary>
    /// Poses the Skeleton for one key, starting from the first frame of its idle played through its own Animator: turns
    /// and bows the chest, turns the head back to the target (laying it on the stock or bowing it to watch the hands),
    /// puts the crossbow where the key wants it and bends the left arm so the fist holds it there (the crossbow rides
    /// the left fist, <see cref="XbowGrip"/>), then lets <see cref="XbowRightHand"/> do the right hand's part. Unity
    /// axes: the skeleton stands at the origin facing +Z. Only bones the avatar maps are moved, since the pose is read
    /// back as muscles; the hips and legs keep the idle's stance.
    /// </summary>
    public sealed class XbowStance
    {
        private static readonly Vector3 ShoulderPocket = new Vector3(-0.07f, 0.01f, 0.05f);   // from the RightArm joint
        private static readonly Vector3 LeftElbow = new Vector3(-0.2f, -0.45f, 0.05f);
        private static readonly Pose Lowered = new Pose(new Vector3(0.12f, 1.12f, 0.22f), Quaternion.Euler(20f, -15f, 0f));

        /// <summary>The low ready on the idle's first frame: across the belly, pointing forward, down and to the left.</summary>
        public static readonly Pose LowReady = new Pose(new Vector3(0.1f, 1.12f, 0.16f), Quaternion.Euler(20f, -45f, 0f));

        private readonly GameObject skeleton;
        private readonly XbowPoser poser;
        private readonly XbowGrip grip;
        private readonly XbowRightHand right;

        public XbowStance(GameObject skeleton, XbowPoser poser, XbowGrip grip)
        {
            this.skeleton = skeleton;
            this.poser = poser;
            this.grip = grip;
            right = new XbowRightHand(this, grip);
        }

        public XbowGrip Grip => grip;

        /// <summary>The crossbow's world pose after the last <see cref="Apply"/>.</summary>
        public Pose Crossbow { get; private set; }

        public Transform Bone(string name) => XbowReference.Bone(skeleton, name);

        /// <summary>The idle's first frame, the pose every key starts from.</summary>
        public void Rest() => poser.Pose("Idle", 0f);

        public void Apply(XbowKey key)
        {
            Rest();
            TurnChest(key.Yaw, key.Lean);
            Look(key);
            HoldLeft(Place(key.Bow));
            Crossbow = grip.BowIn(this);
            right.Reach(key.Right, Crossbow);
        }

        /// <summary>
        /// Whatever the skeleton is doing now (a frame of the game's idle, walk or run), both hands take the crossbow at
        /// the low ready, riding the chest (<see cref="XbowGrip.Carry"/>).
        /// </summary>
        public void Carry()
        {
            HoldLeft(grip.CarryIn(this));
            Crossbow = grip.BowIn(this);
            right.Reach(Hand.Wrist, Crossbow);
        }

        public static Vector3 At(Pose pose, Vector3 local) => pose.position + pose.rotation * local;

        private Pose Place(Bow bow)
        {
            switch (bow)
            {
                case Bow.Aim: return Aimed();
                case Bow.Kick: return Kicked(Aimed());
                case Bow.Raised: return Between(grip.CarryIn(this), Aimed(), 0.55f, 0.08f);
                case Bow.Lowered: return Lowered;
                default: return grip.CarryIn(this);
            }
        }

        /// <summary>Level at the target, the butt in the pocket of the right shoulder.</summary>
        private Pose Aimed()
        {
            Quaternion rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            Vector3 butt = Bone("RightArm").position + ShoulderPocket;
            return new Pose(butt - rotation * XbowParts.Butt, rotation);
        }

        /// <summary>The shot's kick: the muzzle jumps up about the butt and the whole crossbow rides back a little.</summary>
        private static Pose Kicked(Pose aim)
        {
            Vector3 butt = At(aim, XbowParts.Butt);
            Quaternion kick = Quaternion.AngleAxis(-9f, aim.rotation * Vector3.right);
            return new Pose(butt + kick * (aim.position - butt) - aim.forward * 0.035f, kick * aim.rotation);
        }

        private static Pose Between(Pose a, Pose b, float t, float lift) =>
            new Pose(Vector3.Lerp(a.position, b.position, t) + Vector3.up * lift, Quaternion.Slerp(a.rotation, b.rotation, t));

        /// <summary>Turns the left fist to hold the crossbow at `bow` and bends the arm to bring it there, elbow down.</summary>
        private void HoldLeft(Pose bow)
        {
            Pose hand = grip.HandFor(this, bow);
            Transform left = Bone("LeftHand");
            TwoBoneIk.Solve(Bone("LeftArm"), Bone("LeftForeArm"), left, hand.position, Bone("LeftArm").position + LeftElbow);
            left.rotation = hand.rotation;
        }

        /// <summary>
        /// Positive yaw takes the right shoulder back, spread over the three spine bones; lean bows the back forward
        /// over the lower two.
        /// </summary>
        private void TurnChest(float yaw, float lean)
        {
            Turn(Bone("Spine"), Vector3.right, lean * 0.5f);
            Turn(Bone("Spine1"), Vector3.right, lean * 0.5f);
            Turn(Bone("Spine"), Vector3.up, yaw * 0.3f);
            Turn(Bone("Spine1"), Vector3.up, yaw * 0.35f);
            Turn(Bone("Spine2"), Vector3.up, yaw * 0.35f);
        }

        /// <summary>Head back to the target; laid on the stock (bowed and tilted right) or bowed to watch the hands.</summary>
        private void Look(XbowKey key)
        {
            Turn(Bone("Neck"), Vector3.up, -key.Yaw * 0.3f);
            Turn(Bone("Head"), Vector3.up, -key.Yaw * 0.4f);
            float bow = key.Cheek * 14f + key.Down - key.Lean * 0.5f;
            Turn(Bone("Neck"), Vector3.right, bow * 0.4f);
            Turn(Bone("Head"), Vector3.right, bow * 0.6f);
            Turn(Bone("Head"), Vector3.forward, -key.Cheek * 18f);
        }

        public static void Turn(Transform bone, Vector3 axis, float degrees) =>
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;

        /// <summary>
        /// Turns a hand so its knuckles point along `knuckles` and its index finger sits on the `across` side of its
        /// little finger. `side` is "Left" or "Right".
        /// </summary>
        public void Frame(string side, Vector3 knuckles, Vector3 across)
        {
            Transform hand = Bone(side + "Hand");
            Vector3 along = Bone(side + "HandMiddle1").position - hand.position;
            Vector3 spread = Bone(side + "HandIndex1").position - Bone(side + "HandPinky1").position;
            Quaternion now = Quaternion.LookRotation(along, spread);
            Quaternion wanted = Quaternion.LookRotation(knuckles.normalized, across);
            hand.rotation = wanted * Quaternion.Inverse(now) * hand.rotation;
        }
    }
}
