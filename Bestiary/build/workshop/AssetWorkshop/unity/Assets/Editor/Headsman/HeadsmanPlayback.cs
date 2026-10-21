using UnityEditor;
using UnityEngine;
using Workshop.Crossbow;

namespace Workshop.Headsman
{
    /// <summary>
    /// The headsman as the mod will show it, for the previews: the axe hung from RightHand where the right fist holds it
    /// (as the mod's kit will hang it), and what the clips cannot do done in code at the clip's time: the creature's own
    /// turn (the rear strike's shuffle), the head turned past the neck's muscles about the neck's line, and the axe
    /// hidden from the throw until the new one is solid.
    /// </summary>
    public sealed class HeadsmanPlayback
    {
        public const string AxeName = "ecp_headsman_axe";
        private readonly GameObject skeleton;

        public HeadsmanPlayback(GameObject skeleton, HeadsmanGrip grip)
        {
            this.skeleton = skeleton;
            Axe = Hang(skeleton, grip, AxeName);
        }

        public GameObject Axe { get; }

        public Transform Bone(string name) => XbowReference.Bone(skeleton, name);

        /// <summary>A copy of the axe hung from RightHand as the right fist holds it.</summary>
        public static GameObject Hang(GameObject skeleton, HeadsmanGrip grip, string name)
        {
            Transform hand = XbowReference.Bone(skeleton, "RightHand");
            var axe = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HeadsmanAxe.Prefab));
            axe.name = name;
            Pose at = grip.AxeIn(hand);
            axe.transform.SetPositionAndRotation(at.position, at.rotation);
            axe.transform.localScale = Vector3.one * skeleton.transform.lossyScale.x;
            axe.transform.SetParent(hand, true);
            return axe;
        }

        /// <summary>After the Animator has posed the move's clip at `time`: the head spin and the axe's visibility.</summary>
        public void Dress(HeadsmanMove move, float time)
        {
            TurnTorso(move == null ? 0f : move.Torso(time));
            Spin(move == null ? 0f : move.Spin(time));
            Axe.SetActive(move == null || move.AxeShown(time));
        }

        /// <summary>
        /// Turns everything above the waist by `degrees` (to the right) about the upright line through the lower back,
        /// on top of the pose: a skeleton can wring itself right round, the hips and legs staying as they were.
        /// </summary>
        public void TurnTorso(float degrees)
        {
            if (Mathf.Abs(degrees) < 0.01f)
                return;
            Transform spine = Bone("Spine");
            spine.rotation = Quaternion.AngleAxis(degrees, Vector3.up) * spine.rotation;
        }

        /// <summary>
        /// Turns the head by `degrees` (to the right) on top of the pose, about the chest's up line (the Skeleton's neck
        /// leans far forward; turning about it would tip the skull over).
        /// </summary>
        public void Spin(float degrees)
        {
            if (Mathf.Abs(degrees) < 0.01f)
                return;
            Transform head = Bone("Head");
            Vector3 up = (Bone("Neck").position - Bone("Spine2").position).normalized;
            head.rotation = Quaternion.AngleAxis(degrees, up) * head.rotation;
        }
    }
}
