// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System.IO;
using System.Text;
using BepInEx;
using ExtraPotions.Inscribe.Adopted.Rules;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// <c>inscribe dump affixes|economy|items</c> (console-commands.md section 3). <c>affixes</c> / <c>economy</c> write
    /// the effective merged configuration (every layer applied, before validation) to
    /// <c>ExtraPotions.Inscribe.Adopted_effective_&lt;family&gt;.yml.txt</c> in the config folder, with a header naming the layers; the
    /// <c>.yml.txt</c> extension keeps it out of both families. The layers are the texts the running rules were built
    /// from (<see cref="ActiveRules.SourcesInForce"/>), so a bound player dumps the server's configuration and a file
    /// with errors on disk does not replace what is in force. <c>items</c> writes the item survey
    /// (<see cref="ItemSurvey"/>). Runs on the caller's machine and writes only there.
    /// </summary>
    internal static class DumpCommand
    {
        public const string Grammar = "inscribe dump inscriptions|economy|items";

        public static void Run(CommandCall call)
        {
            switch (call.Lower(0))
            {
                case "inscriptions": Effective(call, FamilySpec.Affixes, "inscriptions"); break;
                case "economy": Effective(call, FamilySpec.Economy, "economy"); break;
                case "items": ItemSurvey.Write(call); break;
                default: call.Fail(call.Arg(0).Length == 0 ? "dump what?" : $"cannot dump '{call.Arg(0)}'.", Grammar); break;
            }
        }

        // The texts the running rules were built from: this machine's files, or the server's on a bound player.
        private static void Effective(CommandCall call, FamilySpec spec, string family)
        {
            RuleSources sources = ActiveRules.SourcesInForce(spec);
            RuleIssues issues = new RuleIssues();
            YamlMappingNode merged = FamilyBuilder.Merge(spec, sources.Files, issues);
            string path = ConfigOutput.Write($"ExtraPotions.Inscribe.Adopted_effective_{family}.yml.txt", Header(family, sources, issues) + Yaml(merged));
            call.Reply($"wrote {path}");
            if (sources.FromServer)
            {
                call.Detail("these are the server's files, in force for you while the server binds this player.");
            }
        }

        private static string Header(string family, RuleSources sources, RuleIssues issues)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"# ExtraPotions.Inscribe.Adopted effective {family} configuration, written by 'inscribe dump {family}'.\n");
            sb.Append($"# Not read by the mod. The rules in force, from {(sources.FromServer ? "the server's" : "this machine's")} files. Layers, lowest first:\n");
            sb.Append(sources.DefaultsLayered ? $"#   {FamilySpec.DefaultsOrigin}\n" : "#   (built-in defaults skipped: use_defaults: false)\n");
            foreach (SourceText file in sources.Files)
            {
                sb.Append($"#   {file.Name}\n");
            }
            sb.Append($"# {issues.Warnings.Count} warning(s) while merging.\n\n");
            return sb.ToString();
        }

        private static string Yaml(YamlMappingNode node)
        {
            StringWriter writer = new StringWriter();
            new YamlStream(new YamlDocument(node)).Save(writer, false);
            return writer.ToString();
        }
    }

    /// <summary>Writes a command's output file into the BepInEx config folder.</summary>
    internal static class ConfigOutput
    {
        /// <summary>Writes UTF-8 without BOM, Unix line ends; returns the full path.</summary>
        public static string Write(string fileName, string text)
        {
            string path = Path.Combine(Paths.ConfigPath, fileName);
            File.WriteAllText(path, text.Replace("\r\n", "\n"), new UTF8Encoding(false));
            return path;
        }
    }
}
