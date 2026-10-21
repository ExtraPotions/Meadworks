// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Config;
using ExtraPotions.Wildblood.Adopted.Runtime;

namespace ExtraPotions.Wildblood.Adopted.Display
{
    /// <summary>
    /// How far one nameplate has been dressed by <see cref="Patches.EnemyHudPatch"/>: the creature's controller, found
    /// once, and the display settings under which every part the plate needs (the star row, the stolen and the devoured
    /// icons) was in place. A plate dressed under the settings in force is left alone, except that a starred one keeps
    /// the game's own star badges hidden, which the game sets again every frame; a change to one of the three settings,
    /// or a part that could not be added yet, has it dressed again.
    /// </summary>
    internal sealed class PlateDress
    {
        public const int Stars = 1;
        public const int Stolen = 2;
        public const int Devoured = 4;

        /// <summary>The creature's controller; null for a character this mod never resolves (a player).</summary>
        public EliteController? Controller;

        /// <summary>The settings mask the plate was fully dressed under; -1 until it is.</summary>
        public int DressedFor = -1;

        /// <summary>True when the plate carries this mod's star row, so the game's own badges stay hidden.</summary>
        public bool Starred;
        public bool BadgesOwned, Level2, Level3;
        public int TraitStars = -1, TraitMask = -1;

        /// <summary>The frame the plate was last seen in the game's list, so a plate the game dropped is forgotten.</summary>
        public int Seen;

        public PlateDress(EliteController? controller)
        {
            Controller = controller;
        }

        /// <summary>The three display settings as one mask, read once a frame.</summary>
        public static int Wanted() =>
            (Configuration.ColouredStars.Value ? Stars : 0) | (Configuration.ShowStolenItems.Value ? Stolen : 0)
            | (Configuration.ShowDevouredCreatures.Value ? Devoured : 0);

        public bool DressedUnder(int wanted) => DressedFor == wanted;
    }
}
