using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// Builds ecr_rimegiant_kit.prefab, everything the Rime Giant wears, grouped under mounts named after the Troll's
    /// bones. The mod parents each mount to the bone of that name (local transform zero), so each part keeps the offset
    /// measured here on the same skeleton at the game's own scale (the armature at 130):
    ///
    ///   Spine2/ecr_rime_plate_0        the chest            Spine2/ecr_rime_plate_1        the upper back
    ///   LeftArm/ecr_rime_plate_2       the left shoulder    RightArm/ecr_rime_plate_3      the right shoulder
    ///   LeftForeArm/ecr_rime_plate_4   the left forearm     RightForeArm/ecr_rime_plate_5  the right forearm
    ///   LeftUpLeg/ecr_rime_plate_6     the left thigh       RightUpLeg/ecr_rime_plate_7    the right thigh
    ///
    /// placed in the idle's first frame, and the crust, placed in the Sleeping clip's first frame, one piece per bone it
    /// lies on: Spine2/ecr_rime_crust (upper back and shoulders), Spine1/ecr_rime_crust_1 (lower back), Head/_2,
    /// LeftForeArm/_3, RightForeArm/_4, LeftUpLeg/_5 and RightUpLeg/_6 (the knees). No colliders, rigidbodies or animators.
    /// </summary>
    public static class RimeKit
    {
        public const string Name = "ecr_rimegiant_kit";

        public static string CrustName(int piece) => piece == 0 ? "ecr_rime_crust" : "ecr_rime_crust_" + piece;

        public static string Build(GameObject troll, RimePoser poser, RimeFitData fit, string folder)
        {
            var kit = new GameObject(Name);
            poser.Pose("Idle", 0f);
            foreach (PlateFit plate in fit.plates)
            {
                GameObject part = Part(plate.name, plate.name, Mount(kit, plate.mount));
                Place(part.transform, troll, plate.position, plate.rotation);
            }
            poser.Pose("Sleeping", 0f);
            for (int piece = 0; piece < fit.crust.mounts.Length; piece++)
            {
                GameObject part = Part(CrustName(piece), CrustName(piece), Mount(kit, fit.crust.mounts[piece]));
                Place(part.transform, troll, Vector3.zero, Quaternion.identity);
            }
            Check(kit);
            string path = folder + "/" + Name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(kit, path);
            UnityEngine.Object.DestroyImmediate(kit);
            Log.Info("kit " + path);
            return path;
        }

        /// <summary>Gives a part under a mount the world pose it has on the posed skeleton, as an offset from the bone.</summary>
        private static void Place(Transform part, GameObject troll, Vector3 position, Quaternion rotation)
        {
            Transform bone = RimeReference.Bone(troll, part.parent.name);
            part.localPosition = bone.InverseTransformPoint(position);
            part.localRotation = Quaternion.Inverse(bone.rotation) * rotation;
            part.localScale = Vector3.one / bone.lossyScale.x;
        }

        private static void Check(GameObject kit)
        {
            if (kit.GetComponentsInChildren<Component>(true).Any(c => c is Collider || c is Rigidbody || c is Animator))
                throw new InvalidOperationException("the kit must carry no colliders, rigidbodies or animators");
            var named = kit.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("ecr_rime_")).ToArray();
            if (named.Length != named.Select(t => t.name).Distinct().Count() || named.Any(t => t.parent.parent != kit.transform))
                throw new InvalidOperationException("every ecr_rime_ part must be named once and hang straight from its mount");
            if (named.Count(t => t.name.StartsWith("ecr_rime_plate_")) != RimePlates.Count)
                throw new InvalidOperationException("the kit must have " + RimePlates.Count + " plates");
        }

        private static Transform Mount(GameObject kit, string bone)
        {
            Transform mount = kit.transform.Find(bone);
            if (mount != null)
                return mount;
            mount = new GameObject(bone).transform;
            mount.SetParent(kit.transform, false);
            return mount;
        }

        private static GameObject Part(string asset, string name, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RimeBuild.PartPrefab(asset));
            if (prefab == null)
                throw new InvalidOperationException("no part " + asset + "; build it in Blender first");
            var part = UnityEngine.Object.Instantiate(prefab, parent, false);
            part.name = name;
            Flatten(part);
            return part;
        }

        /// <summary>
        /// Moves the mesh from the FBX's inner node (named like the part) onto the part itself, so a plate is one object
        /// the mod can detach and a name lookup finds only the part. The inner node carries no transform of its own.
        /// </summary>
        public static void Flatten(GameObject part)
        {
            Transform inner = part.GetComponentInChildren<MeshFilter>().transform;
            bool plain = inner.localPosition.sqrMagnitude < 1e-10f && Quaternion.Angle(inner.localRotation, Quaternion.identity) < 0.01f
                         && (inner.localScale - Vector3.one).sqrMagnitude < 1e-8f;
            if (inner == part.transform || !plain || inner.childCount > 0)
                throw new InvalidOperationException(part.name + ": expected the FBX's mesh on a plain inner node");
            part.AddComponent<MeshFilter>().sharedMesh = inner.GetComponent<MeshFilter>().sharedMesh;
            part.AddComponent<MeshRenderer>().sharedMaterials = inner.GetComponent<MeshRenderer>().sharedMaterials;
            UnityEngine.Object.DestroyImmediate(inner.gameObject);
        }
    }
}
