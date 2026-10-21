// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later, adapted from
// EliteCreaturesReborn/Rules/Difficulty.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Wildblood namespace; Custom moved last so Medium is the design default (4.2); Star Mode added.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Wildblood.Stars
{
    /// <summary>
    /// Difficulty presets (design A2). The five presets shape star caps, star odds and mutation rates by biome rank and
    /// the bosses killed after it; Custom uses the rule file's own per-biome rows with the world tier's boosts.
    /// </summary>
    public enum Difficulty
    {
        Easy,
        Medium,
        Hard,
        VeryHard,
        Extreme,
        Custom,
    }

    /// <summary>Design A9 and 4.2: Wildblood rolls stars; KeepExisting keeps the level the game or another mod gave; Off rolls nothing.</summary>
    public enum StarMode
    {
        Wildblood,
        KeepExisting,
        Off,
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
                case "normal": difficulty = Difficulty.Medium; return true; // CreatureManager's Biome Level Preset name (design 11.2)
                case "hard": difficulty = Difficulty.Hard; return true;
                case "veryhard": difficulty = Difficulty.VeryHard; return true;
                case "extreme": difficulty = Difficulty.Extreme; return true;
                default: difficulty = Difficulty.Medium; return false;
            }
        }

        public static string Name(Difficulty difficulty) => difficulty == Difficulty.VeryHard ? "Very Hard" : difficulty.ToString();
    }
}
