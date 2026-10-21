// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Veins/Veins.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Rich veins: 0 to 3 stars per ore deposit, the same on every machine with nothing stored (<see cref="VeinRoll"/>:
    /// the world seed and the deposit's position). Plain stone never has stars.
    /// <list type="bullet">
    /// <item><b>The bonus</b> (<see cref="OnBreak"/>, on the rock's owner): "Vein Bonus Per Star" percent more drop rolls
    /// per star on every chunk anyone breaks, whatever their level; whole rolls plus a chance for the fraction.</item>
    /// <item><b>Read the rock</b> (<see cref="VeinHover"/>, on each client): from "Read The Rock Level", the local
    /// player's hover over an ore deposit shows its stars, and a multi-chunk rock its chunks left.</item>
    /// </list>
    /// </summary>
    public static class Veins
    {
        /// <summary>
        /// The vein's stars, 0..3, on any machine: 0 for plain stone. Read from the rock's transform (the deposit's own
        /// position, not a chunk's), which stays readable in the frame the game destroys the rock's ZDO, so it works for
        /// the last chunk too; 0 once the rock's object itself is gone.
        /// </summary>
        public static int Stars(Rock rock)
        {
            if (rock == null || !rock.IsOre || rock.Target == null)
                return 0;
            return StarsAt(rock.Position);
        }

        /// <summary>The stars an ore deposit standing at this position has in the loaded world (the caller knows it is ore).</summary>
        public static int StarsAt(Vector3 position) => VeinRoll.StarsFor(VeinRoll.At(position));

        /// <summary>
        /// Called by <see cref="MineBreak"/> on the rock's owner, first of the break features, for every chunk (or single
        /// piece) a miner broke, by a hit, a splash or a collapse. A rich vein adds stars × "Vein Bonus Per Star" percent
        /// extra rolls with <see cref="RockBreak.AddRolls(float)"/> (2★ at 25%: a 50% chance of one); the foundation spawns
        /// them after every feature ran. The stars come from the rock, not from <see cref="RockBreak.Position"/> (the
        /// chunk's drop spot), so they match what the hover shows.
        /// </summary>
        public static void OnBreak(RockBreak broken)
        {
            if (!PickSkill.Active || broken == null || broken.Rock == null || !broken.IsOre)
                return;
            float rolls = Stars(broken.Rock) * VeinSettings.BonusPerStar.Value / 100f;
            if (rolls > 0f)
                broken.AddRolls(rolls);
        }
    }
}
