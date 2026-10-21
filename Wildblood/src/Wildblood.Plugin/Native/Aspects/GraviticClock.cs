// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Gravitic's rhythm, kept on the boss's owner. It counts only seconds of fighting - the boss alert with a player
    /// within reach of its pull - and a roar falls due every `every` of them, the first a whole interval into the fight,
    /// so a player always has a moment to learn the boss before it pulls. A lull only pauses the count;
    /// <see cref="ResetAfter"/> seconds out of the fight end it. Each roar is stamped in the boss's ZDO on the shared
    /// clock, and whichever machine owns the boss next - the same one after a lull, or a new owner after a hand-over -
    /// waits out the rest of that interval, so a hand-over neither roars twice nor starts the fight's count over.
    /// </summary>
    internal sealed class GraviticClock
    {
        private static readonly int GravityAtHash = TraitKeys.GravityAt.GetStableHashCode();

        /// <summary>Seconds out of the fight that end it; anything shorter is a lull that only pauses the count.</summary>
        private const float ResetAfter = 10f;

        private float _fought;
        private float _idle;
        private float _due;
        private bool _primed;

        /// <summary>One frame on the owner; true when a roar is due now.</summary>
        public bool Tick(bool fighting, float every, ZDO? zdo, float dt)
        {
            if (zdo == null)
            {
                return false;
            }
            if (!fighting)
            {
                _idle += dt;
                _primed = _primed && _idle < ResetAfter; // a long enough break ends the fight
                return false;
            }
            _idle = 0f;
            if (!_primed)
            {
                Prime(every, zdo);
            }
            _fought += dt;
            return _fought >= _due;
        }

        // A fight begins, or this machine has just taken the boss over: a whole interval, or what is left of the one
        // the last roar started when that roar was less than an interval ago.
        private void Prime(float every, ZDO zdo)
        {
            _primed = true;
            _fought = 0f;
            long last = zdo.GetLong(GravityAtHash);
            float since = last > 0L ? NetTime.SecondsSince(last) : float.MaxValue;
            _due = since < every ? Mathf.Clamp(every - since, 0f, every) : every;
        }

        /// <summary>A roar went out: stamp it for any later owner and start the next whole interval.</summary>
        public void Spent(ZDO zdo, float every)
        {
            zdo.Set(TraitKeys.GravityAt, NetTime.NowMs());
            _fought = 0f;
            _due = every;
        }

        /// <summary>Not the owner (any more), or dead: forget the fight, so owning it again re-reads the stamp.</summary>
        public void Reset()
        {
            _primed = false;
            _idle = 0f;
        }
    }
}
