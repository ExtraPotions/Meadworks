// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the four column button captions in the seven Meadworks languages.
using System.Collections.Generic;

namespace ExtraPotions.Burrow
{
    /// <summary>
    /// Captions of the column buttons under the weight plate, by game language name. A button has 44 x 32.8 units for text
    /// at 14 pt (Sort 16 pt), two lines at most; "Quick stack" measures 90 units at 14 pt in game, about 8 a letter, so each
    /// line is kept to about five letters. A language that does not fit gets a shorter word, never a smaller size. A
    /// language missing here falls back to English.
    /// </summary>
    public static class ColumnCaptions
    {
        public const string QuickStack = "burrow_stow_quickstack";
        public const string StoreAll = "burrow_stow_storeall";
        public const string TopUp = "burrow_stow_topup";
        public const string Sort = "burrow_stow_sort";

        public static readonly IReadOnlyList<string> Keys = new[] { QuickStack, StoreAll, TopUp, Sort };

        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Words =
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["English"] = Row("Quick stack", "Store all", "Top up", "Sort"),
                ["German"] = Row("Stapel", "Alles rein", "Füllen", "Ordne"),
                ["French"] = Row("Tasser", "Tout caser", "Garnir", "Trier"),
                ["Spanish"] = Row("Apilar", "Meter todo", "Llenar", "Orden"),
                ["Russian"] = Row("Сдать", "Всё в ящик", "Взять", "А-Я"),
                // A space splits the two-word captions into two lines; without it the text would break after the third character.
                ["Korean"] = Row("빠른 쌓기", "모두 넣기", "채우기", "정렬"),
                ["Chinese"] = Row("快速 堆叠", "全部 存入", "补满", "整理"),
            };

        /// <summary>The caption of <paramref name="key"/> in <paramref name="language"/>, else English, else null.</summary>
        public static string? For(string? language, string key)
        {
            if (language != null && Words.TryGetValue(language, out var row) && row.TryGetValue(key, out var word))
                return word;
            return Words["English"].TryGetValue(key, out var english) ? english : null;
        }

        private static IReadOnlyDictionary<string, string> Row(string quickStack, string storeAll, string topUp, string sort) =>
            new Dictionary<string, string> { [QuickStack] = quickStack, [StoreAll] = storeAll, [TopUp] = topUp, [Sort] = sort };
    }
}
