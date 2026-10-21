using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// Facts about the Greydwarf skeleton the clip is authored on, logged, plus an idle still:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.Slinger.SlingerProbe.Run -workshopOut &lt;folder&gt;
    /// Checks that the avatar is human, that a pose read back from the idle clip matches the clip's own root curves
    /// (so RootT/RootQ can be written straight from HumanPose), and that every muscle maps to one of the clip's curves.
    /// </summary>
    public static class SlingerProbe
    {
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
                Log.Error("slinger probe failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void Probe(string folder)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var greydwarf = SlingerReference.Greydwarf();
            var animator = greydwarf.GetComponent<Animator>();
            Log.Info($"avatar {animator.avatar.name} human {animator.avatar.isHuman} valid {animator.avatar.isValid} scale {animator.humanScale:F3}");
            var idle = SlingerReference.Clip("Idle");
            idle.SampleAnimation(greydwarf, 0f);
            foreach (string bone in new[] { "root", "spine1", "spine2", "spine3", "head", "l_shoulder", "l_arm1", "l_arm2", "l_hand", "l_middle1", "r_arm1", "r_hand", "r_index2", "l_foot" })
            {
                Transform t = SlingerReference.Bone(greydwarf, bone);
                Log.Info($"bone {bone}: pos {t.position:F3} lossy {t.lossyScale:F2} fwd {t.forward:F2} up {t.up:F2}");
            }
            foreach (var renderer in greydwarf.GetComponentsInChildren<Renderer>())
                Log.Info($"renderer {renderer.name} bounds {renderer.bounds.size:F2}");
            CheckRoot(greydwarf, animator, idle);
            CheckMuscleNames(idle);
            SlingerStage.Build();
            Directory.CreateDirectory(folder);
            SlingerStage.Shoot(folder, "probe_idle", Camera.main);
        }

        private static void CheckRoot(GameObject greydwarf, Animator animator, AnimationClip idle)
        {
            var handler = new HumanPoseHandler(animator.avatar, greydwarf.transform);
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            float Curve(string name) => AnimationUtility.GetEditorCurve(idle,
                EditorCurveBinding.FloatCurve("", typeof(Animator), name)).Evaluate(0f);
            Log.Info($"pose body {pose.bodyPosition:F3} {pose.bodyRotation.eulerAngles:F1}; clip RootT ({Curve("RootT.x"):F3}, {Curve("RootT.y"):F3}, {Curve("RootT.z"):F3})");
            Log.Info($"pose muscle 'Left Arm Down-Up' {pose.muscles[Array.IndexOf(HumanTrait.MuscleName, "Left Arm Down-Up")]:F3}; clip {Curve("Left Arm Down-Up"):F3}");
        }

        private static void CheckMuscleNames(AnimationClip idle)
        {
            var attributes = AnimationUtility.GetCurveBindings(idle).Select(b => b.propertyName).ToHashSet();
            var missing = HumanTrait.MuscleName.Select(SlingClip.CurveName).Where(n => !attributes.Contains(n)).ToArray();
            Log.Info($"idle has {attributes.Count} curves; muscles without a curve: {(missing.Length == 0 ? "none" : string.Join(", ", missing))}");
            Log.Info("sample curves: " + string.Join(", ", attributes.Where(a => a.Contains("Hand") || a.Contains("Index")).Take(12)));
        }
    }
}
