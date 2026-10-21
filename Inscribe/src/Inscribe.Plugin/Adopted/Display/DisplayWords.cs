// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Globalization;
using ExtraPotions.Inscribe.Adopted.Effects;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// Words and numbers as the tooltip shows them (display.md section 3): names that may be a <c>$key</c> or literal
    /// text, keys that may be missing, and values in the player's own culture (display only; item data is always
    /// invariant). Only called while a tooltip block is built, which is once per item state, never per frame.
    /// </summary>
    internal static class DisplayWords
    {
        /// <summary>Whether the game's localization has a word for a key (without <c>$</c>) in the current language.</summary>
        public static bool Has(string key) => Words.Localize("$" + key) != "[" + key + "]";

        /// <summary>
        /// A YAML <c>name</c>: a <c>$key</c> is localized and falls back to <paramref name="fallback"/> when no
        /// translation provides it (localization.md section 4); anything else is literal text shown as written.
        /// </summary>
        public static string Name(string? nameOrKey, string fallback)
        {
            if (string.IsNullOrEmpty(nameOrKey))
            {
                return fallback;
            }
            if (nameOrKey![0] != '$')
            {
                return nameOrKey;
            }
            string key = nameOrKey.Substring(1);
            string text = Words.Localize(nameOrKey);
            return text == "[" + key + "]" ? fallback : text;
        }

        /// <summary>A stored value in the player's culture, no sign: <c>6</c>, <c>12,5</c> on a German client.</summary>
        public static string Plain(float value) => value.ToString("0.##", CultureInfo.CurrentCulture);

        /// <summary>
        /// The generic value format: signed by the effect's polarity (raise +, lower -) and followed by its unit
        /// (<c>%</c> for percent, the affix's <c>unit</c> for flat). A negative stored value flips the sign.
        /// </summary>
        public static string Signed(float value, AffixDef def)
        {
            float shown = def.EffectDef != null && def.EffectDef.Polarity == EffectPolarity.Lower ? -value : value;
            string sign = shown < 0f ? "-" : "+";
            return sign + Plain(System.Math.Abs(shown)) + Unit(def);
        }

        /// <summary>The unit suffix. Judgement calls: <c>%</c>, <c> ms</c>, <c>°</c>, <c> m</c>, <c> min</c>.</summary>
        public static string Unit(AffixDef def)
        {
            if (def.Value == AffixValueType.Percent)
            {
                return "%";
            }
            switch (def.Unit)
            {
                case AffixUnit.Ms: return " ms";
                case AffixUnit.Deg: return "°";
                case AffixUnit.M: return " m";
                case AffixUnit.Min: return " min";
                default: return "";
            }
        }

        /// <summary>Raw text from data, shown verbatim: never parsed as rich text.</summary>
        public static string Verbatim(string raw) => "<noparse>" + raw + "</noparse>";
    }
}
