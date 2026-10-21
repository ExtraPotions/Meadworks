using System;
using System.Collections.Generic;
using System.IO;
using ExtraPotions.Mantle.Embedded.EliteCraftingLink;
using Mantle.Core;
using Mantle.Panels;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Mantle.Elite
{
    /// <summary>
    /// EliteCrafting's inscriptions on the Gear tab's stat sheet, after Epic Loot's effects (the user's request,
    /// 2026-10-05: "the center panel also includes stats on gear from elite crafting"): a section per EliteCrafting
    /// category (offence, defence, utility; one EliteCrafting adds later under Other), each inscription once with its
    /// values summed over everything worn, in its own tooltip line's words, from EliteCrafting's
    /// <c>GetPlayerInscriptionsJson</c> (0.7.0 and later). Hovering a line lists each item's own value, and says so when
    /// the sum passes the effect's cap. Without EliteCrafting, or with an older one, nothing.
    /// </summary>
    public static class EliteStatLines
    {
        private const string Endpoint = "GetPlayerInscriptionsJson";
        private static readonly string[] Known = { "offense", "defense", "utility" };
        private static bool? supported;
        private static bool warned;

        public static void Fill(Player player, StatSheet sheet)
        {
            List<YamlMappingNode> entries = Entries(player);
            if (entries.Count == 0)
                return;
            foreach (string category in Known)
                AddSection(sheet, entries, Title(category), entry => Field(entry, "category") == category);
            AddSection(sheet, entries, Title(null), entry => Array.IndexOf(Known, Field(entry, "category")) < 0);
        }

        private static void AddSection(StatSheet sheet, List<YamlMappingNode> entries, string title, Predicate<YamlMappingNode> takes)
        {
            sheet.Section(title);
            foreach (YamlMappingNode entry in entries)
                if (takes(entry))
                    sheet.Add(Field(entry, "line"), "", Tip(entry));
        }

        /// <summary>Each item's value, then the cap note when the game applies less than the sum.</summary>
        private static string Tip(YamlMappingNode entry)
        {
            TipText tip = new TipText();
            if (entry.Children.TryGetValue(new YamlScalarNode("sources"), out YamlNode node) && node is YamlSequenceNode sources)
                foreach (YamlNode source in sources)
                    if (source is YamlMappingNode map)
                        tip.Part(Field(map, "item"), Field(map, "value"));
            if (Field(entry, "capped") == "true")
                tip.Heading(StatTipWords.Capped);
            return tip.ToString();
        }

        private static string Title(string category)
        {
            string group = category == "offense" ? Words.StatOffence
                : category == "defense" ? Words.StatDefence
                : category == "utility" ? Words.Utility
                : Words.StatOther;
            return Words.StatEliteCrafting + " · " + group;
        }

        private static List<YamlMappingNode> Entries(Player player)
        {
            List<YamlMappingNode> entries = new List<YamlMappingNode>();
            string json = Supported ? CraftingInscriptions.GetPlayerInscriptionsJson(player) : null;
            if (string.IsNullOrEmpty(json))
                return entries;
            YamlSequenceNode list = Parse(json);
            if (list != null)
                foreach (YamlNode node in list)
                    if (node is YamlMappingNode map)
                        entries.Add(map);
            return entries;
        }

        private static YamlSequenceNode Parse(string json)
        {
            try
            {
                YamlStream stream = new YamlStream();
                stream.Load(new StringReader(json));
                return stream.Documents.Count > 0 ? stream.Documents[0].RootNode as YamlSequenceNode : null;
            }
            catch (YamlException e)
            {
                if (!warned)
                    Plugin.Log.LogWarning($"EliteCrafting's inscription list does not parse, it is left out of the stat sheet: {e.Message}");
                warned = true;
                return null;
            }
        }

        private static string Field(YamlMappingNode map, string key) =>
            map.Children.TryGetValue(new YamlScalarNode(key), out YamlNode node) && node is YamlScalarNode scalar ? scalar.Value ?? "" : "";

        /// <summary>EliteCrafting with the endpoint, asked once it is found.</summary>
        private static bool Supported
        {
            get
            {
                if (supported == null && CraftingLink.Present)
                    supported = CraftingLink.HasEndpoint(Endpoint);
                return supported == true;
            }
        }
    }
}
