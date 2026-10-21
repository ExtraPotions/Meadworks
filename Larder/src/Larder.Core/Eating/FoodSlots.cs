// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/FoodSlots.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the CanEat and EatFood prefixes became one decision table over counts
// and flags (the game's own outcome at 3 slots included, so the plugin can compare); the most depleted food is
// chosen from plain times; the Eat Anything cheat never reports full; HUD slot count rule added (design B4).
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace ExtraPotions.Larder.Eating
{
    /// <summary>What eating one more food does.</summary>
    public enum EatOutcome
    {
        /// <summary>A free slot takes the new food.</summary>
        Add,

        /// <summary>All slots are used: the most depleted food that can be eaten again is replaced.</summary>
        ReplaceMostDepleted,

        /// <summary>The same food is held and can be eaten again: it is refreshed (the game's rule).</summary>
        RefreshSame,

        /// <summary>The same food is held and cannot be eaten again yet ("no more").</summary>
        NoMore,

        /// <summary>All slots are used and none can be eaten again ("full").</summary>
        Full,
    }

    /// <summary>
    /// Food Slots (design B3, B4): 1 to 5 (game 3). A new food takes a free slot; with all used it replaces the most
    /// depleted food that can be eaten again, else "full"; a lowered count keeps the extra foods until they run out;
    /// feast food fills a slot like any food.
    /// </summary>
    public static class FoodSlots
    {
        public const int GameSlots = 3;
        public const int MinSlots = 1;
        public const int MaxSlots = 5;

        public static int Clamp(int slots) => slots < MinSlots ? MinSlots : slots > MaxSlots ? MaxSlots : slots;

        /// <summary>
        /// The outcome of eating a food while <paramref name="held"/> foods are held with <paramref name="slots"/>
        /// configured. Eat Anything (cheat): the same food can always be eaten again and the slots never report full.
        /// </summary>
        public static EatOutcome Decide(int slots, int held, bool hasSame, bool sameCanEatAgain, bool anyCanEatAgain, bool eatAnything)
        {
            if (hasSame)
                return sameCanEatAgain || eatAnything ? EatOutcome.RefreshSame : EatOutcome.NoMore;
            if (held < Clamp(slots))
                return EatOutcome.Add;
            return anyCanEatAgain || eatAnything ? EatOutcome.ReplaceMostDepleted : EatOutcome.Full;
        }

        /// <summary>Foods kept after the count is lowered: none is removed, they run out on their own.</summary>
        public static int Kept(int slots, int held) => held;

        /// <summary>HUD food slots (B4): the configured count (empty ones included), or the foods still held if more.</summary>
        public static int HudSlots(int slots, int held)
        {
            int configured = Clamp(slots);
            return held > configured ? held : configured;
        }

        /// <summary>
        /// The index of the food a replacement takes: the lowest time left among the foods that can be eaten again,
        /// or -1. With Eat Anything and none re-eatable, the lowest time left of all.
        /// </summary>
        public static int MostDepleted(IReadOnlyList<float> timesLeft, IReadOnlyList<bool> canEatAgain, bool eatAnything)
        {
            int best = -1;
            for (int i = 0; i < timesLeft.Count; i++)
            {
                if (canEatAgain[i] && (best < 0 || timesLeft[i] < timesLeft[best]))
                    best = i;
            }
            if (best >= 0 || !eatAnything)
                return best;
            for (int i = 0; i < timesLeft.Count; i++)
            {
                if (best < 0 || timesLeft[i] < timesLeft[best])
                    best = i;
            }
            return best;
        }
    }
}
