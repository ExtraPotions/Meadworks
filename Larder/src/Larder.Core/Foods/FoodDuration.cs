// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Foods
{
    /// <summary>
    /// How long a food lasts (design 1.3, "Duration modifier, food rate"): burn time = item Duration x Duration
    /// Modifier, and the world food rate then sets how fast it runs down. Eat Again At reads the result. Endless Food (cheat) holds the timer.
    /// </summary>
    public static class FoodDuration
    {
        /// <summary>The composed burn time in seconds; a negative modifier reads 0.</summary>
        public static float BurnTime(float itemDuration, float durationModifier)
        {
            return itemDuration * LarderMath.NonNegative(durationModifier);
        }

        /// <summary>The time left after <paramref name="seconds"/> at the world food rate, never below 0.</summary>
        public static float RunDown(float timeLeft, float seconds, float foodRate, bool endless)
        {
            if (endless)
                return timeLeft;
            float left = timeLeft - seconds * LarderMath.NonNegative(foodRate);
            return left > 0f ? left : 0f;
        }
    }
}
