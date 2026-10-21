// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Seams/Seam.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// One open seam on the miner's own client (<see cref="OpenSeams"/>): the chunk to hit, when the window closes, how
    /// far its chain has come, and its glow.
    /// </summary>
    internal sealed class Seam
    {
        public Seam(Rock rock, ZDOID id, int area, float closes, int links, SeamGlow glow)
        {
            Rock = rock;
            Id = id;
            Area = area;
            Closes = closes;
            Links = links;
            Glow = glow;
        }

        public Rock Rock { get; }

        public ZDOID Id { get; }

        /// <summary>The seam's chunk, as an area index.</summary>
        public int Area { get; }

        /// <summary>Time.time when the window closes.</summary>
        public float Closes { get; }

        /// <summary>Clean strikes already made in this chain: 0 for a seam the roll opened.</summary>
        public int Links { get; }

        public SeamGlow Glow { get; }

        /// <summary>
        /// The window is still open, the rock is still loaded and the chunk still stands on this machine. Another
        /// machine's break of the chunk arrives by the game's RPC_SetAreaHealth, which hides its collider.
        /// </summary>
        public bool IsOpen => Time.time < Closes && Rock.IsValid && SeamPicker.IsStanding(Rock, Area);
    }
}
