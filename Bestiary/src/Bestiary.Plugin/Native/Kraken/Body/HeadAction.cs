// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>How far an attack moves the head from where it rests beside the ship.</summary>
    public struct HeadOffsets
    {
        public float Raise;     // metres up
        public float Forward;   // metres towards the target
        public float Lean;      // degrees forward at the neck
        public float Beak;      // 0 shut, 1 wide open
        public float Siphon;    // 0 at rest, 1 swollen
        public float Turn;      // 0 facing the ship, 1 facing the target

        public static HeadOffsets Lerp(HeadOffsets a, HeadOffsets b, float t) => new HeadOffsets
        {
            Raise = Mathf.Lerp(a.Raise, b.Raise, t), Forward = Mathf.Lerp(a.Forward, b.Forward, t),
            Lean = Mathf.Lerp(a.Lean, b.Lean, t), Beak = Mathf.Lerp(a.Beak, b.Beak, t),
            Siphon = Mathf.Lerp(a.Siphon, b.Siphon, t), Turn = Mathf.Lerp(a.Turn, b.Turn, t),
        };
    }

    /// <summary>
    /// One of the head's attacks, as every machine plays it from the moment the owner's order arrives. The bite: the
    /// head turns to its target, rears with the beak gaping, then lunges up and over the rail and snaps shut; for someone
    /// further in, the whole head throws itself over the rail and down onto the deck. The ink:
    /// the head turns and rears back while the siphon swells (the warning), then squirts, the siphon squeezing empty. A
    /// flinch (the kraken staggered) before the bite snaps or the ink flies pulls the head back without either.
    /// </summary>
    public class HeadAction
    {
        public enum Kind { Bite, Ink }

        public const float BiteWindup = 0.55f, BiteSnap = 0.8f, BiteHold = 1.1f, BiteEnd = 1.8f;
        public const float InkWindup = 1.0f, InkSqueezed = 1.3f, InkHold = 1.6f, InkEnd = 2.2f;
        public const float Reach = 2.2f;         // metres the head lunges for a bite at the rail
        public const float MinLunge = 1f;        // someone right at the rail, the head already close
        public const float MaxLunge = 5.5f;      // the whole head thrown over the rail at someone further in
        private const float Recoil = 0.5f;

        private static readonly HeadOffsets BiteReared = new HeadOffsets { Raise = 1.0f, Forward = -0.4f, Lean = -12f, Beak = 1f, Turn = 1f };
        private static readonly HeadOffsets BiteLunged = new HeadOffsets { Raise = 2.2f, Forward = 2.2f, Lean = 25f, Beak = 0f, Turn = 1f };
        private static readonly HeadOffsets InkReared = new HeadOffsets { Raise = 1.3f, Forward = -0.6f, Lean = -18f, Beak = 1f, Turn = 1f };
        private static readonly HeadOffsets InkSquirted = new HeadOffsets { Raise = 1.3f, Forward = -0.2f, Lean = -6f, Beak = 0.85f, Turn = 1f };

        private readonly float _start = Time.time;
        private float _cancelled = -1f;
        private HeadOffsets _atCancel;

        public HeadAction(Kind kind, Vector3 target)
        {
            What = kind;
            Target = target;
        }

        public Kind What { get; }

        /// <summary>What it attacks, in the ship's space.</summary>
        public Vector3 Target { get; }

        /// <summary>Whether its moment (the snap, the squirt) has been dealt, or can no longer be.</summary>
        public bool Fired { get; set; }

        /// <summary>Metres the head throws itself towards its target: set as a bite begins, from where the head is.</summary>
        public float Lunge { get; set; } = Reach;

        public float Age => Time.time - _start;

        public float Moment => What == Kind.Bite ? BiteSnap : InkWindup;

        /// <summary>Whether its moment comes this frame.</summary>
        public bool Firing => !Fired && _cancelled < 0f && Age >= Moment;

        public bool Done => _cancelled >= 0f ? Time.time - _cancelled >= Recoil : Age >= (What == Kind.Bite ? BiteEnd : InkEnd);

        public void Flinch()
        {
            if (Fired || _cancelled >= 0f)
            {
                return;
            }
            _atCancel = Offsets();
            _cancelled = Time.time;
        }

        public HeadOffsets Offsets()
        {
            if (_cancelled >= 0f)
            {
                return HeadOffsets.Lerp(_atCancel, default, Ease.InOut(Ease.Span(Time.time, _cancelled, _cancelled + Recoil)));
            }
            return What == Kind.Bite ? Bite(Age) : Ink(Age);
        }

        private HeadOffsets Bite(float age)
        {
            if (age < BiteWindup)
            {
                return HeadOffsets.Lerp(default, BiteReared, Ease.InOut(Ease.Span(age, 0f, BiteWindup)));
            }
            if (age < BiteHold)
            {
                HeadOffsets lunge = HeadOffsets.Lerp(BiteReared, Lunged(), Ease.Out(Ease.Span(age, BiteWindup, BiteSnap)));
                lunge.Beak = 1f - Ease.Span(age, BiteSnap - 0.08f, BiteSnap);
                return lunge;
            }
            return HeadOffsets.Lerp(Lunged(), default, Ease.InOut(Ease.Span(age, BiteHold, BiteEnd)));
        }

        // The lunge's far end: further in, the whole head rises higher over the rail and leans further down onto the deck.
        private HeadOffsets Lunged()
        {
            float extra = Mathf.Max(0f, Lunge - Reach);
            HeadOffsets lunged = BiteLunged;
            lunged.Forward = Lunge;
            lunged.Raise += extra * 0.4f;
            lunged.Lean = Mathf.Min(BiteLunged.Lean + extra * 4f, 40f);
            return lunged;
        }

        private static HeadOffsets Ink(float age)
        {
            if (age < InkWindup)
            {
                return HeadOffsets.Lerp(default, InkReared, Ease.InOut(Ease.Span(age, 0f, InkWindup)));
            }
            if (age < InkHold)
            {
                return HeadOffsets.Lerp(InkReared, InkSquirted, Ease.Out(Ease.Span(age, InkWindup, InkSqueezed)));
            }
            return HeadOffsets.Lerp(InkSquirted, default, Ease.InOut(Ease.Span(age, InkHold, InkEnd)));
        }
    }
}
