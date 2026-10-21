using System.Linq;
using UnityEngine;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// Where the eight rime plates sit, measured on the Troll in the idle's first frame. The mod breaks them from the
    /// highest index down, so the chest (0) goes last. Each plate is centred where a ray along its outward direction
    /// meets the skin between two bones, turned to the skin there, and fitted to it (<see cref="RimeFit"/>).
    /// </summary>
    public static class RimePlates
    {
        public const int Count = 8;

        public sealed class Spec
        {
            public string Mount, From, To;   // the bone the plate hangs from; its centre lies between From and To
            public float Along;              // 0 at From, 1 at To
            public Vector3 Offset;           // added to that point, in metres (world, idle)
            public Vector3 Outward;          // roughly which way the plate faces; the skin's own normal refines it
            public bool UpAlongLimb;         // the plate's length runs up the limb (towards From), else world up
            public float Width, Height;      // footprint on the skin, in metres
            public string[] Region;          // the skin the plate is fitted to: triangles of these bones only
        }

        public static string Name(int index) => "ecr_rime_plate_" + index;

        public static readonly Spec[] Specs =
        {
            Chest, Back, LeftShoulder, Mirror(LeftShoulder), LeftForearm, Mirror(LeftForearm), LeftThigh, Mirror(LeftThigh),
        };

        private static Spec Chest => new Spec
        {
            Mount = "Spine2", From = "Spine1", To = "Spine2", Along = 0.75f, Offset = Vector3.zero, Outward = new Vector3(0f, -0.1f, 1f),
            Width = 2.3f, Height = 1.8f, Region = new[] { "Spine0", "Spine1", "Spine2", "LeftShoulder", "RightShoulder" },
        };

        private static Spec Back => new Spec
        {
            Mount = "Spine2", From = "Spine1", To = "Spine2", Along = 0.7f, Offset = Vector3.zero, Outward = new Vector3(0f, 0.45f, -1f),
            Width = 2.6f, Height = 2.2f, Region = new[] { "Spine0", "Spine1", "Spine2", "LeftShoulder", "RightShoulder", "Head" },
        };

        private static Spec LeftShoulder => new Spec
        {
            Mount = "LeftArm", From = "LeftArm", To = "LeftForeArm", Along = 0.12f, Offset = Vector3.zero, Outward = new Vector3(-0.55f, 1f, 0f),
            UpAlongLimb = true, Width = 1.6f, Height = 1.7f, Region = new[] { "LeftShoulder", "LeftArm" },
        };

        private static Spec LeftForearm => new Spec
        {
            Mount = "LeftForeArm", From = "LeftForeArm", To = "LeftHand", Along = 0.45f, Offset = Vector3.zero, Outward = new Vector3(-1f, 0f, -0.2f),
            UpAlongLimb = true, Width = 0.95f, Height = 1.45f, Region = new[] { "LeftForeArm", "LeftHand" },
        };

        private static Spec LeftThigh => new Spec
        {
            Mount = "LeftUpLeg", From = "LeftUpLeg", To = "LeftLeg", Along = 0.45f, Offset = Vector3.zero, Outward = new Vector3(-1f, 0.1f, 0.45f),
            UpAlongLimb = true, Width = 1.1f, Height = 1.35f, Region = new[] { "LeftHip", "LeftUpLeg" },
        };

        /// <summary>The right side's plate: the left one's bones renamed, its directions reflected across the body's middle.</summary>
        private static Spec Mirror(Spec left)
        {
            string Right(string bone) => bone.Replace("Left", "Right");
            Vector3 Reflect(Vector3 v) => new Vector3(-v.x, v.y, v.z);
            return new Spec
            {
                Mount = Right(left.Mount), From = Right(left.From), To = Right(left.To), Along = left.Along, Offset = Reflect(left.Offset),
                Outward = Reflect(left.Outward), UpAlongLimb = left.UpAlongLimb, Width = left.Width, Height = left.Height,
                Region = left.Region.Select(Right).ToArray(),
            };
        }
    }
}
