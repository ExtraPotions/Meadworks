// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Veins/VeinText.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The words of Read the rock, added under the game's hover name of an ore deposit (English, like the mod's other
    /// texts; the stars are gold ★ glyphs, <see cref="StarText"/>):
    /// <list type="bullet">
    /// <item>"Rich vein ★★" for a vein with stars, "Plain vein" for one without, so a player who has the milestone can
    /// tell a plain vein from a hover that shows nothing.</item>
    /// <item>"9 of 12 chunks left" under it for a multi-chunk rock (MineRock5 or MineRock), as this machine knows the
    /// chunks: a broken chunk reaches every client at once (the game's area health RPC).</item>
    /// </list>
    /// </summary>
    public static class VeinText
    {
        /// <summary>The lines for an ore deposit, without a leading line break.</summary>
        public static string Lines(Rock rock)
        {
            string vein = VeinLine(Veins.Stars(rock));
            return rock.HasChunks ? vein + "\n" + ChunksLine(rock) : vein;
        }

        public static string VeinLine(int stars) => stars > 0 ? SagaText.Format("gs_80f99934c2f50bd7", StarText.Colored(stars)) : SagaText.Text("gs_4b6b4f69d0b61bfb");

        public static string ChunksLine(Rock rock) => SagaText.Format("gs_891cb56b54c72a6f", RockChunks.Left(rock), RockChunks.Count(rock));
    }
}
