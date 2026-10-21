using System.Collections.Generic;
using HarmonyLib;

namespace Mantle.Core
{
    /// <summary>
    /// English words of the mod. A module registers its keys in Initialize with <see cref="Add"/>; the words are
    /// handed to the game's localization when a language is set up and at once when one already is.
    /// Keys are "mantle_..." without the dollar; <see cref="Add"/> returns the "$mantle_..." token to put into texts.
    /// </summary>
    public static class Language
    {
        private static readonly Dictionary<string, string> words = new Dictionary<string, string>();

        public static string Add(string key, string english)
        {
            words[key] = english;
            if (Localization.m_instance != null)
                Localization.m_instance.AddWord(key, english);
            return "$" + key;
        }

        /// <summary>Localizes a text with $tokens through the game, or returns it unchanged before the game is up.</summary>
        public static string Localize(string text)
        {
            return Localization.m_instance != null ? Localization.m_instance.Localize(text) : text;
        }

        internal static void Install(Localization localization)
        {
            foreach (KeyValuePair<string, string> word in words)
                localization.AddWord(word.Key, word.Value);
        }
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
    public static class LanguageSetupPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Localization __instance) => Language.Install(__instance);
    }
}
