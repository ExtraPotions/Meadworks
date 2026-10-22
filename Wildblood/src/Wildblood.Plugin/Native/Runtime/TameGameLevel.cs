// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: an older build stored the game level here and could put a new tame back on it.

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    internal static class TameGameLevel
    {
        /// <summary>The level the game gave the creature before an older build replaced it. This build reads it and does not write it.</summary>
        public const string Key = "wildblood.game_level";

        /// <summary>1 when an older build put a tame back on that level. Cleared after the stored stars are published. Clearing it does not change the level.</summary>
        public const string Mark = "wildblood.tame_game_stars";

        public static bool Marked(ZDO zdo) => zdo != null && zdo.GetInt(Mark, 0) == 1;

        /// <summary>Resolve calls this before the restore pass. The mark stays so that pass can still see a parked roll.</summary>
        public static void Release(ZDO zdo)
        {
            if (zdo == null || !Marked(zdo)) return;
        }

        /// <summary>OWNER. Clears the old mark after the stored roll is published. Does not rewrite traits, health, or the saved game level.</summary>
        public static void Clear(ZDO zdo)
        {
            if (Marked(zdo)) zdo.Set(Mark, 0);
        }
    }
}
