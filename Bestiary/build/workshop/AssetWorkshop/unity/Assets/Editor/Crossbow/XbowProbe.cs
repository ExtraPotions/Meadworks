using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.Crossbow
{
    /// <summary>
    /// Facts about the Skeleton the crossbowman's clips are authored on, logged, plus stills of its game clips:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.Crossbow.XbowProbe.Run -workshopOut &lt;folder&gt;
    /// The game's crude bow sits on LeftHand_Attach as the game's VisEquipment puts it, and both hand attach points get
    /// axis markers (x red, y green, z blue, 10 cm), so the stills show how the archer holds its bow and which way
    /// each attach frame points.
    /// </summary>
    public static class XbowProbe
    {
        private static readonly (string state, float time)[] Poses =
            { ("Idle", 0f), ("Walk", 0.2f), ("Walk", 0.6f), ("Run", 0.3f), ("Aim", 0.5f), ("Recoil", 0f), ("Recoil", 0.35f) };
        private static readonly string[] Bones =
            { "Hips", "Spine2", "Neck", "Head", "LeftArm", "LeftForeArm", "LeftHand", "LeftHand_Attach", "RightArm", "RightForeArm", "RightHand", "RightHand_Attach", "LeftUpLeg", "RightUpLeg" };

        public static void Run()
        {
            int code = 1;
            try
            {
                Probe(SlingerStage.Argument("-workshopOut"));
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("crossbow probe failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void Probe(string folder)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var skeleton = XbowReference.Skeleton();
            var animator = XbowReference.Animator(skeleton);
            Log.Info($"avatar {animator.avatar.name} human {animator.avatar.isHuman} valid {animator.avatar.isValid} scale {animator.humanScale:F3}");
            foreach (var renderer in skeleton.GetComponentsInChildren<Renderer>())
                Log.Info($"renderer {renderer.name} bounds {renderer.bounds.size:F2}");
            var poser = new XbowPoser(skeleton);
            foreach (string state in XbowPoser.GameStates)
                Log.Info($"clip {state}: {poser.Length(state):F2} s");
            Bow(skeleton);
            Marker(XbowReference.Bone(skeleton, "LeftHand_Attach"));
            Marker(XbowReference.Bone(skeleton, "RightHand_Attach"));
            SlingerStage.Build();
            Directory.CreateDirectory(folder);
            foreach (var (state, time) in Poses)
                Still(folder, skeleton, poser, state, time);
        }

        private static void Still(string folder, GameObject skeleton, XbowPoser poser, string state, float time)
        {
            poser.Pose(state, time);
            foreach (string bone in Bones)
            {
                Transform t = XbowReference.Bone(skeleton, bone);
                Log.Info($"{state} {time:0.00} {bone}: pos {t.position:F3} lossy {t.lossyScale.x:F2} fwd {t.forward:F2} up {t.up:F2} right {t.right:F2}");
            }
            var camera = Camera.main;
            foreach (var (name, eye, look) in new[] {
                ("front", new Vector3(1.6f, 1.4f, 3.2f), new Vector3(0f, 1.0f, 0f)),
                ("side", new Vector3(3.6f, 1.2f, 0.2f), new Vector3(0f, 1.0f, 0.2f)),
                ("behind", new Vector3(-1.4f, 1.6f, -3.0f), new Vector3(0f, 1.0f, 0.2f)) })
            {
                SlingerStage.Aim(camera, eye, look);
                SlingerStage.Shoot(folder, $"probe_{state}_{time:0.00}_{name}", camera, 640, 640);
            }
        }

        /// <summary>The crude bow's mesh under LeftHand_Attach, where the game's VisEquipment puts skeleton_bow's attach.</summary>
        private static void Bow(GameObject skeleton)
        {
            var attach = new GameObject("bow_attach").transform;
            attach.SetParent(XbowReference.Bone(skeleton, "LeftHand_Attach"), true);
            attach.localPosition = Vector3.zero;
            attach.localRotation = Quaternion.identity;
            var bow = new GameObject("bow");
            bow.transform.SetParent(attach, false);
            bow.transform.localPosition = new Vector3(0.021f, 0.043f, 0.062f);
            bow.transform.localRotation = new Quaternion(-0.18113379f, -0.3371026f, 0.8907433f, 0.24521193f);
            bow.transform.localScale = Vector3.one * 0.8f;
            bow.AddComponent<MeshFilter>().sharedMesh = ReferenceAssets.Mesh("GameElements/Items/weapons/_res/bow/Mesh_polySurface2_polySurface1.asset");
            bow.AddComponent<MeshRenderer>().sharedMaterial = SlingerStage.Plain("bow", new Color(0.35f, 0.24f, 0.14f));
        }

        private static void Marker(Transform joint)
        {
            var root = new GameObject("marker").transform;
            root.SetParent(joint, true);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            Axis(root, Vector3.right, Color.red);
            Axis(root, Vector3.up, Color.green);
            Axis(root, Vector3.forward, Color.blue);
        }

        private static void Axis(Transform root, Vector3 along, Color colour)
        {
            var stick = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(stick.GetComponent<Collider>());
            stick.transform.SetParent(root, false);
            float scale = 1f / root.lossyScale.x;
            stick.transform.localPosition = along * 0.05f * scale;
            stick.transform.localScale = (Vector3.one * 0.008f + along * 0.1f) * scale;
            var material = SlingerStage.Plain("axis", colour);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", colour);
            stick.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
