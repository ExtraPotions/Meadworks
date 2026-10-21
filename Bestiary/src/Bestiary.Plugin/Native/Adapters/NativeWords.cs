// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Bestiary.Runtime.Native.Adapters
{
    internal static class NativeWords
    {
        private static string? language;
        public static void Add(Localization localization, string key, string fallback, string? selectedLanguage = null)
        {
            var translated = BestiaryText.TextForLanguage(key, selectedLanguage ?? localization.GetSelectedLanguage());
            localization.AddWord(key, translated == key ? fallback : translated);
        }
        internal static void Tick()
        {
            var current = Localization.instance;
            if (current == null || language == current.GetSelectedLanguage()) return;
            language = current.GetSelectedLanguage();
            Mimic.MimicWords.Add(current); Slinger.SlingerWords.Add(current);
            RimeGiant.RimeGiantWords.Add(current); Kraken.KrakenWords.Add(current);
            Arsenal.ArsenalWords.Add(current); Crossbow.XbowWords.Add(current); Headsman.HeadsmanWords.Add(current);
            Ballista.BallistaWords.Add(current);
        }
        internal static void Reset() => language = null;
    }
}
