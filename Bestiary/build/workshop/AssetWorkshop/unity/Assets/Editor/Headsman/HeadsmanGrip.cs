using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// How the fists hold the axe, measured on the Skeleton's closed fists. Each fist holds the haft the way a woodsman's
    /// does: through the curled fingers, running from the heel of the hand across the palm to the root of the index
    /// finger (a little towards the knuckles from straight across, <see cref="Diagonal"/>), the head beyond the thumb
    /// side and the edge facing the way the knuckles point. A grip frame has +Y along the haft towards the head and -X
    /// towards the edge, the same as the axe's own frame at a grip seat. The right fist carries the axe (the kit hangs it
    /// from RightHand); the left fist is put on the lower seat.
    /// </summary>
    public sealed class HeadsmanGrip
    {
        private const float Diagonal = 0.45f;
        private static readonly string[] Fingers = { "Index", "Middle", "Ring", "Pinky" };

        /// <summary>The grip frames in each hand's own frame (positions in the bone's local units).</summary>
        public Pose Right, Left;

        public void Measure(HeadsmanStance stance)
        {
            Right = Fist(stance, "Right");
            Left = Fist(stance, "Left");
            float scale = stance.Bone("RightHand").lossyScale.x;
            Log.Info($"grips (m from the wrist): right {Right.position * scale:F3} {Right.rotation.eulerAngles:F0}, left {Left.position * scale:F3} {Left.rotation.eulerAngles:F0}");
        }

        /// <summary>A grip seat's frame in the axe's frame: at the haft's centre, +Y along it.</summary>
        public static Pose Seat(float height) =>
            new Pose(HeadsmanAxe.Spine(height), Quaternion.FromToRotation(Vector3.up, HeadsmanAxe.Along(height)));

        /// <summary>The axe's world pose for a key (its upper seat at the key's grip, turned to its haft and edge).</summary>
        public static Pose Axe(AxeKey key)
        {
            Quaternion frame = key.Turn;
            Pose seat = Seat(HeadsmanAxe.UpperHeight);
            Quaternion rotation = frame * Quaternion.Inverse(seat.rotation);
            return new Pose(key.Grip - rotation * seat.position, rotation);
        }

        /// <summary>A seat of the axe in the world.</summary>
        public static Pose SeatOn(Pose axe, float height)
        {
            Pose seat = Seat(height);
            return new Pose(axe.position + axe.rotation * seat.position, axe.rotation * seat.rotation);
        }

        /// <summary>The hand bone's world pose that puts its grip frame (`inHand`) on `grip`.</summary>
        public static Pose HandFor(Transform hand, Pose inHand, Pose grip)
        {
            Quaternion rotation = grip.rotation * Quaternion.Inverse(inHand.rotation);
            return new Pose(grip.position - rotation * Vector3.Scale(inHand.position, hand.lossyScale), rotation);
        }

        /// <summary>The axe as the right fist holds it now (what the kit hangs from RightHand).</summary>
        public Pose AxeIn(Transform rightHand)
        {
            var grip = new Pose(rightHand.TransformPoint(Right.position), rightHand.rotation * Right.rotation);
            Pose seat = Seat(HeadsmanAxe.UpperHeight);
            Quaternion rotation = grip.rotation * Quaternion.Inverse(seat.rotation);
            return new Pose(grip.position - rotation * seat.position, rotation);
        }

        /// <summary>The grip frame of a closed fist, in its hand's frame: the curl's middle, turned as the class says.</summary>
        private static Pose Fist(HeadsmanStance stance, string side)
        {
            Transform hand = stance.Bone(side + "Hand");
            Vector3 across = (stance.Bone(side + "HandIndex1").position - stance.Bone(side + "HandPinky1").position).normalized;
            Vector3 along = (stance.Bone(side + "HandMiddle1").position - hand.position).normalized;
            Vector3 haft = (across + Diagonal * along).normalized;
            Vector3 edge = Vector3.ProjectOnPlane(along, haft).normalized;
            Vector3 centre = Vector3.zero;
            foreach (string finger in Fingers)
                centre += Vector3.Lerp(stance.Bone(side + "Hand" + finger + "1").position, stance.Bone(side + "Hand" + finger + "3_end").position, 0.5f) / Fingers.Length;
            Quaternion frame = Quaternion.LookRotation(Vector3.Cross(-edge, haft), haft);
            return new Pose(hand.InverseTransformPoint(centre), Quaternion.Inverse(hand.rotation) * frame);
        }
    }
}
