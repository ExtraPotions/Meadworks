using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// The keys of one headsman clip, each part of the pose on a curve of its own (clamped-auto tangents, so holds stay
    /// still and nothing overshoots), sampled into a <see cref="HeadsmanPose"/> at any time. The axe is keyed as its
    /// grip and its turn (a quaternion, each key kept on the same side of the sphere as the one before and normalised
    /// after interpolation, as Unity's own rotation curves are): keys must come in time order and less than half a
    /// turn apart. Feet stay planted except for their steps: a step lifts a foot and sets it down at the idle's
    /// place plus an offset, both turned about the creature's origin by the step's turn.
    /// </summary>
    public sealed class HeadsmanKeys
    {
        private sealed class Stride
        {
            public float Lift, Land, Turn;
            public Vector3 Offset;
        }

        private const float StepHeight = 0.13f;
        private readonly AnimationCurve[] channels = Enumerable.Range(0, Enum.GetValues(typeof(Ch)).Length).Select(_ => new AnimationCurve()).ToArray();
        private readonly AnimationCurve[] axe = Curves(7);
        private Quaternion lastTurn = Quaternion.identity;
        private readonly AnimationCurve root = new AnimationCurve();
        private readonly AnimationCurve[] leftHand = Curves(3);
        private readonly List<Stride> left = new List<Stride>(), right = new List<Stride>();

        public HeadsmanKeys Key(Ch channel, params (float time, float value)[] keys)
        {
            foreach (var (time, value) in keys)
                channels[(int)channel].AddKey(time, value);
            return this;
        }

        public HeadsmanKeys Axe(float time, AxeKey key)
        {
            Quaternion q = key.Turn;
            if (Quaternion.Dot(q, lastTurn) < 0f)
                q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            lastTurn = q;
            float[] v = { key.Grip.x, key.Grip.y, key.Grip.z, q.x, q.y, q.z, q.w };
            for (int i = 0; i < v.Length; i++)
                axe[i].AddKey(time, v[i]);
            return this;
        }

        /// <summary>Where the left hand goes while it is off the axe (body frame; <see cref="Ch.LeftFree"/> weighs it in).</summary>
        public HeadsmanKeys LeftHand(float time, Vector3 at)
        {
            for (int i = 0; i < 3; i++)
                leftHand[i].AddKey(time, at[i]);
            return this;
        }

        /// <summary>The creature's own turn in degrees (the mod turns the creature by it while the clip plays).</summary>
        public HeadsmanKeys Root(params (float time, float degrees)[] keys)
        {
            foreach (var (time, degrees) in keys)
                root.AddKey(time, degrees);
            return this;
        }

        /// <summary>A step: the foot lifts at `lift` and lands at `land` on its idle place plus `offset`, turned by `turn`.</summary>
        public HeadsmanKeys Step(bool leftFoot, float lift, float land, Vector3 offset, float turn = 0f)
        {
            (leftFoot ? left : right).Add(new Stride { Lift = lift, Land = land, Offset = offset, Turn = turn });
            (leftFoot ? left : right).Sort((a, b) => a.Land.CompareTo(b.Land));
            return this;
        }

        /// <summary>Makes every curve smooth: clamped-auto tangents on every key.</summary>
        public HeadsmanKeys Done()
        {
            foreach (AnimationCurve curve in channels.Concat(axe).Concat(leftHand).Append(root))
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }
            return this;
        }

        public float RootYaw(float time) => root.length == 0 ? 0f : root.Evaluate(time);

        public HeadsmanPose At(float time)
        {
            var pose = new HeadsmanPose { RootYaw = RootYaw(time) };
            foreach (Ch channel in Enum.GetValues(typeof(Ch)))
                pose[channel] = channels[(int)channel].length == 0 ? Default(channel) : channels[(int)channel].Evaluate(time);
            Vector3 V(AnimationCurve[] c, int at) => new Vector3(c[at].Evaluate(time), c[at + 1].Evaluate(time), c[at + 2].Evaluate(time));
            pose.Axe = axe[0].length == 0 ? HeadsmanRest.Ready : AxeKey.From(V(axe, 0), Turn(time));
            if (leftHand[0].length > 0)
                pose.LeftHand = V(leftHand, 0);
            (pose.LeftFoot, pose.LeftLift) = Foot(left, HeadsmanRest.LeftAnkle, time);
            (pose.RightFoot, pose.RightLift) = Foot(right, HeadsmanRest.RightAnkle, time);
            return pose;
        }

        private Quaternion Turn(float time)
        {
            var q = new Quaternion(axe[3].Evaluate(time), axe[4].Evaluate(time), axe[5].Evaluate(time), axe[6].Evaluate(time));
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length);
        }

        private static float Default(Ch channel) => channel == Ch.Fingers ? 1f : 0f;

        private static (Plant, float) Foot(List<Stride> steps, Vector3 rest, float time)
        {
            var from = new Plant(rest, 0f);
            foreach (Stride step in steps)
            {
                var to = new Plant(Quaternion.AngleAxis(step.Turn, Vector3.up) * (rest + step.Offset), step.Turn);
                if (time >= step.Land)
                {
                    from = to;
                    continue;
                }
                if (time <= step.Lift)
                    return (from, 0f);
                float u = Mathf.SmoothStep(0f, 1f, (time - step.Lift) / (step.Land - step.Lift));
                return (new Plant(Vector3.Lerp(from.At, to.At, u), Mathf.Lerp(from.Turn, to.Turn, u)), StepHeight * Mathf.Sin(Mathf.PI * u));
            }
            return (from, 0f);
        }

        private static AnimationCurve[] Curves(int count) => Enumerable.Range(0, count).Select(_ => new AnimationCurve()).ToArray();
    }
}
