// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The rule file's <c>difficulty:</c> line (features/difficulty.md). <see cref="Custom"/> is the file's own per-biome
    /// star and mutation rows with the world tier's boosts, and what a file with no line gets, so an existing server
    /// keeps playing as before. The five presets replace those rows with one shape: a biome's star cap, star odds and
    /// mutation rate follow how many bosses the world has killed and how many of them came after that biome.
    /// </summary>
    public enum Difficulty
    {
        Custom,
        Easy,
        Medium,
        Hard,
        VeryHard,
        Extreme,
    }

    /// <summary>The words a rule file and the console use for a difficulty.</summary>
    public static class DifficultyNames
    {
        public const string Choices = "Easy, Medium, Hard, Very Hard, Extreme or Custom";

        /// <summary>Case, spaces, dashes and underscores are ignored: <c>very hard</c>, <c>VeryHard</c>, <c>very_hard</c>.</summary>
        public static bool TryParse(string? text, out Difficulty difficulty)
        {
            string key = (text ?? "").Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();
            switch (key)
            {
                case "custom": difficulty = Difficulty.Custom; return true;
                case "easy": difficulty = Difficulty.Easy; return true;
                case "medium": difficulty = Difficulty.Medium; return true;
                case "hard": difficulty = Difficulty.Hard; return true;
                case "veryhard": difficulty = Difficulty.VeryHard; return true;
                case "extreme": difficulty = Difficulty.Extreme; return true;
                default: difficulty = Difficulty.Custom; return false;
            }
        }

        public static string Name(Difficulty difficulty) => difficulty == Difficulty.VeryHard ? "Very Hard" : difficulty.ToString();
    }
}
