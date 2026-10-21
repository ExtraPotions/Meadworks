// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System;
using System.Collections.Generic;
using System.IO;
using ExtraPotions.Inscribe.Adopted.Rules;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// Which layer last touched each id of an id list (<c>affixes</c>, <c>stones</c>, <c>rarities</c>), for the last
    /// column of <c>inscribe list</c>: <c>builtin</c> for the embedded defaults, else the file name. Layers merge in order
    /// (built-in defaults, the main file, the other files by name), so the last layer that names an id is the last
    /// that touched it. Reads the texts the running rules were built from (<see cref="ActiveRules.SourcesInForce"/>),
    /// so a bound player sees the server's file names, prefixed <c>server:</c>. Command path only: parses the texts
    /// each time it is asked.
    /// </summary>
    internal sealed class RuleOrigins
    {
        private readonly Dictionary<string, string> _byId;

        private RuleOrigins(Dictionary<string, string> byId)
        {
            _byId = byId;
        }

        public static RuleOrigins For(FamilySpec spec, string listKey)
        {
            RuleSources sources = ActiveRules.SourcesInForce(spec);
            Dictionary<string, string> byId = new Dictionary<string, string>(StringComparer.Ordinal);
            if (sources.DefaultsLayered)
            {
                Collect(FamilyBuilder.DefaultText(spec), "builtin", listKey, byId);
            }
            string prefix = sources.FromServer ? "server:" : "";
            foreach (SourceText file in sources.Files)
            {
                Collect(file.Text, prefix + file.Name, listKey, byId);
            }
            return new RuleOrigins(byId);
        }

        public string Of(string id) => _byId.TryGetValue(id, out string origin) ? origin : "?";

        private static void Collect(string text, string origin, string listKey, Dictionary<string, string> byId)
        {
            if (!(Root(text) is YamlMappingNode root) || !(YamlNodes.Child(root, listKey) is YamlSequenceNode list))
            {
                return;
            }
            foreach (YamlNode entry in list.Children)
            {
                string? id = entry is YamlMappingNode map ? YamlNodes.Text(YamlNodes.Child(map, "id")) : null;
                if (!string.IsNullOrEmpty(id))
                {
                    byId[id!] = origin;
                }
            }
        }

        // A file that does not parse is reported by the rules loader; here it simply contributes nothing.
        private static YamlNode? Root(string text)
        {
            try
            {
                YamlStream stream = new YamlStream();
                stream.Load(new StringReader(text));
                return stream.Documents.Count > 0 ? stream.Documents[0].RootNode : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
