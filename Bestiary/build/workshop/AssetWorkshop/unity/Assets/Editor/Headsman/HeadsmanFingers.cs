using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// The fingers as muscles: which way this avatar's "Stretched" finger muscles curl a finger (measured once), and a
    /// pose's fingers set from the idle's towards closed (1), or spread open below 0. Both hands alike.
    /// </summary>
    public sealed class HeadsmanFingers
    {
        private static readonly Regex Curl = new Regex(@"^(Left|Right) (Index|Middle|Ring|Little) \d Stretched$");
        private readonly float[] rest;
        private readonly float curled;

        public HeadsmanFingers(HeadsmanStance stance, HumanPoseHandler handler)
        {
            stance.Rest();
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            rest = (float[])pose.muscles.Clone();
            curled = Bend(stance, handler, -1f) > Bend(stance, handler, 1f) ? -1f : 1f;
            stance.Rest();
        }

        public void Set(float[] muscles, float amount) => Set(muscles, amount, amount);

        /// <summary>The right hand's fingers by `right`, the left's by `left`.</summary>
        public void Set(float[] muscles, float right, float left)
        {
            for (int i = 0; i < muscles.Length; i++)
            {
                Match match = Curl.Match(HumanTrait.MuscleName[i]);
                if (match.Success)
                {
                    float amount = match.Groups[1].Value == "Left" ? left : right;
                    muscles[i] = Mathf.Clamp(Mathf.LerpUnclamped(rest[i], curled * 0.9f, amount), -1f, 1f);
                }
            }
        }

        /// <summary>Closes both fists on the idle's first frame, where the grips are measured.</summary>
        public void CloseOnRest(HeadsmanStance stance, HumanPoseHandler handler)
        {
            stance.Rest();
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            Set(pose.muscles, 1f);
            handler.SetHumanPose(ref pose);
        }

        private static float Bend(HeadsmanStance stance, HumanPoseHandler handler, float value)
        {
            stance.Rest();
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            pose.muscles[Array.IndexOf(HumanTrait.MuscleName, "Left Index 1 Stretched")] = value;
            handler.SetHumanPose(ref pose);
            Vector3 knuckle = stance.Bone("LeftHandIndex1").position;
            return Vector3.Angle(knuckle - stance.Bone("LeftHand").position, stance.Bone("LeftHandIndex2").position - knuckle);
        }
    }
}
