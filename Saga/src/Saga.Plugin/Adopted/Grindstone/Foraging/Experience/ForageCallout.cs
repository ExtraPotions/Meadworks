// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Foraging/Experience/ForageCallout.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Foraging's words in the world ("Discovered Thistle!"), for the picker only: drawn on the picker's own client with
    /// the game's floating text (<see cref="FloatingText"/>), a little above the plant, if their Show Callouts is on.
    /// </summary>
    public static class ForageCallout
    {
        private const float Height = 1.2f;

        public static void Show(Vector3 spot, string text)
        {
            if (ForagingSettings.ShowCallouts.Value)
                FloatingText.Show(spot + Vector3.up * Height, text);
        }
    }
}
