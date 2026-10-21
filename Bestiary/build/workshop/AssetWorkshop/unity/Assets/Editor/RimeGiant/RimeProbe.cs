using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// Facts about the Troll the kit is measured on, logged, plus stills of each clip:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.RimeGiant.RimeProbe.Run -workshopOut &lt;folder&gt;
    /// Checks that the avatar is human and the troll faces +Z, logs the bones in the idle and asleep, and renders each
    /// clip at a few moments from the front and the side.
    /// </summary>
    public static class RimeProbe
    {
        private static readonly string[] Bones =
        {
            "Root", "Spine0", "Spine1", "Spine2", "Head", "Nose", "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
            "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg",
        };

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
                Log.Error("rime probe failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void Probe(string folder)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RimeStage.Build();
            var troll = RimeReference.Troll();
            var animator = troll.GetComponent<Animator>();
            Log.Info($"avatar {animator.avatar.name} human {animator.avatar.isHuman} valid {animator.avatar.isValid} scale {animator.humanScale:F3}");
            Directory.CreateDirectory(folder);
            var poser = new RimePoser(troll);
            foreach (string state in RimePoser.States)
                Clip(folder, troll, poser, state);
            troll.SetActive(false);
            FrostTroll(folder);
        }

        /// <summary>
        /// The developers' unused ice troll (Characters/FrostTroll, reference only), at the Troll's game scale in the
        /// idle, from four sides: how they hung rock and ice on a troll, for the plates' scale and density.
        /// </summary>
        private static void FrostTroll(string folder)
        {
            var frost = RimeReference.FrostTroll();
            new RimePoser(frost).Pose("Idle", 0f);
            var camera = Camera.main;
            foreach (var (name, eye) in new[] { ("front", new Vector3(6f, 5f, 16f)), ("back", new Vector3(-7f, 6f, -15f)),
                         ("side", new Vector3(17f, 4f, 0f)), ("top", new Vector3(2f, 18f, 6f)) })
            {
                SlingerStage.Aim(camera, eye, new Vector3(0f, 3f, 0f));
                SlingerStage.Shoot(folder, "frosttroll_" + name, camera, 1280, 960);
            }
        }

        private static void Clip(string folder, GameObject troll, RimePoser poser, string state)
        {
            AnimationClip clip = RimeReference.Clip(state);
            Log.Info($"clip {state}: {clip.name} length {clip.length:F2} s, loop {clip.isLooping}, human {clip.humanMotion}");
            var camera = Camera.main;
            foreach (float fraction in new[] { 0f, 0.35f, 0.6f, 0.99f })
            {
                poser.Pose(state, clip.length * fraction);
                if (fraction == 0f)
                    Report(troll, state);
                SlingerStage.Aim(camera, new Vector3(6f, 5f, 16f), new Vector3(0f, 3f, 0f));
                SlingerStage.Shoot(folder, $"{state}_{fraction:0.00}_front", camera);
                SlingerStage.Aim(camera, new Vector3(17f, 4f, 0f), new Vector3(0f, 3f, 0f));
                SlingerStage.Shoot(folder, $"{state}_{fraction:0.00}_side", camera);
            }
        }

        private static void Report(GameObject troll, string state)
        {
            string At(string bone)
            {
                Transform t = RimeReference.Bone(troll, bone);
                return $"{bone} {t.position:F2} lossy {t.lossyScale.x:F1} up {t.up:F2} fwd {t.forward:F2}";
            }
            Log.Info($"{state} bones: " + string.Join("; ", Bones.Select(At)));
            foreach (var renderer in troll.GetComponentsInChildren<Renderer>())
                Log.Info($"{state} renderer {renderer.name} bounds {renderer.bounds.center:F2} size {renderer.bounds.size:F2}");
        }
    }
}
