using UnityEngine;

namespace Workshop.Crossbow
{
    /// <summary>
    /// The crossbow's and the quiver's points in their own Unity frames, from their model.py files (Blender (x, y, z) is
    /// Unity (-x, z, -y)). The crossbow's origin is the middle of the grip (where the right hand takes the stock's wrist),
    /// +Z points at the target and +Y is the groove side; the quiver's origin is the middle of its back at the mouth, +Z
    /// is the side it bulges out to and +Y its open mouth. The mod (EliteCreaturesPack Crossbow/XbowKit) finds the same
    /// points as empties in the kit. The left fist holds the crossbow round the fore-stock (<see cref="XbowGrip"/>). The
    /// bolt (ecp_xbow_bolt) has its nock end at its origin and its head along +Z, 0.57 m away (the game bone bolt's length), as every bolt slot expects.
    /// </summary>
    public static class XbowParts
    {
        public static readonly Vector3 TipA = new Vector3(0.352f, 0.024f, 0.398f);
        public static readonly Vector3 TipB = new Vector3(-0.352f, 0.024f, 0.398f);
        public static readonly Vector3 Rest = new Vector3(0f, 0.024f, 0.398f);       // the string's middle when let go
        public static readonly Vector3 Nut = new Vector3(0f, 0.036f, 0.100f);        // the string's middle when spanned
        public static readonly Vector3 Groove = new Vector3(0f, 0.031f, 0.100f);     // a laid bolt's nock end, head along +Z
        public static readonly Vector3 Muzzle = new Vector3(0f, 0.032f, 0.560f);
        public static readonly Vector3 Support = new Vector3(0f, -0.010f, 0.220f);   // the fore-stock's middle on the spine's bow, in the left fist
        public static readonly Vector3 Butt = new Vector3(0f, -0.020f, -0.150f);     // the back of the stock, at the shoulder

        /// <summary>
        /// Where the bolts stand in the quiver (quiver frame; the model's BOLT_SLOTS in Unity axes): nock ends 14 cm up out
        /// of its mouth, so the vanes stay above the rim, each shaft aimed at a point near the middle of the bottom (the
        /// case narrows from 11 x 7 cm at the mouth to 8 x 5 at the bottom, and straight or splayed bolts cut through its
        /// walls), and each turned about its own line so the vanes of neighbours interleave. The heads stop 2 cm off the
        /// bottom of the 31 cm case.
        /// </summary>
        public static readonly Vector3[] QuiverSlots =
        {
            new Vector3(0.026f, 0.14f, 0.024f), new Vector3(0f, 0.145f, 0.020f), new Vector3(-0.026f, 0.138f, 0.026f),
            new Vector3(0.013f, 0.142f, 0.048f), new Vector3(-0.014f, 0.136f, 0.050f),
        };

        private static readonly Vector3 MouthMiddle = new Vector3(0f, 0f, 0.035f);
        private static readonly Vector3 BottomMiddle = new Vector3(0f, -0.29f, 0.025f);
        private static readonly float[] QuiverRoll = { 0f, 40f, 80f, 20f, 60f };

        /// <summary>
        /// A quiver slot's rotation in the quiver's frame: +Z (the bolt's head) down into the case towards the bottom,
        /// +Y out of the case's front, turned by the slot's roll. The first slot, the bolt the hand takes, has no roll.
        /// </summary>
        public static Quaternion QuiverSlot(int i)
        {
            Vector3 nock = QuiverSlots[i];
            Vector3 across = new Vector3(nock.x, 0f, nock.z) - MouthMiddle;
            Vector3 head = BottomMiddle + new Vector3(across.x, 0f, across.z) * 0.4f;
            Vector3 down = (head - nock).normalized;
            return Quaternion.AngleAxis(QuiverRoll[i], down) * Quaternion.LookRotation(down, Vector3.forward);
        }
    }
}
