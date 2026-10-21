using UnityEngine;

namespace Workshop.Crossbow
{
    /// <summary>
    /// How the hands hold things, and where the quiver hangs, measured on the Skeleton.
    ///
    /// The crossbow rides the left fist, as the archer's bow does: the fore-stock runs through the fist along the line
    /// from the little finger to the index finger (forward, the index side towards the prod) and the groove faces the
    /// palm's side, so under the aimed crossbow the palm is up and the fingers close round the stock.
    /// <see cref="Bow"/> is the crossbow in LeftHand's own frame, measured once on the idle's first frame; the kit hangs it
    /// there.
    ///
    /// The right hand's pinch is the point between its curled index and middle fingertips, in RightHand's frame, with a
    /// rotation: the bolt in the hand has its nock end there and its head along the pinch's +Z, and the string rides it
    /// while the hand spans. It is guessed from the idle's fingers first, then measured on the clip as the Animator
    /// plays it (<see cref="XbowBuild"/> authors twice); the kit's ecp_xbow_pinch empty is exactly this. The quiver hangs
    /// from Hips on the right hip, measured on the idle's first frame. The low ready the carry clips hold the crossbow
    /// at, <see cref="Carry"/>, has its place in Spine2's frame, so it rides the chest through the game's idle, walk and
    /// run, and its aim in the creature's own frame, so it points the same way however the walk hunches the chest.
    /// </summary>
    public sealed class XbowGrip
    {
        private static readonly string[] Knuckles = { "Index1", "Index2", "Middle1", "Middle2", "Ring1", "Ring2", "Pinky1", "Pinky2" };

        public Pose Bow;
        public Pose Carry;
        public Vector3 PinchOffset;
        public Quaternion PinchRotation = Quaternion.identity;
        public Pose Quiver;

        /// <summary>The crossbow in the world, from the left hand as it is now.</summary>
        public Pose BowIn(XbowStance stance)
        {
            Transform hand = stance.Bone("LeftHand");
            return new Pose(hand.TransformPoint(Bow.position), hand.rotation * Bow.rotation);
        }

        /// <summary>The low ready in the world: where the chest has it now, aimed as the creature faces (the skeleton's root is the world's here).</summary>
        public Pose CarryIn(XbowStance stance) => new Pose(stance.Bone("Spine2").TransformPoint(Carry.position), Carry.rotation);

        /// <summary>Measures <see cref="Carry"/>: the low ready (<see cref="XbowStance.LowReady"/>) placed in the idle's chest frame.</summary>
        public void MeasureCarry(XbowStance stance)
        {
            Pose ready = XbowStance.LowReady;
            Carry = new Pose(stance.Bone("Spine2").InverseTransformPoint(ready.position), ready.rotation);
        }

        /// <summary>The left hand's world rotation and position that put the crossbow at `bow`.</summary>
        public Pose HandFor(XbowStance stance, Pose bow)
        {
            Transform hand = stance.Bone("LeftHand");
            Quaternion rotation = bow.rotation * Quaternion.Inverse(Bow.rotation);
            Vector3 offset = rotation * Vector3.Scale(Bow.position, hand.lossyScale);
            return new Pose(bow.position - offset, rotation);
        }

        /// <summary>
        /// Measures <see cref="Bow"/> on the idle's left hand: the fist's middle is among its finger joints, a little
        /// towards the palm; the stock's fore-stock middle (<see cref="XbowParts.Support"/>) goes there.
        /// </summary>
        public void MeasureBow(XbowStance stance)
        {
            Transform hand = stance.Bone("LeftHand");
            Vector3 fist = Vector3.zero;
            foreach (string joint in Knuckles)
                fist += stance.Bone("LeftHand" + joint).position / Knuckles.Length;
            Vector3 along = stance.Bone("LeftHandMiddle1").position - hand.position;
            Vector3 across = stance.Bone("LeftHandIndex1").position - stance.Bone("LeftHandPinky1").position;
            Vector3 palm = Vector3.Cross(across, along).normalized;
            Quaternion rotation = Quaternion.LookRotation(across, palm);
            fist += palm * 0.012f;
            Vector3 origin = fist - rotation * XbowParts.Support;
            Bow = new Pose(hand.InverseTransformPoint(origin), Quaternion.Inverse(hand.rotation) * rotation);
        }

        /// <summary>On the right hip, its back against the pelvis a little behind the hip joint, bulging out to the right, the mouth tipped forward.</summary>
        public static Pose QuiverOn(GameObject skeleton)
        {
            Vector3 hips = XbowReference.Bone(skeleton, "Hips").position;
            return new Pose(hips + new Vector3(0.19f, 0.13f, -0.06f),
                Quaternion.LookRotation(new Vector3(1f, 0f, 0.22f), new Vector3(-0.12f, 1f, 0.3f)));
        }

        /// <summary>A quiver slot in the world.</summary>
        public Pose Slot(int i) =>
            new Pose(XbowStance.At(Quiver, XbowParts.QuiverSlots[i]), Quiver.rotation * XbowParts.QuiverSlot(i));

        /// <summary>The first guess: between the index and middle fingertips of the idle's right hand, along the fingers.</summary>
        public void GuessPinch(XbowStance stance)
        {
            Transform hand = stance.Bone("RightHand");
            Vector3 along = stance.Bone("RightHandMiddle1").position - hand.position;
            Vector3 across = stance.Bone("RightHandIndex1").position - stance.Bone("RightHandPinky1").position;
            PinchOffset = hand.InverseTransformPoint(Tips(stance));
            PinchRotation = Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(along, Vector3.Cross(along, across));
        }

        /// <summary>
        /// Measured on the played clip at the lay, where the bolt in the fingers lies exactly in the groove: the pinch is
        /// between the fingertips as the clip curls them, turned as the groove.
        /// </summary>
        public void MeasurePinch(XbowStance stance)
        {
            Transform hand = stance.Bone("RightHand");
            PinchOffset = hand.InverseTransformPoint(Tips(stance));
            PinchRotation = Quaternion.Inverse(hand.rotation) * BowIn(stance).rotation;
        }

        /// <summary>The pinch in the world, from the right hand as it is now.</summary>
        public Pose Pinch(XbowStance stance)
        {
            Transform hand = stance.Bone("RightHand");
            return new Pose(hand.TransformPoint(PinchOffset), hand.rotation * PinchRotation);
        }

        private static Vector3 Tips(XbowStance stance) =>
            Vector3.Lerp(stance.Bone("RightHandIndex3_end").position, stance.Bone("RightHandMiddle3_end").position, 0.5f);
    }
}
