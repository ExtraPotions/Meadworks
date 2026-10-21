using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.Crossbow
{
    /// <summary>
    /// Writes a crossbowman clip: each key posed by <see cref="XbowStance"/>, read back as muscles through the
    /// Skeleton's own avatar, its fingers closed (the left fist always round the fore-stock, the right by the key's Grip),
    /// and keyed (<see cref="SlingTrack"/>: one curve per muscle plus RootT/RootQ). Humanoid, so the game's Skeleton plays
    /// it through its own avatar in place of one of the archer's clips. Clip settings are the idle's (every root motion
    /// baked into the pose), since every key starts from the idle's first frame, except that the root keeps its original
    /// place rather than the centre of mass's (which moves with the arms and would shift a clip by its first key's).
    /// </summary>
    public sealed class XbowAuthor
    {
        private static readonly Regex Curl = new Regex(@"^(Left|Right) (Index|Middle|Ring|Little) \d Stretched$");

        private readonly XbowStance stance;
        private readonly HumanPoseHandler handler;
        private readonly float curled;

        public XbowAuthor(GameObject skeleton, XbowStance stance)
        {
            this.stance = stance;
            Animator animator = XbowReference.Animator(skeleton);
            handler = new HumanPoseHandler(animator.avatar, animator.transform);
            curled = CurlSign();
        }

        public AnimationClip Write(string name, XbowKey[] keys, float length, AnimationEvent[] events, string path)
        {
            var track = new SlingTrack();
            float[] rest = null;
            foreach (XbowKey key in keys)
            {
                stance.Apply(key);
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                rest ??= (float[])pose.muscles.Clone();
                CloseFingers(pose.muscles, rest, key.Grip, curled);
                Clamped(name, key, pose.muscles);
                track.Add(key.Time, pose);
            }
            return Save(track.ToClip(name), length, events, path);
        }

        /// <summary>Which way a finger's "Stretched" muscle curls it on this avatar: the sign that bends it more.</summary>
        private float CurlSign()
        {
            float Bend(float value)
            {
                stance.Apply(new XbowKey(0f));
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                pose.muscles[Array.IndexOf(HumanTrait.MuscleName, "Left Index 1 Stretched")] = value;
                handler.SetHumanPose(ref pose);
                Vector3 knuckle = stance.Bone("LeftHandIndex1").position;
                return Vector3.Angle(knuckle - stance.Bone("LeftHand").position, stance.Bone("LeftHandIndex2").position - knuckle);
            }
            float sign = Bend(-1f) > Bend(1f) ? -1f : 1f;
            Log.Info($"skeleton fingers curl towards {sign:+0;-0}");
            return sign;
        }

        /// <summary>Which way this avatar's "Stretched" finger muscles curl a finger.</summary>
        public float Curled => curled;

        /// <summary>The left fist closed; the right fingers from their rest towards closed by `grip`.</summary>
        public static void CloseFingers(float[] muscles, float[] rest, float grip, float curled)
        {
            for (int i = 0; i < muscles.Length; i++)
            {
                Match match = Curl.Match(HumanTrait.MuscleName[i]);
                if (!match.Success)
                    continue;
                float amount = match.Groups[1].Value == "Left" ? 1f : grip;
                muscles[i] = Mathf.Clamp(Mathf.LerpUnclamped(rest[i], curled * 0.9f, amount), -1f, 1f);
            }
        }

        /// <summary>Logs the arm and hand muscles a key pushes past their limits: the avatar clamps those when it plays.</summary>
        private static void Clamped(string clip, XbowKey key, float[] muscles)
        {
            string[] over = HumanTrait.MuscleName.Select((n, i) => (n, v: muscles[i]))
                .Where(m => Mathf.Abs(m.v) > 1.02f && !m.n.Contains("Stretched") && !m.n.Contains("Spread"))
                .Select(m => $"{m.n} {m.v:F2}").ToArray();
            if (over.Length > 0)
                Log.Info($"{clip} {key.Time:0.00}: past the limits: {string.Join(", ", over)}");
        }

        private static AnimationClip Save(AnimationClip clip, float length, AnimationEvent[] events, string path)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(XbowReference.Clip("Idle"));
            settings.stopTime = length;
            settings.loopTime = false;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, events);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            Log.Info($"clip {clip.name}: {clip.length:F3} s, human {clip.humanMotion}, {AnimationUtility.GetCurveBindings(clip).Length} curves");
            return clip;
        }
    }
}
