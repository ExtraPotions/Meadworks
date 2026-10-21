using UnityEditor;
using UnityEngine;

namespace Workshop.Crossbow
{
    /// <summary>
    /// Builds ecp_xbow_kit.prefab, everything the crossbowman wears, under mounts named after the Skeleton's bones. The
    /// mod parents each mount to the bone of that name (local transform zero), so each part keeps the offset measured
    /// here on the same skeleton at the game's own scale:
    ///
    ///   LeftHand/ecp_xbow_crossbow    in the fist round the fore-stock (<see cref="XbowGrip.Bow"/>), with empties
    ///                                 for the prod tips, the string's rest, the nut, the groove (a laid bolt's nock end)
    ///                                 and the muzzle (where the bolt leaves), and the two halves of the string,
    ///                                 ecp_xbow_string_a and _b, which the mod stretches, and the loaded bolt
    ///                                 ecp_xbow_bolt_groove
    ///   RightHand/ecp_xbow_pinch      between the right fingertips (<see cref="XbowGrip"/>): the bolt taken from the
    ///                                 quiver sits there (ecp_xbow_bolt_hand), and the string rides it while spanned
    ///   Hips/ecp_xbow_quiver          on the right hip, with slots ecp_xbow_quiver_bolt_0.._4 in its mouth, a bolt in
    ///                                 each (ecp_xbow_bolt_quiver_0.._4)
    /// </summary>
    public static class XbowKit
    {
        public const string Name = "ecp_xbow_kit";

        public static string Build(GameObject skeleton, XbowGrip grip, string folder)
        {
            var kit = new GameObject(Name);
            Crossbow(Mount(kit, "LeftHand"), skeleton, grip.Bow, folder);
            Transform pinch = Empty("ecp_xbow_pinch", Mount(kit, "RightHand"));
            pinch.localPosition = grip.PinchOffset;
            pinch.localRotation = grip.PinchRotation;
            XbowBolt.In(pinch, "ecp_xbow_bolt_hand", 1f / XbowReference.Bone(skeleton, "RightHand").lossyScale.x);
            Quiver(Mount(kit, "Hips"), skeleton, grip.Quiver, folder);
            string path = folder + "/" + Name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(kit, path);
            Object.DestroyImmediate(kit);
            Log.Info("kit " + path);
            return path;
        }

        private static void Crossbow(Transform mount, GameObject skeleton, Pose inFist, string folder)
        {
            GameObject crossbow = Part(folder, "ecp_xbow_crossbow", "ecp_xbow_crossbow", mount);
            Transform bone = XbowReference.Bone(skeleton, mount.name);
            crossbow.transform.localPosition = inFist.position;
            crossbow.transform.localRotation = inFist.rotation;
            crossbow.transform.localScale = Vector3.one / bone.lossyScale.x;
            Transform c = crossbow.transform;
            Empty("ecp_xbow_tip_a", c).localPosition = XbowParts.TipA;
            Empty("ecp_xbow_tip_b", c).localPosition = XbowParts.TipB;
            Empty("ecp_xbow_rest", c).localPosition = XbowParts.Rest;
            Empty("ecp_xbow_nut", c).localPosition = XbowParts.Nut;
            Transform groove = Empty("ecp_xbow_groove", c);
            groove.localPosition = XbowParts.Groove;
            XbowBolt.In(groove, "ecp_xbow_bolt_groove", 1f);
            Empty("ecp_xbow_muzzle", c).localPosition = XbowParts.Muzzle;
            Part(folder, "ecp_xbow_string", "ecp_xbow_string_a", c);
            Part(folder, "ecp_xbow_string", "ecp_xbow_string_b", c);
        }

        private static void Quiver(Transform mount, GameObject skeleton, Pose at, string folder)
        {
            GameObject quiver = Part(folder, "ecp_xbow_quiver", "ecp_xbow_quiver", mount);
            Transform bone = XbowReference.Bone(skeleton, mount.name);
            quiver.transform.localPosition = bone.InverseTransformPoint(at.position);
            quiver.transform.localRotation = Quaternion.Inverse(bone.rotation) * at.rotation;
            quiver.transform.localScale = Vector3.one / bone.lossyScale.x;
            for (int i = 0; i < XbowParts.QuiverSlots.Length; i++)
            {
                Transform slot = Empty($"ecp_xbow_quiver_bolt_{i}", quiver.transform);
                slot.localPosition = XbowParts.QuiverSlots[i];
                slot.localRotation = XbowParts.QuiverSlot(i);
                XbowBolt.In(slot, $"ecp_xbow_bolt_quiver_{i}", 1f);
            }
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
