// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.IO;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rules;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Api
{
    /// <summary>
    /// The API's definitions are JSON objects with the YAML format 2 field names (api.md section 1). JSON is YAML, so the
    /// merged YamlDotNet reads them into the same node tree the rule files give, and the rule parsers build the same
    /// models from them. Problems are logged under the endpoint's name; an error refuses the definition.
    /// </summary>
    internal static class ApiJson
    {
        /// <summary>The text as a mapping registered with <paramref name="issues"/> (for line numbers), or null after logging why not.</summary>
        public static YamlMappingNode? Map(string? json, string endpoint, RuleIssues issues)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                Log.Warn($"API {endpoint}: no definition given");
                return null;
            }
            try
            {
                YamlStream stream = new YamlStream();
                stream.Load(new StringReader(json));
                if (stream.Documents.Count > 0 && stream.Documents[0].RootNode is YamlMappingNode map)
                {
                    issues.Register(map, endpoint);
                    return map;
                }
                Log.Warn($"API {endpoint}: the definition should be a JSON object, {{ \"id\": ... }}");
            }
            catch (YamlException e)
            {
                Log.Warn($"API {endpoint}: the definition does not parse (line {e.Start.Line}): {e.Message}");
            }
            return null;
        }

        /// <summary>Logs a definition's warnings and errors; true when it has no error.</summary>
        public static bool Accept(RuleIssues issues, string endpoint)
        {
            foreach (string warning in issues.Warnings)
            {
                Log.Warn($"API {endpoint}: {warning}");
            }
            foreach (string error in issues.Errors)
            {
                Log.Error($"API {endpoint}: {error}");
            }
            return !issues.HasErrors;
        }
    }
}
