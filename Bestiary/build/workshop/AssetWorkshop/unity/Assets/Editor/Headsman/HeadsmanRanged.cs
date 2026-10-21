using UnityEngine;
using static Workshop.Headsman.HeadsmanArcs;

namespace Workshop.Headsman
{
    /// <summary>
    /// The headsman's two throws, for a target out of reach, and the rear strike:
    ///   hurl: the axe up over the head and cocked far back, the front foot steps in and the axe is thrown overhand
    ///     straight at the target, tumbling end over end.
    ///   spin throw: the spin's wind-up and turn, the axe let go flat as it comes round towards the target, a stumble.
    ///   Both then raise their empty hands over the head for 2 s while a new axe forms in them, and bring it down to
    ///     the ready hold. The hands keep the shape of holding it (the keys drive a phantom axe) and open after the throw.
    ///   rear: done the way only a skeleton can. The skull snaps all the way round, then the whole upper body wrings
    ///     round at the waist to follow it (both in code: the neck's and back's muscles stop far short), the hips and
    ///     legs still facing the old way; from there a two-handed chop at the target behind; then the legs hop round
    ///     under the still upper body until they face the same way (the creature's own turn, the upper body unwinding
    ///     as they go), the left hand off the axe for balance during the hop and back on it after with the hooked spike on the back of its head
    ///     (the edge still faces forward, so the fists never have to turn it over); then the feet step round while the
    ///     creature turns to face where its head looks, the head untwisting as it goes, the axe lifted over the head and
    ///     brought to the ready hold.
    /// </summary>
    public static class HeadsmanRanged
    {
        private static readonly Vector3 Shoulders = new Vector3(0.03f, 1.5f, 0.0f);
        private static readonly Vector3 Pivot = new Vector3(0.03f, 1.45f, 0.02f);

        /// <summary>
        /// Seconds the new axe takes to form in the raised hands; the skeleton where the thrown axe broke forms in the
        /// same window (<see cref="HeadsmanSummon"/>), so the two come into being together.
        /// </summary>
        public const float Forming = 2f;

        /// <summary>Both hands over the head holding the axe level, the head out to the right and the edge up.</summary>
        private static readonly AxeKey Raised = new AxeKey(new Vector3(0.2f, 1.97f, 0.1f), Vector3.right, Vector3.up);

        private static readonly AxeKey Lowering = new AxeKey(new Vector3(0.2f, 1.7f, 0.32f), new Vector3(0.8f, 0.6f, 0.1f), new Vector3(0f, 0.3f, 1f));

        public static HeadsmanMove Hurl()
        {
            var keys = new HeadsmanKeys()
                .Axe(0f, HeadsmanRest.Ready)
                .Axe(0.45f, Chop(Pivot, 0.42f, 5f, -70f))
                .Axe(0.8f, Chop(Pivot + new Vector3(0f, -0.02f, -0.08f), 0.38f, -15f, -125f))
                .Axe(0.95f, Chop(Pivot + new Vector3(0f, -0.03f, -0.1f), 0.38f, -18f, -130f))
                .Axe(1.05f, Chop(Pivot, 0.45f, 0f, -55f))
                .Axe(1.15f, Chop(Pivot + new Vector3(0f, -0.05f, 0.12f), 0.5f, 40f, 30f))
                .Axe(1.3f, Chop(new Vector3(0.03f, 1.2f, 0.35f), 0.55f, 95f, 100f))
                .Axe(1.7f, Chop(new Vector3(0.03f, 1.25f, 0.3f), 0.45f, 110f, 110f))
                .Axe(2.05f, Chop(new Vector3(0.03f, 1.45f, 0.15f), 0.4f, 40f, 80f));
            Rearm(keys, 2.35f);
            keys.Key(Ch.Lean, (0f, 0f), (0.45f, -8f), (0.8f, -18f), (0.95f, -19f), (1.05f, -5f), (1.15f, 12f), (1.3f, 32f), (1.7f, 24f), (2.05f, 5f))
                .Key(Ch.HipsZ, (0f, 0f), (0.8f, -0.08f), (0.95f, -0.09f), (1.3f, 0.14f), (1.7f, 0.12f), (2.35f, 0.02f), (5.1f, 0f))
                .Key(Ch.HipsY, (0f, 0f), (0.8f, -0.05f), (1.3f, -0.12f), (1.7f, -0.1f), (2.35f, 0f))
                .Key(Ch.HeadPitch, (0f, 0f), (0.8f, -10f), (1.15f, -5f), (1.3f, -15f), (1.7f, -10f))
                .Key(Ch.Elbows, (0f, 0f), (0.45f, 0.8f), (0.8f, 1f), (1.15f, 0.9f), (1.3f, 0.4f), (1.7f, 0.2f))
                .Key(Ch.Fingers, (0f, 1f), (1.12f, 1f), (1.22f, -0.2f))
                .Step(true, 0.95f, 1.2f, new Vector3(0f, 0f, 0.2f))
                .Step(true, 2.0f, 2.35f, Vector3.zero)
                .Done();
            var move = new HeadsmanMove
            {
                Name = "hurl", Title = "4 overhead throw", Length = 5.1f, Keys = keys,
                Release = 1.15f, Ghost = 2.35f, Solid = 2.35f + Forming, Flight = Flight.Tumble,
            };
            move.Moments.Add((2.1f, "hands up"));
            move.Sounds.AddRange(new[] { (0.2f, "vocal"), (1.02f, "whoosh_heavy"), (1.15f, "throw"), (2.25f, "vocal_raise"), (2.35f, "regen"), (2.35f + Forming, "solid") });
            return move;
        }

        public static HeadsmanMove SpinThrow()
        {
            var keys = new HeadsmanKeys()
                .Axe(0f, HeadsmanRest.Ready)
                .Axe(0.35f, Level(70f, 1.2f, 0.45f, 15f))
                .Axe(0.55f, Level(95f, 1.15f, 0.48f, 12f))
                .Axe(0.65f, Level(95f, 1.15f, 0.48f, 12f))
                .Axe(0.8f, Level(20f, 1.1f, 0.55f, 8f))
                .Axe(0.9f, Level(-20f, 1.08f, 0.58f, 5f))
                .Axe(1.2f, Level(-20f, 1.1f, 0.6f, 0f))
                .Axe(1.35f, Level(-45f, 1.15f, 0.55f, -5f))
                .Axe(1.7f, Level(-60f, 1.05f, 0.4f, 30f))
                .Axe(2.1f, Chop(new Vector3(0.03f, 1.45f, 0.15f), 0.4f, 40f, 80f));
            Rearm(keys, 2.4f);
            keys.Key(Ch.Yaw, (0f, 0f), (0.35f, 30f), (0.55f, 55f), (0.65f, 58f), (0.75f, 30f), (0.85f, -30f), (0.95f, -100f), (1.05f, -165f),
                    (1.15f, -230f), (1.25f, -290f), (1.35f, -340f), (1.5f, -375f), (1.7f, -385f), (2.1f, -365f), (2.4f, -360f))
                .Key(Ch.Twist, (0f, 0f), (0.55f, 20f), (0.7f, 20f), (0.85f, -15f), (1.35f, -15f), (1.7f, -5f), (2.4f, 0f))
                .Key(Ch.Side, (0f, 0f), (1.35f, 0f), (1.5f, -8f), (1.7f, -12f), (1.95f, 6f), (2.2f, 0f))
                .Key(Ch.Lean, (0f, 0f), (0.55f, 12f), (1.3f, 8f), (1.5f, 15f), (1.7f, 18f), (2.1f, 6f))
                .Key(Ch.HipsY, (0f, 0f), (0.55f, -0.08f), (1.3f, -0.08f), (1.7f, -0.1f), (2.4f, 0f))
                .Key(Ch.HeadYaw, (0f, 0f), (0.55f, -30f), (0.7f, -30f), (0.9f, 15f), (1.35f, 20f), (1.7f, 0f))
                .Key(Ch.HeadPitch, (0f, 0f), (1.7f, 0f))
                .Key(Ch.Elbows, (0f, 0f), (1.7f, 0.2f))
                .Key(Ch.Fingers, (0f, 1f), (1.17f, 1f), (1.27f, -0.2f))
                .Step(false, 0.7f, 0.85f, Vector3.zero, 0f)
                .Step(true, 0.82f, 0.97f, Vector3.zero, -90f)
                .Step(false, 0.95f, 1.1f, Vector3.zero, -170f)
                .Step(true, 1.08f, 1.23f, Vector3.zero, -250f)
                .Step(false, 1.2f, 1.35f, Vector3.zero, -320f)
                .Step(true, 1.4f, 1.62f, new Vector3(-0.15f, 0f, 0.05f), -380f)
                .Step(false, 1.9f, 2.15f, Vector3.zero, -360f)
                .Step(true, 2.1f, 2.4f, Vector3.zero, -360f)
                .Done();
            var move = new HeadsmanMove
            {
                Name = "spinthrow", Title = "5 spin throw", Length = 5.2f, Keys = keys,
                Release = 1.19f, Ghost = 2.4f, Solid = 2.4f + Forming, Flight = Flight.Disc,
            };
            move.Moments.Add((0.65f, "spin"));
            move.Sounds.AddRange(new[] { (0.15f, "vocal"), (0.75f, "whoosh_spin"), (1.19f, "throw"), (2.3f, "vocal_raise"), (2.4f, "regen"), (2.4f + Forming, "solid") });
            return move;
        }

        /// <summary>From `raised` the empty hands are up over the head for 1.5 s while the new axe forms, then bring it down.</summary>
        private static void Rearm(HeadsmanKeys keys, float raised)
        {
            float solid = raised + Forming;
            keys.Axe(raised, Raised)
                .Axe(raised + 0.75f, new AxeKey(Raised.Grip + new Vector3(0f, 0.03f, -0.02f), Raised.Haft, Raised.Edge))
                .Axe(solid, Raised)
                .Axe(solid + 0.3f, Lowering)
                .Axe(solid + 0.75f, HeadsmanRest.Ready)
                .Key(Ch.Lean, (raised, -6f), (solid, -8f), (solid + 0.75f, 0f))
                .Key(Ch.HeadPitch, (raised, -30f), (solid, -30f), (solid + 0.75f, 0f))
                .Key(Ch.Elbows, (raised, 1f), (solid, 1f), (solid + 0.3f, 0.5f), (solid + 0.75f, 0f))
                .Key(Ch.Fingers, (raised, -0.4f), (solid - 0.15f, -0.4f), (solid, 1f));
        }

        public static HeadsmanMove Rear()
        {
            var keys = new HeadsmanKeys()
                .Axe(0f, HeadsmanRest.Ready)
                .Axe(0.3f, HeadsmanRest.Ready)
                .Axe(0.42f, Chop(Pivot, 0.42f, 5f, -70f))
                .Axe(0.52f, Chop(Pivot + new Vector3(0f, 0f, -0.04f), 0.38f, -12f, -120f))
                .Axe(0.6f, Chop(Pivot + new Vector3(0f, -0.05f, 0.08f), 0.45f, 25f, -20f))
                .Axe(0.68f, Chop(Pivot + new Vector3(0f, -0.2f, 0.25f), 0.5f, 95f, 100f))
                .Axe(0.9f, Chop(Pivot + new Vector3(0f, -0.18f, 0.22f), 0.48f, 92f, 96f))
                .Axe(1.3f, HeadsmanRest.Ready)
                .Key(Ch.Lean, (0f, 0f), (0.3f, 0f), (0.42f, -5f), (0.52f, -10f), (0.6f, 5f), (0.68f, 22f), (0.9f, 18f), (1.3f, 0f))
                .Key(Ch.HipsY, (0f, 0f), (0.52f, -0.03f), (0.68f, -0.12f), (0.9f, -0.1f), (0.98f, -0.12f), (1.15f, 0.04f),
                    (1.33f, -0.07f), (1.55f, 0f))
                .Key(Ch.Elbows, (0f, 0f), (0.3f, 0.2f), (0.42f, 0.9f), (0.52f, 1f), (0.6f, 0.9f), (0.68f, 0.3f), (1.3f, 0f))
                .Key(Ch.LeftFree, (0f, 0f), (0.95f, 0f), (1.05f, 1f), (1.4f, 1f), (1.6f, 0f))
                .LeftHand(1.0f, new Vector3(-0.42f, 1.15f, 0.15f))
                .LeftHand(1.4f, new Vector3(-0.35f, 1.05f, 0.22f))
                .Root((0f, 0f), (0.95f, 0f), (1.35f, 180f))
                .Step(true, 1.0f, 1.32f, Vector3.zero, 180f)
                .Step(false, 1.0f, 1.32f, Vector3.zero, 180f)
                .Done();
            var move = new HeadsmanMove
            {
                Name = "rear", Title = "6 rear strike", Length = 1.9f, Keys = keys, Impact = 0.68f,
                HeadSpin = Smooth((0f, 0f), (0.05f, 0f), (0.3f, 180f), (0.5f, 0f)),
                TorsoSpin = Smooth((0f, 0f), (0.3f, 0f), (0.5f, 180f), (0.95f, 180f), (1.35f, 0f)),
            };
            move.Moments.Add((0.05f, "head turns"));
            move.Moments.Add((0.3f, "torso turns"));
            move.Moments.Add((0.95f, "legs turn"));
            move.Sounds.AddRange(new[] { (0.05f, "creak"), (0.3f, "creak"), (0.52f, "whoosh_heavy"), (0.68f, "impact_hit") });
            return move;
        }

        private static AnimationCurve Smooth(params (float time, float value)[] keys)
        {
            var curve = new AnimationCurve();
            foreach (var (time, value) in keys)
                curve.AddKey(time, value);
            for (int i = 0; i < curve.length; i++)
            {
                UnityEditor.AnimationUtility.SetKeyLeftTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.ClampedAuto);
                UnityEditor.AnimationUtility.SetKeyRightTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.ClampedAuto);
            }
            return curve;
        }
    }
}
