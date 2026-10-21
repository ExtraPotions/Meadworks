// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: catalog recipe and color texts (design 4.3).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Keel.Catalog
{
    public readonly struct RecipeItem
    {
        public RecipeItem(string prefab, int amount)
        {
            Prefab = prefab;
            Amount = amount;
        }

        public string Prefab { get; }
        public int Amount { get; }

        public override string ToString() => Prefab + ":" + Amount.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A ship recipe: up to <see cref="MaxItems"/> comma-separated <c>Prefab:Amount</c> pairs (the build HUD has room for
    /// five plus the station), each item once, amounts at least 1. Whether each prefab exists is checked by the plugin.
    /// </summary>
    public sealed class Recipe
    {
        public const int MaxItems = 5;

        private Recipe(IReadOnlyList<RecipeItem> items, IReadOnlyList<string> errors)
        {
            Items = items;
            Errors = errors;
        }

        public IReadOnlyList<RecipeItem> Items { get; }

        /// <summary>One message per refused part; empty when the recipe is valid.</summary>
        public IReadOnlyList<string> Errors { get; }

        public bool Valid => Errors.Count == 0 && Items.Count > 0;

        public static Recipe Parse(string? text)
        {
            var items = new List<RecipeItem>();
            var errors = new List<string>();
            foreach (var raw in (text ?? "").Split(','))
            {
                var part = raw.Trim();
                if (part.Length == 0) continue;
                var colon = part.IndexOf(':');
                var prefab = colon > 0 ? part.Substring(0, colon).Trim() : "";
                var amountText = colon > 0 ? part.Substring(colon + 1).Trim() : "";
                if (prefab.Length == 0 || !int.TryParse(amountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
                    errors.Add("\"" + part + "\" is not Prefab:Amount");
                else if (amount < 1)
                    errors.Add("\"" + part + "\" needs an amount of at least 1");
                else if (items.Any(i => i.Prefab == prefab))
                    errors.Add("\"" + prefab + "\" is listed twice");
                else
                    items.Add(new RecipeItem(prefab, amount));
            }
            if (items.Count > MaxItems) errors.Add("more than " + MaxItems + " items");
            if (items.Count == 0 && errors.Count == 0) errors.Add("no items");
            return new Recipe(items, errors);
        }

        /// <summary>
        /// The recipe a ship piece gets (design 4.3): the configured text when it parses and every item exists
        /// (<paramref name="isItem"/>, checked by the plugin against the game's items), else the default with the reason;
        /// a default that fails too gives an empty recipe, which the plugin treats as "do not change the piece".
        /// </summary>
        public static ResolvedRecipe Resolve(string? text, string fallback, Func<string, bool> isItem)
        {
            if (isItem == null) throw new ArgumentNullException(nameof(isItem));
            var reason = Problem(Parse(text), isItem);
            if (reason.Length == 0) return new ResolvedRecipe(Parse(text), false, "");
            var standard = Parse(fallback);
            return new ResolvedRecipe(Problem(standard, isItem).Length == 0 ? standard : Parse(null), true, reason);
        }

        private static string Problem(Recipe recipe, Func<string, bool> isItem)
        {
            if (!recipe.Valid) return string.Join("; ", recipe.Errors);
            var unknown = recipe.Items.Where(i => !isItem(i.Prefab)).Select(i => i.Prefab).ToList();
            return unknown.Count == 0 ? "" : "unknown item " + string.Join(", ", unknown);
        }

        public override string ToString() => string.Join(", ", Items);
    }

    /// <summary>The recipe a piece gets, whether the configured text was refused, and why.</summary>
    public sealed class ResolvedRecipe
    {
        internal ResolvedRecipe(Recipe recipe, bool usedFallback, string reason)
        {
            Recipe = recipe;
            UsedFallback = usedFallback;
            Reason = reason;
        }

        public Recipe Recipe { get; }
        public bool UsedFallback { get; }

        /// <summary>Empty when the configured recipe was used.</summary>
        public string Reason { get; }
    }

    public readonly struct HexColor
    {
        public HexColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        /// <summary>Channels as 0 to 1, for the engine's color type.</summary>
        public float Red => R / 255f;
        public float Green => G / 255f;
        public float Blue => B / 255f;

        /// <summary>Multiplicative tint per channel (white leaves the other color unchanged), rounded.</summary>
        public static HexColor Multiply(HexColor a, HexColor b) =>
            new HexColor(Channel(a.R, b.R), Channel(a.G, b.G), Channel(a.B, b.B));

        private static byte Channel(byte x, byte y) => (byte)Math.Round(x * y / 255.0, MidpointRounding.AwayFromZero);

        /// <summary>Hex <c>RRGGBB</c>, with or without a leading <c>#</c>, any case, surrounding spaces ignored.</summary>
        public static bool TryParse(string? text, out HexColor color)
        {
            color = default;
            var t = (text ?? "").Trim();
            if (t.StartsWith("#", StringComparison.Ordinal)) t = t.Substring(1);
            if (t.Length != 6 || !int.TryParse(t, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)) return false;
            color = new HexColor((byte)(value >> 16), (byte)(value >> 8), (byte)value);
            return true;
        }

        /// <summary>Canonical upper-case <c>RRGGBB</c>.</summary>
        public override string ToString() => R.ToString("X2", CultureInfo.InvariantCulture) + G.ToString("X2", CultureInfo.InvariantCulture) + B.ToString("X2", CultureInfo.InvariantCulture);

        /// <summary>A color setting's rule: canonical hex, else the fallback; empty stays empty when allowed.</summary>
        public static string Normalize(string? text, string fallback, bool allowEmpty)
        {
            if (allowEmpty && string.IsNullOrWhiteSpace(text)) return "";
            return TryParse(text, out var c) ? c.ToString() : fallback;
        }
    }
}