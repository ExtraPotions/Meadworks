// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace ExtraPotions.Shepherd
{
    /// <summary>
    /// Localization keys (design 13, C05): namespace <c>shepherd</c>, tokens <c>$meadworks_shepherd_&lt;key&gt;</c>, English
    /// authoritative in <c>assets/localization/English.json</c>; the other six languages carry the same keys and
    /// placeholders (best-effort translations flagged for native review).
    /// </summary>
    public static class ShepherdText
    {
        public const string Namespace = "shepherd";

        /// <summary>"Herd room: {0} of {1}" (room left, limit).</summary>
        public const string HerdRoom = "herd_room";
        /// <summary>Strict Herd Limit holds a due birth.</summary>
        public const string HerdWaiting = "herd_waiting";
        /// <summary>Unlimited Herd cheat.</summary>
        public const string HerdUnlimited = "herd_unlimited";

        /// <summary>Trough piece and container names (S04b) and their shared description.</summary>
        public const string PieceFeedBarrel = "piece_feed_barrel";
        public const string PiecePlankTrough = "piece_plank_trough";
        public const string PieceStoneTrough = "piece_stone_trough";
        public const string PieceTroughDescription = "piece_trough_description";

        private static readonly string[] keys =
        {
            HerdRoom, HerdWaiting, HerdUnlimited, PieceFeedBarrel, PiecePlankTrough, PieceStoneTrough, PieceTroughDescription,
            "time_seconds", "time_minutes", "time_hours_minutes", "lore_taming_gate", "lore_taming", "lore_fed", "lore_content", "lore_love", "lore_pregnant", "lore_due", "lore_generation", "lore_parents", "lore_unknown", "lore_growing", "lore_grown_due", "lore_hatching", "lore_hatched_due", "callout_strong", "callout_twins",
            "book_title", "book_count", "book_close", "book_hungry", "book_fed", "book_pregnant", "book_summary", "book_previous", "book_next", "book_rename", "book_rename_reach", "book_empty", "book_empty_title", "book_empty_hint", "book_species", "book_stars", "book_variant", "book_state", "book_lineage", "book_generation", "book_born", "book_breeder", "book_mother", "book_sire", "book_grandparents", "book_traits", "trough_stock", "trough_last", "trough_no_meal", "trough_foods", "trough_food_only", "piece_land_pen", "piece_land_pen_description", "land_pen_capacity",
            "book_help_previous", "book_help_next", "book_help_close", "book_help_animal",
            "step_aside_feeding", "step_aside_husbandry", "step_aside_inheritance", "step_aside_containers", "import_done",
            "command_help", "command_off", "command_busy", "command_target", "command_sent", "command_unknown", "command_done", "command_refused",
            "feed_mark_hint", "feed_mark_on", "feed_mark_off", "feed_mark_busy", "feed_mark_refused", "feed_mark_unknown",
        };

        public static IReadOnlyList<string> Keys => keys;

        public static string Token(string key) => "$meadworks_" + Namespace + "_" + key;
    }
}
