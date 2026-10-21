using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// The slinger's shot as a humanoid clip, authored on the Greydwarf's own skeleton: each key is posed by
    /// <see cref="SlingPoser"/>, read back as muscles, and keyed. Humanoid, so the game's Greydwarf plays it through its
    /// own avatar when the mod puts it in place of the "throw" state's clip. It starts and ends on the idle's first
    /// frame, raises the slingshot while the right hand reaches back into the satchel, takes a stone, loads it into the
    /// pouch, draws to the cheek, holds, lets go (the game's OnAttackTrigger fires there and the stone leaves) and
    /// recovers.
    ///
    /// The mod times the stone and the pouch by the clip (EliteCreaturesPack SlingRig): a stone in the right hand from
    /// <see cref="Reach"/> to <see cref="Load"/>, then in the pouch, which rides the right hand, until
    /// <see cref="Release"/> (as fractions of <see cref="Length"/>).
    /// </summary>
    public static class SlingClip
    {
        public const string Name = "ecr_sling_shot";
        public const float Length = 2.4f;
        public const float Reach = 0.62f;      // the fingers close on a stone in the satchel
        public const float Load = 0.92f;       // the stone goes into the pouch, and the pouch into the fingers
        public const float Release = 1.52f;
        public const float FullDraw = 1.45f;

        public static readonly SlingKey[] Keys =
        {
            new SlingKey(0.00f),
            new SlingKey(0.25f) { Yaw = 6, LeftReach = 0.45f, LeftRaise = -0.25f, Fist = 1f, Right = RightHand.ToSatchel, Pinch = -0.3f },
            new SlingKey(0.48f) { Yaw = 4, LeftReach = 0.55f, LeftRaise = -0.18f, Fist = 1f, Right = RightHand.InSatchel, Pinch = -0.4f },
            new SlingKey(Reach) { Yaw = 4, LeftReach = 0.6f, LeftRaise = -0.15f, Fist = 1f, Right = RightHand.InSatchel, Pinch = 0.8f },
            new SlingKey(0.78f) { Yaw = 14, LeftReach = 0.8f, LeftRaise = -0.02f, Fist = 1f, Right = RightHand.Lift, Pinch = 0.8f },
            new SlingKey(Load) { Yaw = 24, LeftReach = 0.9f, LeftRaise = 0.02f, Right = RightHand.Pouch, Fist = 1f, Pinch = 0.7f },
            new SlingKey(1.30f) { Yaw = 30, LeftReach = 0.93f, LeftRaise = 0.05f, Right = RightHand.Cheek, Fist = 1f, Pinch = 0.8f },
            new SlingKey(1.46f) { Yaw = 31, LeftReach = 0.94f, LeftRaise = 0.05f, Right = RightHand.FullDraw, Fist = 1f, Pinch = 0.8f },
            new SlingKey(1.54f) { Yaw = 30, LeftReach = 0.92f, LeftRaise = 0.07f, Right = RightHand.Released, Fist = 1f, Pinch = -0.6f },
            new SlingKey(1.74f) { Yaw = 26, LeftReach = 0.85f, LeftRaise = 0f, Right = RightHand.FollowThrough, Fist = 1f, Pinch = -0.4f },
            new SlingKey(2.06f) { Yaw = 10, LeftReach = 0.5f, LeftRaise = -0.25f, Fist = 0.8f },
            new SlingKey(Length),
        };

        private static readonly Regex Finger = new Regex(@"^(Left|Right) (Thumb|Index|Middle|Ring|Little) (.+)$");
        private static readonly Regex Curl = new Regex(@"^(Left|Right) (Index|Middle|Little) \d Stretched$");

        /// <summary>
        /// The clip curve for a muscle: HumanTrait's own name, except the fingers, which clips name
        /// "LeftHand.Index.1 Stretched" where HumanTrait says "Left Index 1 Stretched".
        /// </summary>
        public static string CurveName(string muscle)
        {
            Match match = Finger.Match(muscle);
            return match.Success ? $"{match.Groups[1].Value}Hand.{match.Groups[2].Value}.{match.Groups[3].Value}" : muscle;
        }

        public static AnimationClip Build(GameObject greydwarf, AnimationClip idle, AnimationClip throwClip, string path)
        {
            var poser = new SlingPoser(greydwarf, idle);
            var handler = new HumanPoseHandler(greydwarf.GetComponent<Animator>().avatar, greydwarf.transform);
            float curled = CurlSign(poser, handler);
            var track = new SlingTrack();
            HumanPose rest = default;
            foreach (SlingKey key in Keys)
            {
                poser.Pose(key);
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                if (key.Time == 0f)
                    rest = pose;
                CloseFingers(pose.muscles, rest.muscles, key, curled);
                track.Add(key.Time, pose);
            }
            return Save(track.ToClip(Name), throwClip, path);
        }

        /// <summary>Which way a finger's "Stretched" muscle curls it on this avatar: the sign that bends it more.</summary>
        private static float CurlSign(SlingPoser poser, HumanPoseHandler handler)
        {
            float Bend(float value)
            {
                poser.Pose(Keys[0]);
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                pose.muscles[Array.IndexOf(HumanTrait.MuscleName, "Left Index 1 Stretched")] = value;
                handler.SetHumanPose(ref pose);
                Vector3 knuckle = poser.Bone("l_index1").position;
                return Vector3.Angle(knuckle - poser.Bone("l_hand").position, poser.Bone("l_index2").position - knuckle);
            }
            float sign = Bend(-1f) > Bend(1f) ? -1f : 1f;
            Log.Info($"fingers curl towards {sign:+0;-0}");
            return sign;
        }

        private static void CloseFingers(float[] muscles, float[] rest, SlingKey key, float curled)
        {
            for (int i = 0; i < muscles.Length; i++)
            {
                Match match = Curl.Match(HumanTrait.MuscleName[i]);
                if (!match.Success)
                    continue;
                float amount = match.Groups[1].Value == "Left" ? key.Fist : key.Pinch;
                muscles[i] = Mathf.Clamp(Mathf.LerpUnclamped(rest[i], curled * 0.9f, amount), -1f, 1f);
            }
        }

        private static AnimationClip Save(AnimationClip clip, AnimationClip throwClip, string path)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(throwClip);
            settings.stopTime = Length;
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = Release, functionName = "OnAttackTrigger" } });
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            Log.Info($"clip {Name}: {clip.length:F2} s, human {clip.humanMotion}, {AnimationUtility.GetCurveBindings(clip).Length} curves, "
                     + $"reach {Reach / Length:F3}, load {Load / Length:F3}, release {Release / Length:F3} of its length");
            return clip;
        }
    }
}
