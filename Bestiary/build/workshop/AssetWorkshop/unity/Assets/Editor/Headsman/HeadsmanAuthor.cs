using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Workshop.Crossbow;
using Workshop.Slinger;

namespace Workshop.Headsman
{
    /// <summary>
    /// Writes the headsman's humanoid clips on the Skeleton's own avatar: each attack sampled from its keys 30 times a
    /// second, posed by <see cref="HeadsmanStance"/>, read back as muscles (the fingers set by the key) and keyed
    /// (<see cref="SlingTrack"/>); the carry idle, walk and run as the game's own clips frame by frame with both fists on
    /// the ready hold. Attack clips take the idle's settings (every root motion baked into the pose), except that the
    /// root keeps its original place rather than the centre of mass's, which moves with the arms. Arm, leg and back
    /// muscles a pose pushes past their limits are logged: the avatar clamps those when it plays.
    /// </summary>
    public sealed class HeadsmanAuthor
    {
        public const float Rate = 30f;

        public static readonly (string name, string state)[] CarryClips =
            { (HeadsmanMove.Prefix + "idle", "Idle"), (HeadsmanMove.Prefix + "walk", "Walk"), (HeadsmanMove.Prefix + "run", "Run") };

        private readonly HeadsmanStance stance;
        private readonly HumanPoseHandler handler;
        private readonly HeadsmanFingers fingers;

        public HeadsmanAuthor(GameObject skeleton, HeadsmanStance stance, HeadsmanFingers fingers)
        {
            this.stance = stance;
            this.fingers = fingers;
            Animator animator = XbowReference.Animator(skeleton);
            handler = new HumanPoseHandler(animator.avatar, animator.transform);
        }

        public AnimationClip Write(HeadsmanMove move, string path)
        {
            var track = new SlingTrack();
            var over = new Dictionary<string, float>();
            float[] previous = null;
            stance.NewClip();
            int frames = Mathf.CeilToInt(move.Length * Rate);
            for (int i = 0; i <= frames; i++)
            {
                float time = Mathf.Min(i / Rate, move.Length);
                HeadsmanPose pose = move.Keys.At(time);
                stance.Apply(pose);
                var human = new HumanPose();
                handler.GetHumanPose(ref human);
                fingers.Set(human.muscles, pose[Ch.Fingers], Mathf.Lerp(pose[Ch.Fingers], 0.2f, pose[Ch.LeftFree]));
                Unwrap(human.muscles, previous);
                Jumps(move, time, human.muscles, previous);
                previous = (float[])human.muscles.Clone();
                Over(human.muscles, over);
                if (i % 6 == 0)
                    Diagnose(move, time, human.muscles, pose.RootYaw + pose[Ch.Yaw]);
                track.Add(time, human);
            }
            stance.Rest();
            if (over.Count > 0)
                Log.Info($"{move.Clip}: past the limits: {string.Join(", ", over.OrderByDescending(o => o.Value).Take(8).Select(o => $"{o.Key} {o.Value:F2}"))}");
            return Save(track.ToClip(move.Clip), XbowReference.Clip("Idle"), move.Length, Events(move), path, true);
        }

        /// <summary>A carry clip: the game's clip for `state`, frame by frame as the Animator plays it, both fists on the ready hold.</summary>
        public AnimationClip Carry(string name, string state, XbowPoser poser, string path)
        {
            float length = poser.Length(state);
            var track = new SlingTrack();
            stance.NewClip();
            int frames = Mathf.Max(2, Mathf.RoundToInt(length * Rate));
            for (int i = 0; i <= frames; i++)
            {
                float time = length * i / frames;
                poser.Pose(state, i == frames ? 0f : time);
                stance.Carry();
                var human = new HumanPose();
                handler.GetHumanPose(ref human);
                fingers.Set(human.muscles, 1f);
                track.Add(time, human);
            }
            return Save(track.ToClip(name), XbowReference.Clip(state), length, Array.Empty<AnimationEvent>(), path, false);
        }

        /// <summary>The game's hit moment (OnAttackTrigger) at the strike, the scrape or the throw.</summary>
        private static AnimationEvent[] Events(HeadsmanMove move)
        {
            float hit = move.Impact >= 0f ? move.Impact : move.Release >= 0f ? move.Release : move.Scrape.x;
            return hit < 0f ? Array.Empty<AnimationEvent>() : new[] { new AnimationEvent { time = hit, functionName = "OnAttackTrigger" } };
        }

        private static readonly string[] Watched =
        {
            "Left Hand In-Out", "Left Hand Down-Up", "Left Forearm Twist In-Out", "Right Hand In-Out", "Right Hand Down-Up", "Right Forearm Twist In-Out",
        };

        /// <summary>Every 0.2 s: how far each fist falls short of its seat, and the wrists' muscles.</summary>
        private void Diagnose(HeadsmanMove move, float time, float[] muscles, float turn)
        {
            string wrists = string.Join(" ", Watched.Select(m => muscles[Array.IndexOf(HumanTrait.MuscleName, m)].ToString("F1")));
            Log.Info($"  {move.Name} {time:0.00}: pulled {stance.Pulled * 100f:F0} cm, short R {stance.Short("Right") * 100f:F0} L {stance.Short("Left") * 100f:F0}; "
                     + $"shoulders {stance.Shoulders(turn):F2}; wrists L io/du/tw R io/du/tw {wrists}");
        }

        /// <summary>
        /// Keeps each twist muscle on the side of a full turn nearest the last frame's. A twist is a turn about the
        /// bone, so 180 degrees one way is 180 the other; read back from a pose, a forearm twisted past half a turn
        /// comes out on the far side, and the clip would spin the hand a whole turn between two frames.
        /// </summary>
        private static void Unwrap(float[] muscles, float[] previous)
        {
            if (previous == null)
                return;
            for (int i = 0; i < muscles.Length; i++)
            {
                if (!HumanTrait.MuscleName[i].Contains("Twist"))
                    continue;
                float turn = 360f / Mathf.Max(HumanTrait.GetMuscleDefaultMax(i), -HumanTrait.GetMuscleDefaultMin(i));
                muscles[i] += Mathf.Round((previous[i] - muscles[i]) / turn) * turn;
            }
        }

        /// <summary>Logs muscles that change by more than 0.6 between two frames (a pose the clip would blur through).</summary>
        private static void Jumps(HeadsmanMove move, float time, float[] muscles, float[] previous)
        {
            if (previous == null)
                return;
            for (int i = 0; i < muscles.Length; i++)
                if (Mathf.Abs(muscles[i] - previous[i]) > 0.6f && !HumanTrait.MuscleName[i].Contains("Stretched"))
                    Log.Info($"  jump {move.Name} {time:0.000}: {HumanTrait.MuscleName[i]} {previous[i]:F2} -> {muscles[i]:F2}");
        }

        private static void Over(float[] muscles, Dictionary<string, float> over)
        {
            for (int i = 0; i < muscles.Length; i++)
            {
                string name = HumanTrait.MuscleName[i];
                if (Mathf.Abs(muscles[i]) <= 1.02f || name.Contains("Stretched") || name.Contains("Spread"))
                    continue;
                over[name] = Mathf.Max(over.TryGetValue(name, out float v) ? v : 0f, Mathf.Abs(muscles[i]));
            }
        }

        private static AnimationClip Save(AnimationClip clip, AnimationClip game, float length, AnimationEvent[] events, string path, bool attack)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(game);
            settings.stopTime = length;
            if (attack)
            {
                settings.loopTime = false;
                settings.keepOriginalPositionXZ = true;
            }
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, events);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            Log.Info($"clip {clip.name}: {clip.length:F3} s, loop {settings.loopTime}, human {clip.humanMotion}");
            return clip;
        }
    }
}
