using UnityEngine;
using Workshop.Slinger;

namespace Workshop.Crossbow
{
    /// <summary>
    /// The right hand's part of a key, done after the crossbow is placed: round the stock's wrist while it aims (a pistol
    /// hold, knuckles forward and up, the index finger on top); over the stock, fingers forward and palm down, to hook
    /// the let-go string, hold it in the nut and lay a bolt in the groove (the pinch, <see cref="XbowGrip"/>, lands on
    /// the string's middle, the nut and the groove's nock end); into the quiver on the right hip, turned so the bolt in
    /// the fingers lines up with the first slot's bolt, and out again along it. Each reach is two-bone IK with the elbow
    /// down and out to the right, then the hand turned.
    /// </summary>
    public sealed class XbowRightHand
    {
        private static readonly Vector3 Elbow = new Vector3(0.35f, -0.4f, -0.1f);
        private static readonly Vector3 QuiverElbow = new Vector3(0.3f, -0.15f, -0.35f);   // out and back, as into a holster
        private static readonly Vector3 WristHold = new Vector3(0.018f, -0.012f, -0.01f);   // the fist's middle, off the grip

        private readonly XbowStance stance;
        private readonly XbowGrip grip;

        public XbowRightHand(XbowStance stance, XbowGrip grip)
        {
            this.stance = stance;
            this.grip = grip;
        }

        public void Reach(Hand task, Pose bow)
        {
            switch (task)
            {
                case Hand.Wrist: Wrist(bow); break;
                case Hand.StringGrab: OnStock(bow, XbowParts.Rest + new Vector3(0f, 0.012f, -0.012f), 35f); break;
                case Hand.Spanned: OnStock(bow, XbowParts.Nut + new Vector3(0f, 0.01f, -0.012f), 15f); break;
                case Hand.Lay: OnStock(bow, XbowParts.Groove, 0f); break;
                case Hand.Pat: OnStock(bow, XbowParts.Groove + new Vector3(-0.01f, 0.04f, -0.035f), -10f); break;
                case Hand.ToQuiver: Quiver(0.2f, 0.05f); break;
                case Hand.InQuiver: Quiver(0f, 0f); break;
                case Hand.BoltOut: Quiver(0.19f, 0f); break;
            }
        }

        /// <summary>Round the stock's wrist: the fist's middle just right of and under the grip, knuckles forward and up.</summary>
        private void Wrist(Pose bow)
        {
            Vector3 knuckles = bow.rotation * new Vector3(0f, 0.55f, 1f), across = bow.rotation * new Vector3(0f, 1f, -0.4f);
            stance.Frame("Right", knuckles, across);
            Vector3 fist = Fist() - stance.Bone("RightHand").position;
            Solve(XbowStance.At(bow, WristHold) - fist);
            stance.Frame("Right", knuckles, across);
        }

        /// <summary>
        /// Over the stock, fingers forward (bent `hook` degrees down, over the string) and palm down, the index finger on
        /// the left: the pinch on the given point of the crossbow.
        /// </summary>
        private void OnStock(Pose bow, Vector3 local, float hook)
        {
            Vector3 fingers = Quaternion.AngleAxis(hook, bow.rotation * Vector3.right) * (bow.rotation * Vector3.forward);
            Vector3 across = bow.rotation * Vector3.left;
            stance.Frame("Right", fingers, across);
            PinchTo(XbowStance.At(bow, local));
            stance.Frame("Right", fingers, across);
        }

        /// <summary>Turned so the bolt in the fingers lines up with the first slot's, the pinch `lift` metres up its line.</summary>
        private void Quiver(float lift, float outward)
        {
            Pose slot = grip.Slot(0);
            Transform hand = stance.Bone("RightHand");
            Quaternion turned = slot.rotation * Quaternion.Inverse(grip.PinchRotation);
            hand.rotation = turned;
            PinchTo(slot.position - slot.forward * lift + slot.up * outward, QuiverElbow);
            hand.rotation = turned;
        }

        private void PinchTo(Vector3 point) => PinchTo(point, Elbow);

        private void PinchTo(Vector3 point, Vector3 elbow)
        {
            Transform hand = stance.Bone("RightHand");
            Solve(point - (hand.TransformPoint(grip.PinchOffset) - hand.position), elbow);
        }

        /// <summary>The middle of the right fist: among its finger joints.</summary>
        private Vector3 Fist()
        {
            Vector3 sum = Vector3.zero;
            foreach (string joint in new[] { "Index1", "Index2", "Middle1", "Middle2", "Pinky1", "Pinky2" })
                sum += stance.Bone("RightHand" + joint).position;
            return sum / 6f;
        }

        private void Solve(Vector3 wrist, Vector3? elbow = null)
        {
            Transform shoulder = stance.Bone("RightArm"), hand = stance.Bone("RightHand");
            Quaternion turn = hand.rotation;
            TwoBoneIk.Solve(shoulder, stance.Bone("RightForeArm"), hand, wrist, shoulder.position + (elbow ?? Elbow));
            hand.rotation = turn;
        }
    }
}
