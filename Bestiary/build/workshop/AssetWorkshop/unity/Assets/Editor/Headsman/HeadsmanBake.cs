using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Crossbow;

namespace Workshop.Headsman
{
    /// <summary>
    /// Bakes the headsman's whole fight for Blender (assets/ecp_headsman/blender_scene.py builds the .blend): the game's
    /// Skeleton (reference, preview only) at the boss's size (<see cref="Scale"/>) with the axe hung from its right
    /// fist, played through a controller that blends like the game's (<see cref="HeadsmanAnimator"/>): a second of the
    /// carry idle, then each attack with a second of idle after it, then the carry walk and run, two loops each. What
    /// the mod does in code is done here the same way (<see cref="HeadsmanPlayback"/>), and the effects around it by
    /// <see cref="HeadsmanEffects"/>. Skinned meshes are recorded by <see cref="XbowCache"/> (point caches), rigid ones by
    /// <see cref="HeadsmanRigid"/>; sequence.json names the camera for each part. 30 frames a second.
    /// </summary>
    public static class HeadsmanBake
    {
        public const float Fps = 30f, Scale = 1.25f, Gap = 1f;

        public static void Run(string folder, HeadsmanGrip grip, HeadsmanMove[] moves)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject boss = XbowReference.Skeleton();
            boss.name = "headsman";
            boss.transform.localScale = Vector3.one * Scale;
            var playback = new HeadsmanPlayback(boss, grip);
            var effects = new HeadsmanEffects(boss, grip, playback, Scale);
            Animator animator = XbowReference.Animator(boss);
            animator.runtimeAnimatorController = HeadsmanAnimator.Build(moves);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // no camera sees it here
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Update(0f);
            List<Renderer> skinned = boss.GetComponentsInChildren<Renderer>(true).Where(r => !r.name.StartsWith("ecp_")).Concat(effects.Skinned).ToList();
            var cache = new XbowCache(skinned);
            var rigid = new HeadsmanRigid();
            foreach (var (renderer, name) in effects.Rigid())
                rigid.Add(renderer, name);
            var timeline = new HeadsmanTimeline(boss, animator, moves, playback, effects, cache, rigid);
            // XbowCache names each part by its place in the list and its renderer's name.
            timeline.SummonBodies = Enumerable.Range(0, 2)
                .Select(i => effects.Summon.Body(i)).Select(r => $"{skinned.IndexOf(r):00}_{r.name}").ToArray();
            Play(timeline, moves);
            cache.Write(folder, Fps);
            rigid.Write(folder);
            timeline.Write(folder);
        }

        private static void Play(HeadsmanTimeline timeline, HeadsmanMove[] moves)
        {
            timeline.Idle(Gap, "idle");
            foreach (HeadsmanMove move in moves)
            {
                timeline.Attack(move);
                timeline.Idle(Gap, null);
            }
            timeline.Loop(HeadsmanAnimator.Walk, "walk", 2);
            timeline.Idle(0.5f, null);
            timeline.Loop(HeadsmanAnimator.Run, "run", 2);
            timeline.Idle(Gap, null);
        }
    }
}
