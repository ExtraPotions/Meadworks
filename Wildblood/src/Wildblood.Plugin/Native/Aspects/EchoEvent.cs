// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Something an Echoing boss did at one moment, for its echo to do again `delay` seconds later
    /// (<see cref="EchoReplay"/>): either an animation cue its animator was sent (a stagger, a taunt, an aborted swing),
    /// or an attack it started.
    /// </summary>
    internal sealed class EchoEvent
    {
        public float Time;

        /// <summary>The animation cue, for a cue; null for an attack.</summary>
        public string? Trigger;

        /// <summary>The attack, for an attack; null for a cue.</summary>
        public EchoAttack? Attack;
    }

    /// <summary>
    /// An attack an Echoing boss started: the weapon it swung, primary or secondary, and the state of the game's random
    /// numbers as it started, so the echo's own start draws the same random animation. The cues the start sent are kept
    /// too: when the echo cannot start the attack it still plays the motion.
    /// </summary>
    internal sealed class EchoAttack
    {
        public string Weapon = "";
        public bool Secondary;
        public UnityEngine.Random.State Seed;
        public readonly List<string> Cues = new List<string>();
    }
}
