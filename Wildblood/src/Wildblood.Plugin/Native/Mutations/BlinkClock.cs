// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Blinking's rhythm, kept on the owner: it counts seconds of combat only, and one blink falls due every `every` of
    /// them. A fight's first blink comes after a random quarter to three quarters of the interval rather than the whole
    /// of it, so it lands inside an ordinary fight instead of after it, and a pack of blinkers never blinks in unison.
    /// A lull (a target switch, a moment out of sight) only pauses the count; <see cref="ResetAfter"/> seconds out of
    /// combat end the fight, and the next one starts with a fresh first draw. Each tell is stamped in the creature's
    /// ZDO on the shared clock, so whichever machine owns it - the same one after a lull, or a new owner after a
    /// hand-over - never sends another until `every` seconds after the last one went out. No double blinks, and the new
    /// owner carries on.
    /// </summary>
    internal sealed class BlinkClock
    {
        /// <summary>ZDO key: when the last tell went out, in shared-clock milliseconds.</summary>
        private const string LastTellKey = Traits.TraitKeys.BlinkedAt;

        private const float FirstMin = 0.25f;
        private const float FirstMax = 0.75f;

        /// <summary>Seconds out of combat that end a fight; anything shorter is a lull that only pauses.</summary>
        private const float ResetAfter = 10f;

        private readonly float _every;
        private float _combat;
        private float _due;
        private float _idle;
        private bool _primed;

        /// <summary>An interval of zero or less switches the blink off.</summary>
        public BlinkClock(float every) => _every = every;

        /// <summary>One frame on the owner; true when a blink is due now.</summary>
        public bool Tick(bool inCombat, ZDO zdo, float dt)
        {
            if (_every <= 0f || zdo == null)
            {
                return false;
            }
            if (!inCombat)
            {
                _idle += dt;
                _primed = _primed && _idle < ResetAfter; // a long enough break ends the fight
                return false;
            }
            _idle = 0f;
            if (!_primed)
            {
                Prime(zdo);
            }
            _combat += dt;
            return _combat >= _due;
        }

        // A fight begins: a random first delay, never sooner than `every` after the last tell anyone sent.
        private void Prime(ZDO zdo)
        {
            _primed = true;
            _combat = 0f;
            _due = _every * Random.Range(FirstMin, FirstMax);
            long last = zdo.GetLong(LastTellKey);
            if (last > 0L)
            {
                _due = Mathf.Max(_due, Mathf.Min(_every, _every - NetTime.SecondsSince(last)));
            }
        }

        /// <summary>A tell went out: stamp it for any later owner and start the next full interval.</summary>
        public void Spent(ZDO zdo)
        {
            zdo.Set(LastTellKey, NetTime.NowMs());
            _combat = 0f;
            _due = _every;
        }

        /// <summary>The moment was wrong or no spot would do: try again shortly, not a whole interval later.</summary>
        public void Retry(float delay) => _due = _combat + delay;

        /// <summary>Not the owner, or dead: forget the fight, so owning it again re-reads the stamp.</summary>
        public void Reset()
        {
            _primed = false;
            _idle = 0f;
        }
    }
}
