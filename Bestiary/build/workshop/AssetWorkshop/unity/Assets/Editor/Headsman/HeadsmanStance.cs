using UnityEngine;
using Workshop.Crossbow;
using Workshop.Slinger;

namespace Workshop.Headsman
{
    /// <summary>
    /// Puts a <see cref="HeadsmanPose"/> on the Skeleton, starting from the first frame of its idle played through its
    /// own Animator: turns the root (the rear strike's shuffle), moves and turns the hips, bows, bends and twists the
    /// back, turns the head, sets each foot on its plant (two-bone IK, knee over the toes), then places the axe and
    /// brings both fists onto its seats (two-bone IK, elbow towards a hint that rises for swings over the head, the
    /// collarbone lifted when a hand goes high). Unity axes: the creature stands at the origin facing +Z. Only bones
    /// the avatar maps are moved, since the pose is read back as muscles.
    /// </summary>
    public sealed class HeadsmanStance
    {
        private readonly GameObject skeleton;
        private readonly XbowPoser poser;
        private readonly HeadsmanGrip grip;
        private Quaternion leftFootRest, rightFootRest, leftHandRest;
        private Vector3 leftToe, rightToe;
        private readonly System.Collections.Generic.Dictionary<string, Vector3> wanted = new System.Collections.Generic.Dictionary<string, Vector3>();
        private readonly System.Collections.Generic.Dictionary<string, float> swivels = new System.Collections.Generic.Dictionary<string, float>();

        public HeadsmanStance(GameObject skeleton, XbowPoser poser, HeadsmanGrip grip)
        {
            this.skeleton = skeleton;
            this.poser = poser;
            this.grip = grip;
            Rest();
            (leftFootRest, leftToe) = FootRest("Left");
            (rightFootRest, rightToe) = FootRest("Right");
            leftHandRest = Bone("LeftHand").rotation;
        }

        public HeadsmanGrip Grip => grip;

        /// <summary>The wrists' neutrals; once set, each arm turns its elbow to where its wrist bends least.</summary>
        public HeadsmanWrist LeftWrist, RightWrist;

        public Transform Bone(string name) => XbowReference.Bone(skeleton, name);

        /// <summary>The idle's first frame, the root unturned.</summary>
        public void Rest()
        {
            skeleton.transform.rotation = Quaternion.identity;
            poser.Pose("Idle", 0f);
        }

        public void Apply(HeadsmanPose pose)
        {
            skeleton.transform.rotation = Quaternion.AngleAxis(pose.RootYaw, Vector3.up);
            poser.Pose("Idle", 0f);
            float turn = pose.RootYaw + pose[Ch.Yaw];
            Body(pose, turn);
            Leg("Left", pose.LeftFoot, pose.LeftLift, leftFootRest, leftToe);
            Leg("Right", pose.RightFoot, pose.RightLift, rightFootRest, rightToe);
            Hold(pose.Axe.Turned(turn), pose[Ch.Elbows], turn, (pose[Ch.LeftFree], pose.LeftHand));
        }

        /// <summary>Whatever frame of the game's idle, walk or run is on the skeleton now, both fists take the ready hold.</summary>
        public void Carry() => Hold(HeadsmanRest.ReadyOn(Bone("Spine2")), 0f, 0f, (0f, Vector3.zero));

        /// <summary>The axe in the world where the right fist holds it now.</summary>
        public Pose Axe => grip.AxeIn(Bone("RightHand"));

        private void Body(HeadsmanPose pose, float turn)
        {
            Quaternion yaw = Quaternion.AngleAxis(turn, Vector3.up);
            Vector3 right = yaw * Vector3.right, forward = yaw * Vector3.forward;
            Transform hips = Bone("Hips");
            hips.position += yaw * new Vector3(pose[Ch.HipsX], pose[Ch.HipsY], pose[Ch.HipsZ]);
            Turn(hips, Vector3.up, pose[Ch.Yaw]);
            Turn(hips, right, pose[Ch.Lean] * 0.2f);
            string[] spine = { "Spine", "Spine1", "Spine2" };
            float[] bow = { 0.3f, 0.3f, 0.2f }, twist = { 0.35f, 0.4f, 0.25f }, side = { 0.4f, 0.35f, 0.25f };
            for (int i = 0; i < spine.Length; i++)
            {
                Transform bone = Bone(spine[i]);
                Turn(bone, right, pose[Ch.Lean] * bow[i]);
                Turn(bone, forward, -pose[Ch.Side] * side[i]);
                Turn(bone, Vector3.up, pose[Ch.Twist] * twist[i]);
            }
            Vector3 chestRight = Quaternion.AngleAxis(pose[Ch.Twist], Vector3.up) * right;
            foreach (var (bone, share) in new[] { ("Neck", 0.4f), ("Head", 0.6f) })
            {
                Turn(Bone(bone), Vector3.up, pose[Ch.HeadYaw] * share);
                Turn(Bone(bone), chestRight, pose[Ch.HeadPitch] * share);
            }
        }

        private void Leg(string side, Plant plant, float lift, Quaternion footRest, Vector3 toeRest)
        {
            Quaternion turn = Quaternion.AngleAxis(plant.Turn, Vector3.up);
            Transform upper = Bone(side + "UpLeg"), knee = Bone(side + "Leg"), foot = Bone(side + "Foot");
            Vector3 target = plant.At + Vector3.up * lift;
            Vector3 hint = Vector3.Lerp(upper.position, target, 0.5f) + turn * toeRest * 0.6f;
            TwoBoneIk.Solve(upper, knee, foot, target, hint);
            foot.rotation = Quaternion.AngleAxis(lift * 120f, turn * Vector3.right) * turn * footRest;
        }

        /// <summary>
        /// The axe placed by the key (world), the right fist on the upper seat and the left on the lower. A key the arms
        /// cannot reach is pulled towards the shoulders until both fists can (<see cref="Pulled"/> says how far).
        /// </summary>
        private void Hold(AxeKey key, float elbows, float turn, (float weight, Vector3 at) free)
        {
            Pose axe = HeadsmanGrip.Axe(key);
            Vector3 pull = Pull(axe, free.weight < 0.5f);
            Pulled = pull.magnitude;
            axe.position += pull;
            Pose right = HeadsmanGrip.HandFor(Bone("RightHand"), grip.Right, HeadsmanGrip.SeatOn(axe, HeadsmanAxe.UpperHeight));
            Pose left = HeadsmanGrip.HandFor(Bone("LeftHand"), grip.Left, HeadsmanGrip.SeatOn(axe, HeadsmanAxe.LowerHeight));
            if (free.weight > 0f)
                left = Blend(left, FreeLeft(free.at, turn), free.weight);
            Arm("Right", right, elbows, turn);
            Arm("Left", left, elbows, turn);
        }

        /// <summary>The left hand off the axe: at `at` in the turned body's frame, turned as it hangs in the idle.</summary>
        private Pose FreeLeft(Vector3 at, float turn)
        {
            Quaternion yaw = Quaternion.AngleAxis(turn, Vector3.up);
            return new Pose(yaw * at, yaw * leftHandRest);
        }

        private static Pose Blend(Pose a, Pose b, float t) =>
            new Pose(Vector3.Lerp(a.position, b.position, t), Quaternion.Slerp(a.rotation, b.rotation, t));

        /// <summary>How far the last pose's axe had to be pulled in for the fists to reach it.</summary>
        public float Pulled { get; private set; }

        /// <summary>Where the shoulders' middle is, in the turned body's frame (for fitting keys).</summary>
        public Vector3 Shoulders(float turn) =>
            Quaternion.AngleAxis(-turn, Vector3.up) * Vector3.Lerp(Bone("LeftArm").position, Bone("RightArm").position, 0.5f);

        private Vector3 Pull(Pose axe, bool bothHands)
        {
            Vector3 upper = HeadsmanGrip.SeatOn(axe, HeadsmanAxe.UpperHeight).position;
            Vector3 lower = HeadsmanGrip.SeatOn(axe, HeadsmanAxe.LowerHeight).position;
            Vector3 towards = (Shoulders(0f) - Vector3.Lerp(upper, lower, 0.5f)).normalized;
            for (int cm = 0; cm < 80; cm++)
            {
                Vector3 shift = towards * (cm * 0.01f);
                if (Reaches("Right", upper + shift) && (!bothHands || Reaches("Left", lower + shift)))
                    return shift;
            }
            return towards * 0.8f;
        }

        /// <summary>Whether the fist can put its grip on `seat`: the shoulder to the grip within the straight arm and fist.</summary>
        private bool Reaches(string side, Vector3 seat)
        {
            Transform upper = Bone(side + "Arm"), fore = Bone(side + "ForeArm"), hand = Bone(side + "Hand");
            float arm = Vector3.Distance(upper.position, fore.position) + Vector3.Distance(fore.position, hand.position);
            float fist = (side == "Right" ? grip.Right : grip.Left).position.magnitude * hand.lossyScale.x;
            return Vector3.Distance(upper.position, seat) <= arm + fist * 0.8f - 0.01f;
        }

        private void Arm(string side, Pose target, float elbows, float turn)
        {
            float s = side == "Right" ? 1f : -1f;
            Transform upper = Bone(side + "Arm");
            Quaternion yaw = Quaternion.AngleAxis(turn, Vector3.up);
            float lift = Mathf.Clamp01((target.position.y - upper.position.y) / 0.45f);
            Turn(Bone(side + "Shoulder"), yaw * Vector3.forward, s * 16f * lift);
            Vector3 low = new Vector3(0.35f * s, -0.6f, -0.35f), high = new Vector3(0.6f * s, 0.25f, 0.35f);
            Vector3 hint = yaw * Vector3.Lerp(low, high, elbows);
            float swivel = BestSwivel(side, target, hint);
            Reach(side, target, upper.position + Quaternion.AngleAxis(swivel, target.position - upper.position) * hint);
            wanted[side] = target.position;
        }

        /// <summary>Logs each elbow's chosen turn (for finding a jump).</summary>
        public bool Trace;

        /// <summary>Starts a new clip: the elbows' turns no longer follow the last frame's.</summary>
        public void NewClip() => swivels.Clear();

        /// <summary>
        /// The turn of the elbow about the shoulder-to-hand line, from the hint, where the wrist bends least (a little
        /// in favour of the hint itself; within 12 degrees of the last frame's turn, so the elbow never jumps between
        /// frames: a jump there sends the arm through a wild path when the clip interpolates the muscles);
        /// zero until the wrists are measured.
        /// </summary>
        private float BestSwivel(string side, Pose target, Vector3 hint)
        {
            HeadsmanWrist wrist = side == "Right" ? RightWrist : LeftWrist;
            if (wrist == null)
                return 0f;
            Transform upper = Bone(side + "Arm"), fore = Bone(side + "ForeArm");
            (Quaternion a, Quaternion b) = (upper.rotation, fore.rotation);
            bool follow = swivels.TryGetValue(side, out float last);
            float best = 0f, least = float.MaxValue;
            (float from, float to) = follow ? (Mathf.Max(-170f, last - 12f), Mathf.Min(170f, last + 12f)) : (-160f, 160f);
            for (float swivel = from; swivel <= to; swivel += follow ? 2f : 5f)
            {
                (upper.rotation, fore.rotation) = (a, b);
                Reach(side, target, upper.position + Quaternion.AngleAxis(swivel, target.position - upper.position) * hint);
                float cost = wrist.Cost(this) + Mathf.Abs(swivel) / 160f * 0.5f + (follow ? Mathf.Abs(swivel - last) / 20f : 0f);
                if (cost < least)
                    (least, best) = (cost, swivel);
            }
            (upper.rotation, fore.rotation) = (a, b);
            if (Trace)
                Log.Info($"  swivel {side} {best:F0} (last {(follow ? last.ToString("F0") : "-")}), cost {least:F2}, reach {Vector3.Distance(upper.position, target.position):F3}");
            swivels[side] = best;
            return best;
        }

        private void Reach(string side, Pose target, Vector3 hint)
        {
            Transform hand = Bone(side + "Hand");
            TwoBoneIk.Solve(Bone(side + "Arm"), Bone(side + "ForeArm"), hand, target.position, hint);
            hand.rotation = target.rotation;
        }

        /// <summary>How far the hand fell short of where the last pose wanted it (out of reach).</summary>
        public float Short(string side) => wanted.TryGetValue(side, out Vector3 at) ? Vector3.Distance(at, Bone(side + "Hand").position) : 0f;

        /// <summary>A foot's world rotation on the idle's first frame, and the way its toes point, level.</summary>
        private (Quaternion, Vector3) FootRest(string side)
        {
            Transform foot = Bone(side + "Foot");
            Vector3 toe = Vector3.ProjectOnPlane(Bone(side + "ToeBase").position - foot.position, Vector3.up).normalized;
            return (foot.rotation, toe);
        }

        public static void Turn(Transform bone, Vector3 axis, float degrees) =>
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
    }
}
