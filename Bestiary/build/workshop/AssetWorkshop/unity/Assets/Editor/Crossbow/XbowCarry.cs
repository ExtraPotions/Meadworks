using System;
using UnityEditor;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.Crossbow
{
    /// <summary>
    /// The crossbowman's own idle, walk and run, which the mod puts in place of the Skeleton's (its "Idle", the
    /// "Shield-Walk-Injured" of its walk and strafe, the "Shield-Run-Forward" of its run): each frame of the game's clip as
    /// the Animator plays it, legs, hips and chest untouched, with both hands on the crossbow at the low ready riding the
    /// chest (<see cref="XbowStance.Carry"/>), read back as muscles. The archer carries its bow in the left hand through
    /// a shield walk, which swings that hand across the crotch; a crossbow there would cut through the legs. Keyed at 30
    /// a second, the last frame the first again so the loop closes; the game clip's own settings (loop, root baking).
    /// </summary>
    public static class XbowCarry
    {
        public static readonly (string name, string state, string replaces)[] Clips =
        {
            ("ecp_xbow_idle", "Idle", "Idle"), ("ecp_xbow_walk", "Walk", "Shield-Walk-Injured"), ("ecp_xbow_run", "Run", "Shield-Run-Forward"),
        };

        private const float Rate = 30f;
        private const float Grip = 0.9f;   // the right fist, as XbowClips.Carried has it

        public static void Write(GameObject skeleton, XbowGrip grip, XbowPoser poser, float curled)
        {
            var stance = new XbowStance(skeleton, poser, grip);
            Animator animator = XbowReference.Animator(skeleton);
            var handler = new HumanPoseHandler(animator.avatar, animator.transform);
            stance.Rest();
            var rest = new HumanPose();
            handler.GetHumanPose(ref rest);
            foreach (var (name, state, _) in Clips)
                Write(name, state, (stance, poser, handler), rest.muscles, curled);
        }

        private static void Write(string name, string state, (XbowStance stance, XbowPoser poser, HumanPoseHandler handler) on, float[] rest, float curled)
        {
            var (stance, poser, handler) = on;
            float length = poser.Length(state);
            var track = new SlingTrack();
            int frames = Mathf.Max(2, Mathf.RoundToInt(length * Rate));
            for (int i = 0; i <= frames; i++)
            {
                float time = length * i / frames;
                poser.Pose(state, i == frames ? 0f : time);
                stance.Carry();
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                XbowAuthor.CloseFingers(pose.muscles, rest, Grip, curled);
                track.Add(time, pose);
            }
            Save(track.ToClip(name), XbowReference.Clip(state), length, XbowBuild.ClipPath(name));
        }

        private static void Save(AnimationClip clip, AnimationClip game, float length, string path)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(game);
            settings.stopTime = length;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            Log.Info($"carry clip {clip.name}: {clip.length:F3} s, loop {settings.loopTime}, from {game.name}");
        }
    }
}
