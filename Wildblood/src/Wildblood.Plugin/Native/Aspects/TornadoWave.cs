// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// One Nightfall wave as the boss's owner raised it: the moment it rose on the shared clock, the numbers it was
    /// raised with - every machine forms, moves, draws and judges its tornadoes by these, never by its own reading of
    /// the rules - the delay the owner drew before the next wave, and for each tornado the player it hunts and the
    /// point it rose at.
    /// </summary>
    internal sealed class TornadoWave
    {
        /// <summary>A tornado with less than this left to turn no longer counts as hunting its player.</summary>
        private const long Ending = 1000L;

        public long At;
        public float Form;
        public float Life;

        /// <summary>The hunting speed in metres a second, fixed by the owner when it raised the wave.</summary>
        public float Speed;

        /// <summary>Damage a second to a player inside a funnel.</summary>
        public float Damage;

        /// <summary>Seconds from this wave to the next, as the owner drew them.</summary>
        public float Next;

        public TornadoShape Shape;

        public readonly List<ZDOID> Targets = new List<ZDOID>();
        public readonly List<Vector3> Spawns = new List<Vector3>();

        public int Count => Targets.Count;

        /// <summary>When its tornadoes have formed and start to hunt and hurt (shared-clock ms).</summary>
        public long FormedAt => At + (long)(Form * 1000f);

        /// <summary>When its tornadoes break up (shared-clock ms).</summary>
        public long EndsAt => At + (long)(Life * 1000f);

        public float Age(long nowMs) => (nowMs - At) / 1000f;

        public void Add(ZDOID target, Vector3 spawn)
        {
            Targets.Add(target);
            Spawns.Add(spawn);
        }

        /// <summary>Whether a tornado of this wave hunts <paramref name="player"/> and has a while left.</summary>
        public bool Hunts(ZDOID player, long nowMs) => nowMs < EndsAt - Ending && Targets.Contains(player);
    }
}
