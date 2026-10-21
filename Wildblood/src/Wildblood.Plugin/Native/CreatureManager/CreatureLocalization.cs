// From CreatureManager 1.2.5 (sighsorry1029), CreatureLocalization.cs, pinned at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// Licensed under GPL-3.0-only; original LICENSE is retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: isolated Wildblood source closure; see docs/cm-native-source-map.json.
// SPDX-License-Identifier: GPL-3.0-only
using System;

namespace ExtraPotions.Wildblood.AdoptedCm;

internal static class CreatureLocalization
{
    [ThreadStatic] private static bool loreFallback;

    internal static T ReadLore<T>(Func<T> read)
    {
        bool previous = loreFallback;
        loreFallback = true;
        try { return read(); }
        finally { loreFallback = previous; }
    }
    internal static string Localize(string key, string fallback)
    {
        bool hasTokenPrefix = key.StartsWith("$", StringComparison.Ordinal);
        string token = hasTokenPrefix ? key : "$" + key;
        global::Localization? localization = global::Localization.instance;
        if (localization == null)
        {
            return fallback;
        }

        try
        {
            string localized = localization.Localize(token);
            if (string.IsNullOrEmpty(localized) || string.Equals(localized, token, StringComparison.Ordinal)
                || loreFallback && string.Equals(localized, "[" + token.Substring(1) + "]", StringComparison.Ordinal))
            {
                return fallback;
            }

            bool returnedKey = hasTokenPrefix
                ? localized.Length == key.Length - 1 &&
                  string.CompareOrdinal(localized, 0, key, 1, localized.Length) == 0
                : string.Equals(localized, key, StringComparison.Ordinal);
            return returnedKey ? fallback : localized;
        }
        catch
        {
            return fallback;
        }
    }

    internal static string LocalizeText(string text)
    {
        if (string.IsNullOrEmpty(text) || global::Localization.instance == null)
        {
            return text;
        }

        try
        {
            return global::Localization.instance.Localize(text);
        }
        catch
        {
            return text;
        }
    }

    internal static string Format(string key, string fallback, params (string Name, string Value)[] placeholders)
    {
        string text = Localize(key, fallback);
        foreach ((string name, string value) in placeholders)
        {
            text = text.Replace("{" + name + "}", value);
        }

        return text;
    }
}
