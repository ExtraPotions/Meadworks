using UnityEngine;
using static Workshop.Headsman.HeadsmanArcs;

namespace Workshop.Headsman
{
    /// <summary>
    /// The headsman's three close attacks, keyed in the turned body's frame (see <see cref="HeadsmanKeys"/>):
    ///   slam: the axe up over the head, cocked far back, then down with the whole body onto the target and into the
    ///     ground in front; the front foot steps in.
    ///   scrape: the body winds round to the right with the edge put to the ground, then drags it fast through an arc
    ///     in front from right to left and lifts it at the left. The rubble shockwave running out from the arc is what
    ///     hits (in the mod the wave, not the axe, deals the damage).
    ///   spin: winds up to the right, spins a full turn to the left with the axe held out flat, is carried past by the
    ///     weight, staggers, leans on the axe to catch itself and straightens.
    /// </summary>
    public static class HeadsmanMelee
    {
        private static readonly Vector3 Shoulders = new Vector3(0.03f, 1.5f, 0.02f);

        public static HeadsmanMove Slam()
        {
            var pivot = new Vector3(0.03f, 1.45f, 0.02f);
            var bowed = new Vector3(0.03f, 1.09f, 0.42f);
            AxeKey ready = HeadsmanRest.Ready;
            var keys = new HeadsmanKeys()
                .Axe(0f, ready)
                .Axe(0.45f, Chop(pivot, 0.42f, 5f, -70f))
                .Axe(0.85f, Chop(pivot + new Vector3(0f, 0f, -0.04f), 0.38f, -10f, -115f))
                .Axe(1.05f, Chop(pivot + new Vector3(0f, 0f, -0.05f), 0.38f, -12f, -120f))
                .Axe(1.18f, Chop(pivot + new Vector3(0f, -0.05f, 0.08f), 0.45f, 25f, -20f))
                .Axe(1.28f, Chop(Vector3.Lerp(pivot, bowed, 0.6f), 0.5f, 70f, 55f))
                .Axe(1.36f, Chop(bowed, 0.5f, 125f, 128f))
                .Axe(1.6f, Chop(bowed + new Vector3(0f, 0.02f, 0f), 0.5f, 123f, 126f))
                .Axe(1.95f, Chop(new Vector3(0.03f, 1.2f, 0.35f), 0.5f, 95f, 90f))
                .Axe(2.75f, ready)
                .Key(Ch.Lean, (0f, 0f), (0.45f, -6f), (0.85f, -12f), (1.05f, -13f), (1.18f, 5f), (1.28f, 28f), (1.36f, 50f), (1.6f, 48f), (1.95f, 28f), (2.35f, 10f), (2.75f, 0f))
                .Key(Ch.HipsY, (0f, 0f), (0.85f, -0.04f), (1.05f, -0.05f), (1.28f, -0.14f), (1.36f, -0.28f), (1.6f, -0.27f), (1.95f, -0.14f), (2.75f, 0f))
                .Key(Ch.HipsZ, (0f, 0f), (0.85f, -0.05f), (1.05f, -0.06f), (1.36f, 0.12f), (1.6f, 0.12f), (2.75f, 0f))
                .Key(Ch.Elbows, (0f, 0f), (0.45f, 0.8f), (0.85f, 1f), (1.18f, 0.9f), (1.36f, 0.2f), (1.95f, 0.3f), (2.75f, 0f))
                .Key(Ch.HeadPitch, (0f, 0f), (0.85f, -12f), (1.05f, -12f), (1.36f, -25f), (1.6f, -25f), (2.75f, 0f))
                .Step(true, 1.02f, 1.3f, new Vector3(0f, 0f, 0.18f))
                .Step(true, 2.1f, 2.5f, Vector3.zero)
                .Done();
            var move = new HeadsmanMove { Name = "slam", Title = "1 slam", Length = 2.75f, Keys = keys, Impact = 1.36f };
            move.Moments.Add((0.85f, "cocked"));
            move.Sounds.AddRange(new[] { (0.15f, "vocal"), (1.12f, "whoosh_short"), (1.36f, "impact_ground") });
            return move;
        }

        public static HeadsmanMove Scrape()
        {
            AxeKey Dig(float angle, float lift = 0f)
            {
                Quaternion round = Quaternion.AngleAxis(angle, Vector3.up);
                return Touch(Round(angle, 0.85f, 0.03f + lift), round * new Vector3(0f, -0.45f, 0.89f), round * new Vector3(-0.3f, -0.95f, 0f));
            }
            var keys = new HeadsmanKeys()
                .Axe(0f, HeadsmanRest.Ready)
                .Axe(0.2f, Level(55f, 1.3f, 0.45f, 25f))
                .Axe(0.38f, Dig(30f, 0.25f))
                .Axe(0.46f, Dig(25f))
                .Axe(0.52f, Dig(22f))
                .Axe(0.72f, Dig(5f))
                .Axe(0.92f, Dig(-15f))
                .Axe(1.05f, Dig(-25f, 0.35f))
                .Axe(1.35f, Level(-30f, 1.25f, 0.5f, 10f))
                .Axe(1.85f, HeadsmanRest.Ready)
                .Key(Ch.Yaw, (0f, 0f), (0.38f, 65f), (0.52f, 62f), (0.72f, 0f), (0.92f, -62f), (1.05f, -72f), (1.35f, -40f), (1.85f, 0f))
                .Key(Ch.Twist, (0f, 0f), (0.38f, 15f), (0.52f, 15f), (0.72f, 0f), (0.92f, -18f), (1.05f, -20f), (1.85f, 0f))
                .Key(Ch.Lean, (0f, 0f), (0.38f, 42f), (0.52f, 46f), (0.92f, 46f), (1.05f, 36f), (1.35f, 15f), (1.85f, 0f))
                .Key(Ch.HipsY, (0f, 0f), (0.38f, -0.2f), (0.52f, -0.22f), (0.92f, -0.22f), (1.05f, -0.15f), (1.85f, 0f))
                .Key(Ch.HeadYaw, (0f, 0f), (0.38f, -40f), (0.52f, -38f), (0.72f, 0f), (0.92f, 38f), (1.05f, 40f), (1.35f, 25f), (1.85f, 0f))
                .Key(Ch.HeadPitch, (0f, 0f), (0.38f, -20f), (0.92f, -20f), (1.85f, 0f))
                .Step(false, 0.05f, 0.3f, new Vector3(0.12f, 0f, 0.02f))
                .Step(false, 1.3f, 1.65f, Vector3.zero)
                .Done();
            var move = new HeadsmanMove { Name = "scrape", Title = "2 ground scrape", Length = 1.85f, Keys = keys, Scrape = new Vector2(0.48f, 0.95f) };
            move.Sounds.AddRange(new[] { (0.05f, "vocal"), (0.3f, "whoosh_heavy"), (0.46f, "grind") });   // no rumble: the mod's scrape is a plain sweep since 2026-09-29
            return move;
        }

        public static HeadsmanMove Spin()
        {
            var keys = new HeadsmanKeys()
                .Axe(0f, HeadsmanRest.Ready)
                .Axe(0.35f, Level(70f, 1.2f, 0.45f, 15f))
                .Axe(0.6f, Level(95f, 1.15f, 0.48f, 12f))
                .Axe(0.75f, Level(95f, 1.15f, 0.48f, 12f))
                .Axe(0.9f, Level(20f, 1.08f, 0.55f, 8f))
                .Axe(1.0f, Level(-20f, 1.05f, 0.58f, 6f))
                .Axe(1.55f, Level(-20f, 1.05f, 0.58f, 6f))
                .Axe(1.75f, Level(-45f, 0.95f, 0.5f, 30f))
                .Axe(2.05f, Level(-25f, 0.85f, 0.45f, 50f))
                .Axe(2.5f, Level(-5f, 0.9f, 0.42f, 48f))
                .Axe(2.9f, Level(-5f, 1.05f, 0.4f, 25f))
                .Axe(3.6f, HeadsmanRest.Ready);
            SpinTurn(keys);
            keys.Key(Ch.Side, (0f, 0f), (1.55f, -5f), (1.75f, -18f), (2.05f, -10f), (2.3f, 10f), (2.6f, -4f), (3.0f, 0f))
                .Key(Ch.Lean, (0f, 0f), (0.6f, 12f), (1.55f, 10f), (1.75f, 20f), (2.05f, 28f), (2.5f, 24f), (3.0f, 8f), (3.6f, 0f))
                .Key(Ch.HipsX, (0f, 0f), (1.55f, 0f), (1.75f, -0.1f), (2.05f, -0.06f), (2.3f, 0.05f), (2.6f, -0.02f), (3.0f, 0f))
                .Key(Ch.HipsY, (0f, 0f), (0.6f, -0.08f), (1.55f, -0.08f), (2.05f, -0.16f), (2.5f, -0.12f), (3.6f, 0f))
                .Key(Ch.Twist, (0f, 0f), (0.6f, 20f), (0.8f, 20f), (0.95f, -15f), (1.55f, -15f), (1.75f, -10f), (3.0f, 0f))
                .Key(Ch.HeadYaw, (0f, 0f), (0.6f, -30f), (0.8f, -30f), (1.0f, 15f), (1.55f, 20f), (2.05f, 0f))
                .Key(Ch.HeadPitch, (0f, 0f), (1.55f, 0f), (1.75f, 10f), (2.05f, 20f), (2.5f, 8f), (3.0f, 0f))
                .Step(true, 1.72f, 1.95f, new Vector3(-0.25f, 0f, 0.1f), -395f)
                .Step(false, 2.25f, 2.5f, new Vector3(0.05f, 0f, 0f), -385f)
                .Step(true, 2.9f, 3.2f, Vector3.zero, -360f)
                .Step(false, 3.1f, 3.4f, Vector3.zero, -360f)
                .Done();
            var move = new HeadsmanMove { Name = "spin", Title = "3 spin", Length = 3.6f, Keys = keys, Impact = 1.2f };   // the hit mid-sweep, all round
            move.Moments.Add((0.75f, "spin"));
            move.Moments.Add((1.75f, "catch balance"));
            move.Sounds.AddRange(new[] { (0.2f, "vocal"), (0.85f, "whoosh_spin") });
            return move;
        }

        /// <summary>The spin's turn: wound 58 degrees to the right, a full turn and a bit to the left, back to square.</summary>
        private static void SpinTurn(HeadsmanKeys keys)
        {
            keys.Key(Ch.Yaw, (0f, 0f), (0.35f, 30f), (0.6f, 55f), (0.75f, 58f), (0.85f, 30f), (0.95f, -30f), (1.05f, -95f), (1.15f, -160f),
                (1.25f, -225f), (1.35f, -285f), (1.45f, -335f), (1.55f, -370f), (1.75f, -395f), (2.05f, -392f), (2.5f, -378f), (3.0f, -365f), (3.6f, -360f));
            keys.Step(false, 0.8f, 0.95f, Vector3.zero, 0f)
                .Step(true, 0.92f, 1.07f, Vector3.zero, -90f)
                .Step(false, 1.05f, 1.2f, Vector3.zero, -170f)
                .Step(true, 1.18f, 1.33f, Vector3.zero, -250f)
                .Step(false, 1.3f, 1.45f, Vector3.zero, -320f)
                .Step(true, 1.45f, 1.62f, Vector3.zero, -370f);
        }
    }
}
