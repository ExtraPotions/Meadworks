using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Crossbow;
using Workshop.Slinger;

namespace Workshop.SkelArsenal
{
    /// <summary>
    /// Bakes the skeleton arsenal showcase for Blender (assets/ecp_skel_arsenal/blender_scene.py builds the .blend):
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.SkelArsenal.ArsenalBake.Run -workshopOut &lt;folder&gt;
    /// Seven of the game's Skeletons (reference, preview only) stand in a row, each holding one weapon of the arsenal as
    /// the game hangs a held item (VisEquipment: the prefab under RightHand_Attach, the bow under LeftHand_Attach, at
    /// zero position and rotation) and running its attack through <see cref="ArsenalAnimator"/>. Each is baked on its own
    /// as one loop at 30 frames a second by <see cref="XbowCache"/> - idle, the attack, back to idle - into
    /// &lt;folder&gt;/&lt;weapon&gt;/, and &lt;folder&gt;/arsenal.json lists them for Blender. Needs the prefabs in
    /// Assets/Bundles/ecp_skel_arsenal (build.ps1 -Bundle ecp_skel_arsenal).
    /// </summary>
    public static class ArsenalBake
    {
        public const float Fps = 30f, Spacing = 2.6f;
        private const float Lead = 0.5f, Tail = 0.6f, Longest = 8f;
        private const string Bundle = "Assets/Bundles/ecp_skel_arsenal/";

        [Serializable]
        public sealed class Group
        {
            public string weapon, label, folder;
            public int frames, attack, loose = -1;
            public float x;                 // Blender x of the skeleton's feet; y and z are 0
        }

        [Serializable]
        public sealed class Lineup
        {
            public float fps;
            public Group[] groups;
        }

        public static void Run()
        {
            int code = 1;
            try
            {
                Bake(SlingerStage.Argument("-workshopOut"));
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("skeleton arsenal bake failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void Bake(string folder)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AnimationClip idle = XbowReference.Clip("Idle");
            var groups = ArsenalRoutines.All.Select((routine, i) => Skeleton(routine, i, idle, folder)).ToArray();
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "arsenal.json"), JsonUtility.ToJson(new Lineup { fps = Fps, groups = groups }, true));
            Log.Info($"skeleton arsenal: {groups.Length} skeletons baked into {folder}");
        }

        /// <summary>One Skeleton with one weapon, placed in the row, baked as one loop into its own folder.</summary>
        private static Group Skeleton(Routine routine, int index, AnimationClip idle, string folder)
        {
            GameObject skeleton = XbowReference.Skeleton();
            skeleton.name = "Skeleton_" + routine.Label;
            float x = (index - (ArsenalRoutines.All.Length - 1) / 2f) * Spacing;
            skeleton.transform.position = new Vector3(-x, 0f, 0f);   // Blender x = -Unity x
            GameObject weapon = Mount(routine, skeleton);
            ArsenalBow bow = routine.LeftHand
                ? new ArsenalBow(weapon, XbowReference.Bone(skeleton, "RightHand_Attach"), Prefab("ecp_skel_arrow"), ArsenalBow.PointsFile())
                : null;
            Animator animator = Animate(skeleton, routine, idle);
            var renderers = skeleton.GetComponentsInChildren<Renderer>(true).Concat(bow?.Renderers ?? Enumerable.Empty<Renderer>()).ToArray();
            var cache = new XbowCache(renderers);
            var group = new Group { weapon = routine.Weapon, label = routine.Label, folder = routine.Weapon, x = x };
            Play(routine, animator, bow, cache, group);
            cache.Write(Path.Combine(folder, routine.Weapon), Fps);
            Report(routine, weapon, renderers, group, bow);
            skeleton.SetActive(false);
            return group;
        }

        /// <summary>The weapon's prefab under the hand's attach point, as VisEquipment.AttachItem puts a held item.</summary>
        private static GameObject Mount(Routine routine, GameObject skeleton)
        {
            GameObject weapon = Prefab(routine.Weapon);
            Transform joint = XbowReference.Bone(skeleton, routine.LeftHand ? "LeftHand_Attach" : "RightHand_Attach");
            weapon.transform.SetParent(joint, true);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
            return weapon;
        }

        private static Animator Animate(GameObject skeleton, Routine routine, AnimationClip idle)
        {
            Animator animator = XbowReference.Animator(skeleton);
            animator.runtimeAnimatorController = ArsenalAnimator.Build(routine, idle);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // no camera sees it here
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Update(0f);
            return animator;
        }

        /// <summary>Idle for Lead seconds, the attack, then idle until Tail seconds after the Animator is back in Movement.</summary>
        private static void Play(Routine routine, Animator animator, ArsenalBow bow, XbowCache cache, Group group)
        {
            int lead = Mathf.RoundToInt(Lead * Fps), tail = Mathf.RoundToInt(Tail * Fps), back = -1, frame = 0;
            cache.Mark("idle");
            for (; frame < Longest * Fps && (back < 0 || frame < back + tail); frame++)
            {
                if (frame == lead)
                {
                    animator.SetTrigger(ArsenalAnimator.Trigger);
                    cache.Mark(routine.Label.ToLowerInvariant());
                    group.attack = frame;
                }
                animator.Update(1f / Fps);
                bow?.Update(animator, 1f / Fps);
                if (bow != null && bow.JustLoosed)
                    group.loose = frame;
                cache.Capture(-1f);
                if (back < 0 && frame > lead + 2 && InMovement(animator))
                    back = frame;
            }
            group.frames = frame;
        }

        private static bool InMovement(Animator animator) =>
            !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName(ArsenalAnimator.Movement);

        private static GameObject Prefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Bundle}{name}/{name}.prefab");
            if (prefab == null)
                throw new FileNotFoundException("no prefab; run build.ps1 -Bundle ecp_skel_arsenal first", name);
            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = name;
            return instance;
        }

        private static void Report(Routine routine, GameObject weapon, Renderer[] renderers, Group group, ArsenalBow bow)
        {
            Bounds held = weapon.GetComponentInChildren<Renderer>().bounds;
            string parts = string.Join(", ", renderers.Select(r => r.name));
            Log.Info($"arsenal {routine.Label}: {group.frames} frames, attack at {group.attack}, loose {group.loose}, " +
                     $"weapon {held.size.x:0.00} x {held.size.y:0.00} x {held.size.z:0.00} m after the loop; parts: {parts}");
            if (bow != null)
                Log.Info($"arsenal bow: drew {bow.Draw:0.00} m behind the string's rest");
        }
    }
}
