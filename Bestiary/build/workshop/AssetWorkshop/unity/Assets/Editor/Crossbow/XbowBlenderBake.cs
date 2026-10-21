using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.Crossbow
{
    /// <summary>
    /// Bakes the crossbowman's animations for Blender (assets/ecp_crossbowman/blender_scene.py builds the .blend):
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.Crossbow.XbowBlenderBake.Run -workshopOut &lt;folder&gt;
    /// The game's Skeleton (reference, preview only) wearing the kit as the mod hangs it, string and bolts moved as the mod
    /// moves them, recorded at 30 frames a second by <see cref="XbowCache"/>: the carry idle, the attack through the
    /// game's own shot states (<see cref="XbowGameAnimator"/>: raise and aim, the shot with its flying bolt, the reload),
    /// the carry idle again, then two loops of the walk and two of the run. Markers name each part for the timeline.
    /// Run it after <see cref="XbowBuild"/>, which leaves the kit and the clips in Assets/Bundles/ecp_crossbowman.
    /// </summary>
    public static class XbowBlenderBake
    {
        private const float Fps = 30f;
        private const float Lead = 0.6f, Attack = 6.0f;

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
                Log.Error("crossbowman blender bake failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void Bake(string folder)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject skeleton = XbowReference.Skeleton();
            XbowPreview.Mount(skeleton);
            var rig = new XbowRigPreview(skeleton);
            var flight = new XbowFlight(XbowReference.Bone(skeleton, "ecp_xbow_muzzle"));
            var cache = new XbowCache(UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            Follow(cache.Tracks, skeleton, flight);
            Shot(skeleton, rig, flight, cache);
            Loops(skeleton, rig, flight, cache);
            cache.Write(folder, Fps);
        }

        /// <summary>Moments of the fire clip (its own seconds) that get a marker when the clip passes them.</summary>
        private static readonly (float time, string name)[] Moments =
        {
            (XbowClips.Fire, "fire"), (0.6f, "lower"), (XbowClips.StringGrab, "span the string"),
            (XbowClips.BoltGrab, "bolt from the quiver"), (XbowClips.Lay, "load the bolt"),
        };

        /// <summary>The carry idle, the attack through the game's own shot states, and the idle again.</summary>
        private static void Shot(GameObject skeleton, XbowRigPreview rig, XbowFlight flight, XbowCache cache)
        {
            Animator animator = XbowReference.Animator(skeleton);
            animator.runtimeAnimatorController = XbowGameAnimator.Build(Clip(XbowClips.AimName), Clip(XbowClips.FireName));
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // no camera sees it here
            animator.Rebind();
            animator.Update(0f);
            cache.Mark("carry (idle)");
            float last = -1f;
            int frames = Mathf.RoundToInt((Lead + Attack) * Fps);
            for (int frame = 0; frame < frames; frame++)
            {
                if (frame == Mathf.RoundToInt(Lead * Fps))
                {
                    animator.SetTrigger(XbowGameAnimator.Trigger);
                    cache.Mark("raise and aim");
                }
                animator.Update(1f / Fps);
                float time = XbowFrames.FireTime(animator);
                Mark(cache, last, time);
                Capture(rig, flight, cache, time);
                last = time;
            }
        }

        private static void Mark(XbowCache cache, float last, float time)
        {
            foreach (var (at, name) in Moments)
            {
                if (last < at && time >= at)
                    cache.Mark(name);
            }
            if (last >= 0f && time < 0f)
                cache.Mark("carry again");
        }

        /// <summary>Two loops of the carry walk and two of the carry run, played as the Animator plays them.</summary>
        private static void Loops(GameObject skeleton, XbowRigPreview rig, XbowFlight flight, XbowCache cache)
        {
            XbowPoser played = XbowBuild.Played(skeleton);
            foreach (string clip in new[] { "ecp_xbow_walk", "ecp_xbow_run" })
            {
                cache.Mark(clip.Substring(9));
                float length = played.Length(clip);
                int frames = Mathf.RoundToInt(2f * length * Fps);
                for (int frame = 0; frame < frames; frame++)
                {
                    played.Pose(clip, Mathf.Repeat(frame / Fps, length));
                    Capture(rig, flight, cache, -1f);
                }
            }
        }

        private static void Capture(XbowRigPreview rig, XbowFlight flight, XbowCache cache, float fireTime)
        {
            rig.Update(fireTime);
            flight.Update(fireTime);
            cache.Capture(fireTime);
        }

        /// <summary>The crossbow and every bolt, for putting another crossbow and its bolts in their places in Blender.</summary>
        private static void Follow(XbowTracks tracks, GameObject skeleton, XbowFlight flight)
        {
            foreach (string name in new[] { "ecp_xbow_crossbow", "ecp_xbow_bolt_groove", "ecp_xbow_bolt_hand" })
                tracks.Follow(name, XbowReference.Bone(skeleton, name));
            for (int i = 0; i < XbowParts.QuiverSlots.Length; i++)
                tracks.Follow($"ecp_xbow_bolt_quiver_{i}", XbowReference.Bone(skeleton, $"ecp_xbow_bolt_quiver_{i}"));
            tracks.Follow("flying_bolt", flight.Bolt);
        }

        private static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(XbowBuild.ClipPath(name));
    }
}
