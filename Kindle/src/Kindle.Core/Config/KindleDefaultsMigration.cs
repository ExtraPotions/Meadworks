// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Kindle.Config
{
    /// <summary>
    /// Input defaults changed by the settings simplification (October 2026). A stored value that still equals the old
    /// default moves to the new default once (config schema 0 to 1); any other stored value is the player's choice and stays.
    /// </summary>
    public static class KindleDefaultsMigration
    {
        /// <summary>Config schema after the migration.</summary>
        public const int Schema = 1;

        /// <summary>Each changed setting with its old default, as stored in the cfg file.</summary>
        public static readonly IReadOnlyList<KeyValuePair<KindleSetting<string>, string>> Changes = new[]
        {
            new KeyValuePair<KindleSetting<string>, string>(KindleSettings.RecipeList.FavouriteRecipeKey, "F"),
            new KeyValuePair<KindleSetting<string>, string>(KindleSettings.Salvage.SalvageKey, "Backspace"),
            new KeyValuePair<KindleSetting<string>, string>(KindleSettings.RecipeList.GamepadSortButton, "JoyLStick"),
        };

        /// <summary>The new default when <paramref name="stored"/> exactly equals the old default; otherwise null (keep).</summary>
        public static string? Migrate(KindleSetting<string> setting, string? stored)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            foreach (var change in Changes)
                if (ReferenceEquals(change.Key, setting))
                    return string.Equals(stored, change.Value, StringComparison.Ordinal) ? setting.Default : null;
            return null;
        }
    }
}
