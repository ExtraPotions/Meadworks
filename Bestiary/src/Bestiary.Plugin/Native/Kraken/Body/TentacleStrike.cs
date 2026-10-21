// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>What a tentacle blow is: at the crew, at the ship itself, or the special grab.</summary>
    public enum StrikeKind
    {
        Slam = 0,    // across the deck at a player (the tentacle phase)
        Smash = 1,   // across the ship, which takes damage (the head phase)
        Grab = 2,    // the special: a warning roar, and whoever it lands on is hoisted up and flung into the sea
    }

    /// <summary>
    /// One tentacle blow, as every machine plays it from the moment the owner's order arrives: the tentacle rears up high
    /// and back, curled over towards its target (the warning), hangs there, whips down across the ship along the line to
    /// the target (the base leading, the tip snapping down last, <see cref="TentacleWhip"/>), then lies writhing on the
    /// deck (where it can be hit) and lifts back to wherever it was. A grab hangs there longer, trembling, and after it
    /// lands it hoists its tip up and out over the water and flings it down: whoever it caught goes with it
    /// (<see cref="KrakenHeld"/>). A flinch (the kraken staggered) before the blow lands pulls the tentacle back.
    /// </summary>
    public class TentacleStrike
    {
        public const float Raise = 0.6f;
        private const float Recoil = 0.6f;
        private static readonly CurlShape Tremble = new CurlShape { Sway = 7f, SideSway = 7f, Pace = 7f };
        private static readonly CurlShape Hoisted = new CurlShape { Lean = -40f, Curl = 55f, CurlFrom = 0.3f, BaseDepth = 0.6f, Pace = 1f };
        private static readonly CurlShape Flung = new CurlShape { Lean = -85f, Curl = 25f, CurlFrom = 0.4f, BaseDepth = 0.6f, Pace = 1f };

        private readonly float _start = Time.time;
        private readonly DeckProfile _deck;
        private float _cancelled = -1f;

        public TentacleStrike(int tentacle, Vector3 target, StrikeKind kind, DeckProfile deck, Vector3[] from)
        {
            Tentacle = tentacle;
            Target = target;
            Kind = kind;
            _deck = deck;
            From = TentacleChain.New();
            TentacleChain.Copy(from, From);
        }

        public int Tentacle { get; }

        /// <summary>The point it strikes towards, in the ship's space.</summary>
        public Vector3 Target { get; }

        public StrikeKind Kind { get; }

        /// <summary>The pose it started from (or, once flinched, the pose it pulls back from).</summary>
        public Vector3[] From { get; }

        /// <summary>Whether its blow has been dealt (or can no longer be).</summary>
        public bool Struck { get; set; }

        public float Age => Time.time - _start;

        /// <summary>When it stops hanging and whips down.</summary>
        public float Hold => HoldOf(Kind);

        /// <summary>When it lands.</summary>
        public float Impact => Hold + 0.2f;

        /// <summary>When a grab lets go of whoever it holds, flinging them out; when a blow starts to lift.</summary>
        public float Release => Impact + (Kind == StrikeKind.Grab ? 0.8f : 1.2f);

        public bool Done => _cancelled >= 0f ? Time.time - _cancelled >= Recoil : Age >= EndOf(Kind);

        /// <summary>Whether the blow lands this frame: its moment has come and it was neither dealt nor called off.</summary>
        public bool Landing => !Struck && _cancelled < 0f && Age >= Impact;

        /// <summary>Seconds a blow of this kind lasts, from the order to back at rest.</summary>
        public static float EndOf(StrikeKind kind) => HoldOf(kind) + (kind == StrikeKind.Grab ? 2.1f : 2.0f);

        private static float HoldOf(StrikeKind kind) => kind == StrikeKind.Grab ? 1.5f : 1.0f;

        /// <summary>Called off before it lands; it pulls back from <paramref name="shown"/>.</summary>
        public void Flinch(Vector3[] shown)
        {
            if (Struck || _cancelled >= 0f || Age >= Impact)
            {
                return;
            }
            _cancelled = Time.time;
            TentacleChain.Copy(shown, From);
        }

        /// <summary>The pose now; <paramref name="rest"/> is where the tentacle goes back to, <paramref name="a"/> and <paramref name="b"/> scratch.</summary>
        public void Pose(ShipScene scene, float scale, float time, Vector3[] rest, Vector3[] into, Vector3[] a, Vector3[] b)
        {
            float age = Age;
            TentacleFrame frame = Frame(scene, scale);
            if (_cancelled >= 0f)
            {
                Recoiling(rest, scale, into);
            }
            else if (age < Hold)
            {
                Reared(frame, age, time, b);
                TentacleWhip.Blend(frame, From, b, Ease.InOut(Ease.Span(age, 0f, Raise)), 0.3f, into);
            }
            else if (age < Release)
            {
                Reared(frame, age, time, a);
                Lying(frame, age, time, b);
                TentacleWhip.Blend(frame, a, b, Ease.In(Ease.Span(age, Hold, Impact)), 0.45f, into);
                Hoisting(frame, age, time, into, a, b);
            }
            else
            {
                After(frame, age, time, rest, into, a, b);
            }
        }

        /// <summary>The frame of the blow: from the tentacle's base towards its target.</summary>
        public TentacleFrame Frame(ShipScene scene, float scale) => scene.Frame(Tentacle, Target, scale);

        // High and back, curled over the target; a grab trembles while it hangs there.
        private void Reared(TentacleFrame frame, float age, float time, Vector3[] into)
        {
            CurlShape shape = TentacleCurl.Raised.For(Tentacle);
            if (Kind == StrikeKind.Grab)
            {
                (shape.Sway, shape.SideSway, shape.Pace, shape.Curl) = (Tremble.Sway, Tremble.SideSway, Tremble.Pace, shape.Curl + 12f);
            }
            TentacleCurl.Build(frame, shape, time, Tentacle, into);
        }

        // A grab, once it has landed: up and out over the water with whoever it caught.
        private void Hoisting(TentacleFrame frame, float age, float time, Vector3[] into, Vector3[] a, Vector3[] b)
        {
            if (Kind != StrikeKind.Grab || age < Impact)
            {
                return;
            }
            TentacleChain.Copy(into, a);
            TentacleCurl.Build(frame, Hoisted, time, Tentacle, b);
            TentacleWhip.Blend(frame, a, b, Ease.InOut(Ease.Span(age, Impact, Release)), 0.2f, into);
        }

        // After the blow: a slam or smash lifts back to rest; a grab flings its tip down at the sea, then goes back.
        private void After(TentacleFrame frame, float age, float time, Vector3[] rest, Vector3[] into, Vector3[] a, Vector3[] b)
        {
            float end = EndOf(Kind);
            if (Kind != StrikeKind.Grab)
            {
                Lying(frame, age, time, a);
                TentacleWhip.Blend(frame, a, rest, Ease.InOut(Ease.Span(age, Release, end)), 0.3f, into);
                return;
            }
            float fling = Release + 0.3f;
            TentacleCurl.Build(frame, age < fling ? Hoisted : Flung, time, Tentacle, a);
            TentacleCurl.Build(frame, Flung, time, Tentacle, b);
            TentacleWhip.Blend(frame, a, age < fling ? b : rest, age < fling ? Ease.Out(Ease.Span(age, Release, fling)) : Ease.InOut(Ease.Span(age, fling, end)), 0.3f, into);
        }

        // Called off: back from where it was to where it goes.
        private void Recoiling(Vector3[] rest, float scale, Vector3[] into)
        {
            TentacleChain.Lerp(From, rest, Ease.InOut(Ease.Span(Time.time, _cancelled, _cancelled + Recoil)), into);
            TentacleChain.Straighten(into, TentacleSpec.Segment * scale);
        }

        // On the deck, writhing a little once it has landed and until it lifts.
        private void Lying(TentacleFrame frame, float age, float time, Vector3[] into)
        {
            TentacleDeck.Build(frame, _deck, TentacleSpec.Length, into);
            float writhe = Ease.Span(age, Impact, Impact + 0.3f) * (1f - Ease.Span(age, Release - 0.3f, Release));
            TentacleDeck.Writhe(frame, writhe, time, into);
        }
    }
}
