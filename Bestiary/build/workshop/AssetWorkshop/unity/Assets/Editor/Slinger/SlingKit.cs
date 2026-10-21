using UnityEditor;
using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// Builds ecr_slinger_kit.prefab, everything the slinger wears, grouped under mounts named after the Greydwarf's
    /// bones. The mod parents each mount to the bone of that name (local transform zero), so each part keeps the offset
    /// measured here on the same skeleton at the game's own scale:
    ///
    ///   l_hand/ecr_sling_slingshot   the slingshot in the fist, upright and facing the target at full draw, with
    ///                                empties for the band anchors, the pouch's resting place and the muzzle (where the
    ///                                stone is launched), the two bands and the pouch (with an empty for its stone)
    ///   r_hand/ecr_sling_pinch       between the drawing fingertips: the pouch follows it while drawn, and the stone
    ///                                taken from the satchel sits in its empty ecr_sling_hand_stone until loaded
    ///   spine2/ecr_slinger_satchel   the bag of stones high on the back, like a quiver: the one place the Greydwarf's
    ///                                long arms and legs never reach in its idle, walk or run (SatchelClearance measures it)
    /// </summary>
    public static class SlingKit
    {
        public const string Name = "ecr_slinger_kit";
        private const float SlingshotSize = 1.8f;
        private const float SatchelSize = 2.4f;

        // Blender (x, y, z) -> Unity (-x, z, -y); see the slingshot's and the pouch's model.py.
        private static readonly Vector3 AnchorA = new Vector3(0.052f, 0.186f, -0.004f);
        private static readonly Vector3 AnchorB = new Vector3(-0.050f, 0.191f, -0.005f);
        public static readonly Vector3 Rest = new Vector3(0.0f, 0.125f, -0.075f);
        private static readonly Vector3 Muzzle = new Vector3(0.0f, 0.19f, 0.03f);

        public static string Build(GameObject greydwarf, AnimationClip shot, AnimationClip idle, string folder)
        {
            var kit = new GameObject(Name);
            shot.SampleAnimation(greydwarf, SlingClip.FullDraw);
            Slingshot(Mount(kit, "l_hand"), greydwarf, folder);
            Transform pinch = Empty("ecr_sling_pinch", Mount(kit, "r_hand"));
            Place(pinch, Mount(kit, "r_hand"), greydwarf, Pinch(greydwarf), Quaternion.identity, 1f);
            Empty("ecr_sling_hand_stone", pinch);
            idle.SampleAnimation(greydwarf, 0f);
            Satchel(Mount(kit, "spine2"), greydwarf, folder);
            string path = folder + "/" + Name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(kit, path);
            Object.DestroyImmediate(kit);
            Log.Info("kit " + path);
            return path;
        }

        private static void Slingshot(Transform mount, GameObject greydwarf, string folder)
        {
            var slingshot = Part(folder, "ecr_slingshot", "ecr_sling_slingshot", mount);
            Place(slingshot.transform, mount, greydwarf, Grip(greydwarf), Quaternion.identity, SlingshotSize);
            Transform s = slingshot.transform;
            Empty("ecr_sling_anchor_a", s).localPosition = AnchorA;
            Empty("ecr_sling_anchor_b", s).localPosition = AnchorB;
            Empty("ecr_sling_rest", s).localPosition = Rest;
            Empty("ecr_sling_muzzle", s).localPosition = Muzzle;
            Part(folder, "ecr_sling_band", "ecr_sling_band_a", s);
            Part(folder, "ecr_sling_band", "ecr_sling_band_b", s);
            var pouch = Part(folder, "ecr_sling_pouch", "ecr_sling_pouch", s);
            pouch.transform.localPosition = Rest;
            Empty("ecr_sling_stone", pouch.transform).localPosition = new Vector3(0f, 0f, 0.012f);
        }

        private static void Satchel(Transform mount, GameObject greydwarf, string folder)
        {
            var satchel = Part(folder, "ecr_slinger_satchel", "ecr_slinger_satchel", mount);
            Place(satchel.transform, mount, greydwarf, SatchelAt(greydwarf), SatchelFacing, SatchelSize);
        }

        /// <summary>
        /// On the upper back in the idle's first frame, a little left of the spine, pressed against the body and facing
        /// back: 8 cm clear of every limb through the idle, walk and run.
        /// </summary>
        private static Vector3 SatchelAt(GameObject greydwarf) =>
            SlingBody.Surface(greydwarf, SlingerReference.Bone(greydwarf, "spine2").position + new Vector3(-0.08f, 0.2f, 0f), Vector3.back);

        private static readonly Quaternion SatchelFacing = Quaternion.LookRotation(Vector3.back, Vector3.up);

        /// <summary>The middle of the satchel's mouth (the model's top, a little in front of its back), for the reach.</summary>
        public static Vector3 SatchelMouth(GameObject greydwarf) =>
            SatchelAt(greydwarf) + SatchelFacing * (new Vector3(0f, 0.054f, 0.035f) * SatchelSize);

        /// <summary>Middle of the closed fist: the stick runs through the curled fingers.</summary>
        private static Vector3 Grip(GameObject greydwarf)
        {
            string[] joints = { "l_index1", "l_index2", "l_middle1", "l_middle2", "l_pinky1", "l_pinky2" };
            Vector3 sum = Vector3.zero;
            foreach (string joint in joints)
                sum += SlingerReference.Bone(greydwarf, joint).position;
            return sum / joints.Length;
        }

        private static Vector3 Pinch(GameObject greydwarf) =>
            Vector3.Lerp(SlingerReference.Bone(greydwarf, "r_index2_end").position, SlingerReference.Bone(greydwarf, "r_middle2_end").position, 0.5f);

        /// <summary>Gives a part under a mount the world pose it has on the posed skeleton, as an offset from the bone.</summary>
        private static void Place(Transform part, Transform mount, GameObject greydwarf, Vector3 position, Quaternion rotation, float size)
        {
            Transform bone = SlingerReference.Bone(greydwarf, mount.name);
            part.localPosition = bone.InverseTransformPoint(position);
            part.localRotation = Quaternion.Inverse(bone.rotation) * rotation;
            part.localScale = Vector3.one * (size / bone.lossyScale.x);
        }

        private static Transform Mount(GameObject kit, string bone)
        {
            Transform mount = kit.transform.Find(bone);
            return mount != null ? mount : Empty(bone, kit.transform);
        }

        private static GameObject Part(string folder, string asset, string name, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{asset}/{asset}.prefab");
            var part = Object.Instantiate(prefab, parent, false);
            part.name = name;
            return part;
        }

        private static Transform Empty(string name, Transform parent)
        {
            var empty = new GameObject(name).transform;
            empty.SetParent(parent, false);
            return empty;
        }
    }
}
