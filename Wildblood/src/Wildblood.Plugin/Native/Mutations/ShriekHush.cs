// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The near-silence of a ringing head, laid over everything the game plays through the listener's own volume
    /// (<c>AudioListener.volume</c>): a gain the game leaves at 1 and touches only for a cinematic. The player's volume
    /// settings live in the game's audio mixer and are never touched, so the settings menu works as ever and nothing
    /// there has to be put back. The listener volume found before the hush is given back exactly; should anything else
    /// set it meanwhile (a cinematic starting or ending), that value becomes the one given back, and a value someone
    /// else set after the hush's last step is never overwritten on release.
    /// </summary>
    internal sealed class ShriekHush
    {
        /// <summary>The share of the sound still heard at the depth of the hush: near-silent, not dead.</summary>
        private const float Floor = 0.04f;

        /// <summary>Seconds to fall silent - long enough that the shriek itself is heard first.</summary>
        private const float FadeIn = 0.35f;

        /// <summary>Seconds over which hearing comes back as the ringing wears off.</summary>
        private const float FadeOut = 0.75f;

        private float _own = -1f;
        private float _written = -1f;

        /// <summary>
        /// How much of the sound passes, from 0 at the depth of the hush to 1 for all of it, from the ringing's clock:
        /// seconds since it began and seconds it has left.
        /// </summary>
        public static float Level(float elapsed, float remaining) =>
            Mathf.Max(Mathf.Clamp01(1f - elapsed / FadeIn), Mathf.Clamp01(1f - remaining / FadeOut));

        /// <summary>One frame of the hush at <paramref name="level"/> (see <see cref="Level"/>).</summary>
        public void Hold(float level)
        {
            float now = AudioListener.volume;
            if (_own < 0f || !Mathf.Approximately(now, _written))
            {
                _own = now; // the first step, or someone else set it since our last: theirs is the volume to give back
            }
            _written = _own * Mathf.Lerp(Floor, 1f, level);
            AudioListener.volume = _written;
        }

        /// <summary>Gives the listener volume back, unless someone else has set it since the last step. Idempotent.</summary>
        public void Release()
        {
            if (_own >= 0f && Mathf.Approximately(AudioListener.volume, _written))
            {
                AudioListener.volume = _own;
            }
            _own = -1f;
        }
    }
}
