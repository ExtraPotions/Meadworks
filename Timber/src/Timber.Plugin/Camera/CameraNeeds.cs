#nullable disable
// OpenKeep BuildCamera. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/BuildCamera/CameraNeeds.cs.
// Changed by expDARE, 2026: Timber namespace, Crucible authority/input/localization/lifecycle; kit pickup UI deferred.
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Timber.Runtime;

namespace Timber.Camera
{
    /// <summary>
    /// The comfort conditions of the camera, read where the player stands: the game's Resting effect (a fire, a roof,
    /// no enemy near; <c>SEMan.s_statusEffectResting</c>) and its comfort level (<c>Player.GetComfortLevel</c>, which
    /// the game recounts every 2 s).
    /// </summary>
    public static class CameraNeeds
    {
        public static bool Met(Player player, CameraValue<bool> resting, CameraValue<int> comfort)
        {
            return !MissesResting(player, resting) && player.GetComfortLevel() >= comfort.Value;
        }

        /// <summary>What is missing as one localized line, or null when nothing is.</summary>
        public static string Describe(Player player, CameraValue<bool> resting, CameraValue<int> comfort)
        {
            if (Met(player, resting, comfort))
                return null;
            List<string> parts = new List<string>();
            if (MissesResting(player, resting))
                parts.Add(TimberText.Text(CameraModule.Resting));
            int have = player.GetComfortLevel();
            if (have < comfort.Value)
                parts.Add(string.Format(TimberText.Text(CameraModule.Comfort), comfort.Value, have));
            return string.Join(", ", parts);
        }

        /// <summary>Everything <see cref="Describe"/> reads, folded into one number: the line changes only when this does.</summary>
        public static int Key(Player player, CameraValue<bool> resting, CameraValue<int> comfort) =>
            (MissesResting(player, resting) ? 1 : 0) | (player.GetComfortLevel() & 0xfff) << 1 | (comfort.Value & 0xfff) << 13;

        private static bool MissesResting(Player player, CameraValue<bool> resting)
        {
            return resting.Value && !player.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectResting);
        }
    }
}
