using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>The single-number parts of a headsman pose, keyed on their own curves (<see cref="HeadsmanKeys"/>).</summary>
    public enum Ch
    {
        /// <summary>The body's turn about the vertical, degrees, positive to the right (clockwise seen from above).</summary>
        Yaw,
        /// <summary>The hips' offset in the turned body's frame (x right, y up, z forward), metres.</summary>
        HipsX, HipsY, HipsZ,
        /// <summary>The back: bowed forward, bent to the right side, the chest turned to the right (degrees).</summary>
        Lean, Side, Twist,
        /// <summary>The head within the neck's muscles: turned right, bowed down (degrees).</summary>
        HeadYaw, HeadPitch,
        /// <summary>0 elbows down and back, 1 up and out, as for a swing over the head.</summary>
        Elbows,
        /// <summary>1 fists closed, 0 fingers as in the idle, below 0 spread open.</summary>
        Fingers,
        /// <summary>0 the left fist on the axe's lower grip; towards 1 it lets go and goes to its free place.</summary>
        LeftFree,
    }

    /// <summary>A foot planted on the ground: its ankle in the world, and its turn from the idle's.</summary>
    public struct Plant
    {
        public Vector3 At;
        public float Turn;

        public Plant(Vector3 at, float turn)
        {
            At = at;
            Turn = turn;
        }
    }

    /// <summary>
    /// Where the axe is in a key, in the turned body's frame: the upper grip (the right fist's), the haft's direction from
    /// butt to head, and the way the cutting edge faces (made square to the haft).
    /// </summary>
    public struct AxeKey
    {
        public Vector3 Grip;
        public Vector3 Haft;
        public Vector3 Edge;

        public AxeKey(Vector3 grip, Vector3 haft, Vector3 edge)
        {
            Grip = grip;
            Haft = haft.normalized;
            Edge = Vector3.ProjectOnPlane(edge, Haft).normalized;
        }

        /// <summary>The axe's own turn: +Y along the haft, -X the way the edge faces.</summary>
        public Quaternion Turn => Quaternion.LookRotation(Vector3.Cross(-Edge, Haft), Haft);

        /// <summary>A key from its grip and turn.</summary>
        public static AxeKey From(Vector3 grip, Quaternion turn) => new AxeKey(grip, turn * Vector3.up, turn * Vector3.left);

        /// <summary>The axe turned about the vertical by `degrees` (positive to the right).</summary>
        public AxeKey Turned(float degrees)
        {
            Quaternion q = Quaternion.AngleAxis(degrees, Vector3.up);
            return new AxeKey(q * Grip, q * Haft, q * Edge);
        }
    }

    /// <summary>One sampled pose of a headsman clip, what <see cref="HeadsmanStance"/> puts on the Skeleton.</summary>
    public sealed class HeadsmanPose
    {
        public readonly float[] Values = new float[System.Enum.GetValues(typeof(Ch)).Length];
        public AxeKey Axe;

        /// <summary>Where the left hand goes when it lets go of the axe (body frame), weighed in by <see cref="Ch.LeftFree"/>.</summary>
        public Vector3 LeftHand;
        public Plant LeftFoot, RightFoot;
        public float LeftLift, RightLift;

        /// <summary>The creature's own turn (the rear strike's shuffle round): the root is turned so while authoring.</summary>
        public float RootYaw;

        public float this[Ch channel]
        {
            get => Values[(int)channel];
            set => Values[(int)channel] = value;
        }
    }
}
