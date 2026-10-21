// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// What the waves report on every tick of a wave (<see cref="RaidWaves.Tick"/>): the raiders of this raid alive now
    /// and the current wave's raiders still to arrive. The runner moves on when both are 0, and writes them to the host's
    /// ZDO for the HUD line.
    /// </summary>
    public readonly struct WaveCount
    {
        public WaveCount(int alive, int toCome)
        {
            Alive = alive;
            ToCome = toCome;
        }

        /// <summary>The raiders of this raid alive now, of every wave.</summary>
        public int Alive { get; }

        /// <summary>The current wave's raiders not yet arrived (waiting under the 20-alive cap, or not yet placed).</summary>
        public int ToCome { get; }

        /// <summary>True when the wave is beaten: nobody alive and nobody still to come.</summary>
        public bool Cleared => Alive <= 0 && ToCome <= 0;
    }
}
