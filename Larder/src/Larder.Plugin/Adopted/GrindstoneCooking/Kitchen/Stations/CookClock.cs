// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Kitchen/Stations/CookClock.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// A slot's cooked time under the cook's perks. The game adds the elapsed seconds to a slot's cooked time, marks
    /// it done past the recipe's cook time T and burnt past 2T. With the cooking speed s the time up to T runs s times
    /// faster; with the extra burn time e (a share of T) the time past T runs 1/(1+e) as fast, so a dish is done after
    /// T/s seconds and burns (1+e)T seconds later instead of T. Exact for any elapsed time, so a station catching up a
    /// long absence in one step ends where it would have second by second.
    /// </summary>
    public static class CookClock
    {
        /// <summary>
        /// The cooked time after <paramref name="elapsed"/> real seconds, from <paramref name="cooked"/>, for a recipe
        /// of <paramref name="cookTime"/> seconds, at cooking speed <paramref name="speed"/> (1 is the game's) and
        /// extra burn time <paramref name="extraBurn"/> (0 is the game's).
        /// </summary>
        public static float Advance(float cooked, float elapsed, float cookTime, float speed, float extraBurn)
        {
            float burnRate = 1f / (1f + extraBurn);
            if (cooked >= cookTime)
                return cooked + elapsed * burnRate;
            float untilDone = (cookTime - cooked) / speed;
            if (elapsed <= untilDone)
                return cooked + elapsed * speed;
            return cookTime + (elapsed - untilDone) * burnRate;
        }

        /// <summary>Whether the perks change nothing: the game's own clock.</summary>
        public static bool IsVanilla(float speed, float extraBurn) => speed <= 1f && extraBurn <= 0f;
    }
}
