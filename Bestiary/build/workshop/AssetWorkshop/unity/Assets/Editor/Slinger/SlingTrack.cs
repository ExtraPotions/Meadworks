using UnityEditor;
using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// Humanoid poses collected over time and written as a clip: one curve per muscle (under the clip's own curve
    /// name), the body's position (RootT) and rotation (RootQ, kept on one side of the quaternion sphere so it never
    /// flips between keys). Keys are clamped-auto, so holds stay still and nothing overshoots.
    /// </summary>
    public sealed class SlingTrack
    {
        private readonly AnimationCurve[] muscles = new AnimationCurve[HumanTrait.MuscleCount];
        private readonly AnimationCurve[] root = new AnimationCurve[7];
        private Quaternion last = Quaternion.identity;

        public SlingTrack()
        {
            for (int i = 0; i < muscles.Length; i++)
                muscles[i] = new AnimationCurve();
            for (int i = 0; i < root.Length; i++)
                root[i] = new AnimationCurve();
        }

        public void Add(float time, HumanPose pose)
        {
            for (int i = 0; i < muscles.Length; i++)
                muscles[i].AddKey(time, pose.muscles[i]);
            Quaternion q = pose.bodyRotation;
            if (Quaternion.Dot(q, last) < 0f)
                q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            last = q;
            float[] values = { pose.bodyPosition.x, pose.bodyPosition.y, pose.bodyPosition.z, q.x, q.y, q.z, q.w };
            for (int i = 0; i < root.Length; i++)
                root[i].AddKey(time, values[i]);
        }

        public AnimationClip ToClip(string name)
        {
            var clip = new AnimationClip { name = name };
            for (int i = 0; i < muscles.Length; i++)
                Set(clip, SlingClip.CurveName(HumanTrait.MuscleName[i]), muscles[i]);
            string[] rootNames = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            for (int i = 0; i < root.Length; i++)
                Set(clip, rootNames[i], root[i]);
            return clip;
        }

        private static void Set(AnimationClip clip, string property, AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curve);
        }
    }
}
