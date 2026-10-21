using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// What every headsman clip starts from, measured on the first frame of the Skeleton's idle (<see cref="Measure"/>):
    /// its ankles and the ready hold. The ready hold is the axe held across the front, the head up by the right shoulder
    /// and the edge towards the enemy, the butt low by the left thigh; every attack starts and ends on it, and the carry
    /// clips hold it riding the chest, so attacks blend in and out without the axe jumping.
    /// </summary>
    public static class HeadsmanRest
    {
        public static Vector3 LeftAnkle { get; private set; }
        public static Vector3 RightAnkle { get; private set; }

        /// <summary>The ready hold in the body frame (the idle's first frame).</summary>
        public static readonly AxeKey Ready = new AxeKey(new Vector3(0.12f, 1.30f, 0.36f),new Vector3(0.5f, 0.84f, 0.2f), new Vector3(0.1f, 0f, 1f));

        /// <summary>The ready hold's grip in Spine2's frame, so the carry clips keep it on the chest.</summary>
        public static Vector3 ReadyOnChest { get; private set; }

        public static void Measure(HeadsmanStance stance)
        {
            stance.Rest();
            LeftAnkle = stance.Bone("LeftFoot").position;
            RightAnkle = stance.Bone("RightFoot").position;
            ReadyOnChest = stance.Bone("Spine2").InverseTransformPoint(Ready.Grip);
            Log.Info($"rest: ankles {LeftAnkle:F3} {RightAnkle:F3}, shoulders {stance.Bone("LeftArm").position:F3} {stance.Bone("RightArm").position:F3}");
        }

        /// <summary>
        /// The ready hold where the chest is now (the carry clips): its grip rides Spine2, its haft and edge stay as the
        /// body faces, since riding the chest's turn would tip the axe about in the hunched walk.
        /// </summary>
        public static AxeKey ReadyOn(Transform chest) => new AxeKey(chest.TransformPoint(ReadyOnChest), Ready.Haft, Ready.Edge);
    }
}
