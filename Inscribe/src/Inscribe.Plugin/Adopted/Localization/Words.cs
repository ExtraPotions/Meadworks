// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; shipped word lists for Spanish, French, German, Russian, Chinese and Korean.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using ExtraPotions.Inscribe.Adopted.Core;
using HarmonyLib;
using YamlDotNet.RepresentationModel;

// Namespace Text, not Localization: a namespace ExtraPotions.Inscribe.Adopted.Localization would hide the game's Localization class
// from every file of the mod.
namespace ExtraPotions.Inscribe.Adopted.Text
{
    /// <summary>
    /// Every player-facing word (localization.md). English ships embedded as flat YAML maps, one file per feature area:
    /// <c>translations/English.&lt;area&gt;.yml</c> (key without <c>$</c> → text), all loaded, so each area adds its
    /// own words in its own file and nobody edits another's. The other six languages ship as
    /// <c>translations/&lt;Language&gt;.yml</c> and replace only the keys they list, so a missing line stays English.
    /// A player's <c>Inscribe.translations.&lt;Language&gt;.yml</c> in the config folder overrides both. Handed to the
    /// game's localization on every language setup and at plugin Awake; the game's cache (which also stores misses) is
    /// evicted afterwards.
    /// </summary>
    public static class Words
    {
        private const string EnglishPrefix = "Inscribe.translations.English.";
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Raised after words reached the game's localization: a language setup (and plugin Awake) or a
        /// <see cref="Add"/>. Anything that caches localized text drops it here. Local machine, main thread.
        /// </summary>
        public static event Action? Changed;
        internal static void Shutdown() { Changed = null; English.Clear(); }

        /// <summary>Loads the embedded English files. Plugin Awake, before anything localizes.</summary>
        internal static void Initialise()
        {
            foreach (string resource in Embedded.Names(EnglishPrefix, ".yml"))
            {
                Merge(English, Embedded.Text(resource) ?? "", resource);
            }
            Log.Info($"{English.Count} English words loaded");
        }

        /// <summary>Adds (or replaces) one English word from code, e.g. for data-driven keys. Key without the <c>$</c>.</summary>
        public static string Add(string key, string english)
        {
            English[key] = english;
            global::Localization? loc = Game.Current;
            if (loc != null)
            {
                loc.AddWord(key, english);
                loc.m_cache.EvictAll();
                RaiseChanged();
            }
            return "$" + key;
        }

        /// <summary>Whether a key (with or without the <c>$</c>) has English text.</summary>
        public static bool Has(string key) => English.ContainsKey(key.TrimStart('$'));

        /// <summary>Localizes a text with <c>$tokens</c> and <c>$1..$n</c> placeholders, or returns it unchanged before the game is up.</summary>
        public static string Localize(string text, params string[] words)
        {
            global::Localization? loc = Game.Current;
            if (loc == null)
            {
                return text;
            }
            return words.Length == 0 ? loc.Localize(text) : loc.Localize(text, words);
        }

        /// <summary>Installs English, then the shipped words for that language, then the player's own file.</summary>
        internal static void Install(global::Localization loc, string language)
        {
            foreach (KeyValuePair<string, string> word in English)
            {
                loc.AddWord(word.Key, word.Value);
            }
            foreach (KeyValuePair<string, string> word in ShippedFile(language))
            {
                loc.AddWord(word.Key, word.Value);
            }
            foreach (var key in English.Keys)
            {
                string? token = key.StartsWith("ecf_stone_", StringComparison.Ordinal) ? "rune_" + key.Substring("ecf_stone_".Length)
                    : key.StartsWith("ecf_msg_", StringComparison.Ordinal) ? "rune_msg_" + key.Substring("ecf_msg_".Length)
                    : key.StartsWith("ecf_rarity_", StringComparison.Ordinal) ? "rarity_" + key.Substring("ecf_rarity_".Length)
                    : key.StartsWith("ecf_affix_", StringComparison.Ordinal) ? "affix_" + key.Substring("ecf_affix_".Length)
                    : key.StartsWith("ecf_essence_", StringComparison.Ordinal) ? "essence_" + key.Substring("ecf_essence_".Length)
                    : key == "ecf_ui_newer_format" ? "rune_msg_newer_format" : null;
                if (token == null) continue;
                string translated = Runtime.InscribeText.TextForLanguage(token, language);
                if (translated == token) continue;
                translated = System.Text.RegularExpressions.Regex.Replace(translated, @"\{(\d+)\}", match => "$" + (int.Parse(match.Groups[1].Value) + 1));
                loc.AddWord(key, translated);
            }
            foreach (KeyValuePair<string, string> word in PlayerFile(language))
            {
                loc.AddWord(word.Key, word.Value);
            }
            loc.m_cache.EvictAll();
            RaiseChanged();
        }

        // A listener's exception must not stop the words from being installed.
        private static void RaiseChanged()
        {
            try
            {
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Log.Error($"a Words.Changed handler threw: {e}");
            }
        }

        /// <summary>
        /// Embedded words for one supported language. Keys the file does not list stay English.
        /// English itself is already installed. An unsupported language normalizes to English and adds nothing.
        /// </summary>
        private static Dictionary<string, string> ShippedFile(string language)
        {
            Dictionary<string, string> words = new Dictionary<string, string>(StringComparer.Ordinal);
            string canonical = ExtraPotions.Meadworks.Crucible.MeadworksLanguages.Normalize(language);
            if (canonical == "English")
            {
                return words;
            }
            foreach (string resource in Embedded.Names("Inscribe.translations." + canonical + ".", ".yml"))
            {
                Merge(words, Embedded.Text(resource) ?? "", resource);
            }
            return words;
        }

        private static Dictionary<string, string> PlayerFile(string language)
        {
            Dictionary<string, string> words = new Dictionary<string, string>(StringComparer.Ordinal);
            string path = Path.Combine(Paths.ConfigPath, $"Inscribe.translations.{language}.yml");
            if (!File.Exists(path))
            {
                return words;
            }
            try
            {
                Merge(words, File.ReadAllText(path), Path.GetFileName(path));
            }
            catch (Exception e)
            {
                Log.Warn($"could not read {path}: {e.Message}");
            }
            return words;
        }

        private static void Merge(Dictionary<string, string> into, string text, string origin)
        {
            try
            {
                YamlStream stream = new YamlStream();
                stream.Load(new StringReader(text));
                if (stream.Documents.Count > 0 && stream.Documents[0].RootNode is YamlMappingNode map)
                {
                    AddPairs(into, map, origin);
                }
            }
            catch (Exception e)
            {
                Log.Warn($"{origin}: not a valid word list, skipped: {e.Message}");
            }
        }

        private static void AddPairs(Dictionary<string, string> into, YamlMappingNode map, string origin)
        {
            foreach (KeyValuePair<YamlNode, YamlNode> pair in map.Children)
            {
                string? key = (pair.Key as YamlScalarNode)?.Value;
                string? value = (pair.Value as YamlScalarNode)?.Value;
                if (string.IsNullOrEmpty(key) || value == null)
                {
                    Log.Warn($"{origin}: an entry is not 'key: text', skipped");
                    continue;
                }
                into[key!.TrimStart('$')] = value;
            }
        }

        /// <summary>The game's localization, if it exists yet (never created from here on a dedicated server early).</summary>
        private static class Game
        {
            public static global::Localization? Current
            {
                get
                {
                    try
                    {
                        return global::Localization.instance;
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(global::Localization), nameof(global::Localization.SetupLanguage))]
        private static class SetupLanguagePatch
        {
            [HarmonyPostfix]
            private static void Postfix(global::Localization __instance, string language) => Install(__instance, language);
        }

        /// <summary>Plugin Awake, after PatchAll: the game's localization may already exist without our words.</summary>
        internal static void InstallNow()
        {
            global::Localization? loc = Game.Current;
            if (loc != null)
            {
                Install(loc, loc.GetSelectedLanguage());
            }
        }
    }
}
