// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GrindstoneSkills 0.14.1:
// GrindstoneSkills/GrindstoneSkills/Kitchen/Stations/CookClock.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the recipe time T is FeastMaster's (station entry x Cook Time
// Multiplier, design 1.3) passed in by the caller; slot state and the seconds until done and burnt added.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Cooking
{
    /// <summary>A cooking station slot's state.</summary>
    public enum SlotState
    {
        Cooking,
        Done,
        Burnt,
    }

    /// <summary>
    /// A slot's cooked time under the cook's perks (design F2, F3 and 1.3). The game adds the elapsed seconds to a
    /// slot's cooked time, marks it done past the recipe time T and burnt past 2T. With cooking speed s the time up to T
    /// runs s times faster; with extra burn time e (a share of T) the time past T runs 1 / (1 + e) as fast, so a dish is
    /// done after T / s seconds and burns (1 + e) T seconds later. Exact for any elapsed time, so a station catching up
    /// a long absence in one step ends where it would have second by second.
    /// </summary>
    public static class CookClock
    {
        /// <summary>The cooked time after <paramref name="elapsed"/> real seconds from <paramref name="cooked"/>.</summary>
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

        /// <summary>The slot's state as the game decides it: done past T, burnt past 2T when it can burn.</summary>
        public static SlotState State(float cooked, float cookTime, bool canBurn)
        {
            if (canBurn && cooked > cookTime * 2f)
                return SlotState.Burnt;
            return cooked > cookTime ? SlotState.Done : SlotState.Cooking;
        }

        /// <summary>Real seconds until the slot reaches T at speed s.</summary>
        public static float SecondsUntilDone(float cooked, float cookTime, float speed)
        {
            return cooked >= cookTime ? 0f : (cookTime - cooked) / speed;
        }

        /// <summary>Real seconds from a done slot until it reaches 2T, the time past T running 1 / (1 + e) as fast.</summary>
        public static float SecondsUntilBurnt(float cooked, float cookTime, float extraBurn)
        {
            float from = cooked > cookTime ? cooked : cookTime;
            float left = cookTime * 2f - from;
            return left > 0f ? left * (1f + extraBurn) : 0f;
        }
    }
}
