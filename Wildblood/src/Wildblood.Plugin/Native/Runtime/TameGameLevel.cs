// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: an older build stored the game level here and could put a new tame back on it.

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    internal static class TameGameLevel
    {
        /// <summary>The level the game gave the creature before this mod's stars replaced it. No longer written.</summary>
        public const string Key = "wildblood.game_level";

        /// <summary>1 when an older build put a tame back on that level. Releasing it lets the stored stars show.</summary>
        public const string Mark = "wildblood.tame_game_stars";

        public static bool Marked(ZDO zdo) => zdo != null && zdo.GetInt(Mark, 0) == 1;

        /// <summary>OWNER. Clears the old mark. Does not rewrite traits, health, or the saved game level key.</summary>
        public static void Release(ZDO zdo)
        {
            if (Marked(zdo)) zdo.Set(Mark, 0);
        }
    }
}
