using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.Crossbow
{
    /// <summary>
    /// Renders the crossbowman as the game will show it: the game's Skeleton (reference, preview only) wearing the kit
    /// the way the mod hangs it, the string and bolts moved like the mod moves them. Stills of the shot's moments from
    /// three sides, the idle, walk and run with the kit on (to see the quiver clear the arm and leg), and the frames of
    /// a video played through the game's own shot states (<see cref="XbowGameAnimator"/>): idle, the attack, idle.
    /// </summary>
    public static class XbowPreview
    {
        private static readonly (string clip, float time)[] Moments =
        {
            (XbowClips.AimName, 0.24f), (XbowClips.AimName, 0.9f), (XbowClips.FireName, 0.38f), (XbowClips.FireName, 1.12f),
            (XbowClips.FireName, XbowClips.StringGrab), (XbowClips.FireName, XbowClips.Spanned), (XbowClips.FireName, XbowClips.BoltGrab),
            (XbowClips.FireName, 3.12f), (XbowClips.FireName, XbowClips.Lay),
        };
        private static readonly (string state, float time)[] Carry =
            { ("ecp_xbow_idle", 0f), ("ecp_xbow_idle", 1.2f), ("ecp_xbow_walk", 0.2f), ("ecp_xbow_walk", 0.6f), ("ecp_xbow_run", 0.3f), ("ecp_xbow_run", 0.6f) };
        public static readonly (string name, Vector3 eye, Vector3 look)[] Views =
        {
            ("front", new Vector3(1.9f, 1.55f, 3.3f), new Vector3(0f, 1.15f, 0.2f)),
            ("side", new Vector3(3.6f, 1.35f, 0.3f), new Vector3(0f, 1.15f, 0.25f)),
            ("left", new Vector3(-2.6f, 1.45f, 1.6f), new Vector3(0f, 1.05f, 0.1f)),
        };

        public static void Render(string folder)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SlingerStage.Build();
            GameObject skeleton = XbowReference.Skeleton();
            Mount(skeleton);
            var rig = new XbowRigPreview(skeleton);
            Directory.CreateDirectory(folder);
            XbowPoser played = XbowBuild.Played(skeleton);
            foreach (var (clip, time) in Moments)
                Still(folder, $"still_{clip.Substring(9)}_{time:0.00}", () => played.Pose(clip, time), rig, clip == XbowClips.FireName ? time : -1f);
            foreach (var (state, time) in Carry)
                Still(folder, $"carry_{state.Substring(9)}_{time:0.00}", () => played.Pose(state, time), rig, -1f);
            XbowFrames.Render(Path.Combine(folder, "frames"), skeleton, rig);
        }

        /// <summary>The kit's mounts onto the bones of the same names, as the mod does it.</summary>
        public static void Mount(GameObject skeleton)
        {
            var kit = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(XbowBuild.Folder + "/" + XbowKit.Name + ".prefab"));
            foreach (Transform mount in kit.transform.Cast<Transform>().ToArray())
            {
                mount.SetParent(XbowReference.Bone(skeleton, mount.name), false);
                mount.localPosition = Vector3.zero;
                mount.localRotation = Quaternion.identity;
                mount.localScale = Vector3.one;
            }
            Object.DestroyImmediate(kit);
            Dress(skeleton);
        }

        /// <summary>The parts' own baked textures, a little matte, as the game's creature shader would show them.</summary>
        private static void Dress(GameObject skeleton)
        {
            foreach (var renderer in skeleton.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("ecp_xbow")))
                foreach (var material in renderer.sharedMaterials)
                    material.SetFloat("_Glossiness", 0.08f);
        }

        private static void Still(string folder, string name, System.Action pose, XbowRigPreview rig, float fireTime)
        {
            pose();
            rig.Update(fireTime);
            Camera camera = Camera.main;
            foreach (var (view, eye, look) in Views)
            {
                SlingerStage.Aim(camera, eye, look);
                SlingerStage.Shoot(folder, $"{name}_{view}", camera, 640, 640);
            }
        }
    }
}
