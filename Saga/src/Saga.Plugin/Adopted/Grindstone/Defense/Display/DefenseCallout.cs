// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Display/DefenseCallout.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Short words floating where Defense did something ("Riposte!", "Reflex!", "Bash!", "First block: Troll"), drawn
    /// with the game's floating damage text (<see cref="FloatingText"/>) on the defending player's own client only, if
    /// their Defense "Show Callouts" is on. Every Defense event happens on that client, so nothing is sent.
    /// </summary>
    public static class DefenseCallout
    {
        public static void Show(Vector3 position, string text)
        {
            if (DefenseSettings.ShowCallouts.Value)
                FloatingText.Show(position, text);
        }

        /// <summary>Above the local player's head.</summary>
        public static void OverPlayer(string text)
        {
            Player player = Player.m_localPlayer;
            if (player != null)
                Show(player.GetTopPoint() + Vector3.up * 0.3f, text);
        }
    }
}
