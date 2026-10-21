using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Crossbow;

namespace Workshop.Headsman
{
    /// <summary>
    /// Logs what the headsman's clips are authored against: the Skeleton's bones on the idle's first frame, the game
    /// clips' settings and lengths, the avatar's limits on the spine, neck and head, and the axe's size and grips.
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.Headsman.HeadsmanProbe.Run
    /// </summary>
    public static class HeadsmanProbe
    {
        private static readonly string[] Bones =
        {
            "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
            "RightArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightFoot",
        };

        public static void Run()
        {
            int code = 1;
            try
            {
                Probe();
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("headsman probe failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void Probe()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject skeleton = XbowReference.Skeleton();
            new XbowPoser(skeleton).Pose("Idle", 0f);
            Log.Info("bones: " + string.Join(" ", skeleton.GetComponentsInChildren<Transform>().Select(t => t.name)));
            foreach (string bone in Bones)
            {
                Transform t = XbowReference.Bone(skeleton, bone);
                Log.Info($"bone {bone}: at {t.position:F3}, fwd {t.forward:F2} up {t.up:F2} right {t.right:F2}, scale {t.lossyScale.x:F3}");
            }
            foreach (string state in new[] { "Idle", "Walk", "Run" })
                Settings(state, XbowReference.Clip(state));
            Limits(XbowReference.Animator(skeleton).avatar);
            HeadsmanAxe.Stage();
            HeadsmanAxe.Report();
        }

        private static void Settings(string state, AnimationClip clip)
        {
            AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
            Log.Info($"clip {state} ({clip.name}): {clip.length:F3} s, loop {s.loopTime}, bake rot {s.loopBlendOrientation} y {s.loopBlendPositionY} "
                     + $"xz {s.loopBlendPositionXZ}, keep rot {s.keepOriginalOrientation} y {s.keepOriginalPositionY} xz {s.keepOriginalPositionXZ}, "
                     + $"feet {s.heightFromFeet}, offset {s.orientationOffsetY:F1}, level {s.level:F3}");
        }

        private static void Limits(Avatar avatar)
        {
            foreach (HumanBone bone in avatar.humanDescription.human)
            {
                if (!new[] { "Spine", "Chest", "UpperChest", "Neck", "Head", "Hips", "LeftUpperLeg", "LeftHand", "RightHand" }.Contains(bone.humanName))
                    continue;
                HumanLimit l = bone.limit;
                Log.Info($"limit {bone.humanName} ({bone.boneName}): default {l.useDefaultValues}, min {l.min:F1}, max {l.max:F1}, center {l.center:F1}");
            }
            foreach (string muscle in HumanTrait.MuscleName.Where(m => m.Contains("Twist") || m.Contains("Turn") || m.StartsWith("Right Hand")))
            {
                int i = Array.IndexOf(HumanTrait.MuscleName, muscle);
                Log.Info($"muscle {muscle}: default {HumanTrait.GetMuscleDefaultMin(i):F0}..{HumanTrait.GetMuscleDefaultMax(i):F0}");
            }
        }
    }
}
