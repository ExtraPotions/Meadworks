// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Conversion
{
    /// <summary>Reads old inscription layers without rewriting them; unchanged old defaults yield to the new balance.</summary>
    public static class InscriptionFormatMigration
    {
        static readonly Dictionary<string, string> Twins = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["quickened_hc"]="quickened", ["blood_drinker_hc"]="blood_drinker", ["stalwart_hc"]="stalwart",
            ["perfect_guard_hc"]="perfect_guard", ["mist_veil_hc"]="mist_veil", ["troll_blood_hc"]="troll_blood",
            ["second_wind_hc"]="second_wind", ["seidr_flow_hc"]="seidr_flow", ["hardened_hc"]="hardened",
            ["fleetfoot_hc"]="fleetfoot",
        };
        static readonly HashSet<string> Removed = new HashSet<string>(new[] { "reflex_draught", "valhallas_edge" }, StringComparer.Ordinal);

        public static string Preview(string name, string text, string legacyDefaults, ICollection<string> notes)
        {
            var root = Read(text);
            if (Scalar(root,"format") != "3") throw new FormatException("Legacy inscription conversion requires format 3.");
            var baseline = Read(legacyDefaults);
            bool main = name.Equals("Inscribe.inscriptions.yml",StringComparison.OrdinalIgnoreCase)
                || name.Equals("EliteCrafting_inscriptions.yml",StringComparison.OrdinalIgnoreCase);
            if (Scalar(root,"use_defaults").Equals("false",StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Format-3 inscriptions with use_defaults: false need an explicit format-4 conversion; the existing file is unchanged.");
            var result = main ? Difference(root,baseline) : root;
            result.Children.Remove(new YamlScalarNode("health_critical"));
            if (Child(root,"health_critical") != null) notes.Add("Removed the health-critical condition settings; the new balance is unconditional. Source unchanged.");
            result.Children[new YamlScalarNode("format")] = new YamlScalarNode("4");
            var baselineRows = Rows(baseline);
            var sourceRows = Rows(root);
            var migrated = new Dictionary<string,YamlMappingNode>(StringComparer.Ordinal);
            foreach (var pair in sourceRows)
            {
                var row = main && baselineRows.TryGetValue(pair.Key,out var original) ? Difference(pair.Value,original) : Copy(pair.Value);
                row.Children.Remove(new YamlScalarNode("id"));
                if (Child(pair.Value,"condition") is YamlScalarNode condition)
                {
                    if (condition.Value != "none" && condition.Value != "health_critical") throw new FormatException("Unknown condition on inscription '"+pair.Key+"'.");
                    row.Children.Remove(new YamlScalarNode("condition"));
                    if (condition.Value == "health_critical") notes.Add("Inscription '"+pair.Key+"': removed health-critical condition; retained customized values.");
                }
                if (row.Children.Count == 0) continue;
                if (Removed.Contains(pair.Key))
                    throw new FormatException("Customized retired inscription '"+pair.Key+"' needs an explicit replacement; the source file is unchanged.");
                var id = Twins.TryGetValue(pair.Key,out var twin) ? twin : pair.Key;
                if (migrated.TryGetValue(id,out var previous)) Merge(previous,row,id);
                else migrated.Add(id,row);
                if (id != pair.Key) notes.Add("Inscription '"+pair.Key+"' customized fields move to '"+id+"'. Source unchanged.");
            }
            result.Children.Remove(new YamlScalarNode("inscriptions"));
            if (migrated.Count > 0)
            {
                var rows = new YamlSequenceNode();
                foreach (var pair in migrated)
                {
                    pair.Value.Children[new YamlScalarNode("id")] = new YamlScalarNode(pair.Key);
                    rows.Add(pair.Value);
                }
                result.Add("inscriptions",rows);
            }
            if (Child(result,"caps") is YamlMappingNode caps)
            {
                var converted = new YamlMappingNode();
                foreach (var pair in caps.Children)
                {
                    if (!(pair.Key is YamlScalarNode key) || key.Value == null) throw new FormatException("A cap key must be text.");
                    var id = key.Value.EndsWith("@health_critical",StringComparison.Ordinal) ? key.Value.Substring(0,key.Value.Length-16) : key.Value;
                    if (id == "auto_mead" || id == "hc_threshold") throw new FormatException("A customized retired cap '"+id+"' needs an explicit replacement.");
                    if (converted.Children.TryGetValue(new YamlScalarNode(id),out var existing) && !Same(existing,pair.Value))
                        throw new FormatException("Conflicting unconditional and health-critical caps for '"+id+"'; source unchanged.");
                    converted.Children[new YamlScalarNode(id)] = pair.Value;
                }
                result.Children[new YamlScalarNode("caps")] = converted;
            }
            notes.Add("Format-3 inscriptions read as format 4 in memory; unchanged old defaults use the new balance, customized values are retained, and no file is rewritten.");
            return Write(result);
        }

        static Dictionary<string,YamlMappingNode> Rows(YamlMappingNode root)
        {
            var result = new Dictionary<string,YamlMappingNode>(StringComparer.Ordinal);
            if (Child(root,"inscriptions") == null) return result;
            if (!(Child(root,"inscriptions") is YamlSequenceNode rows)) throw new FormatException("Inscriptions must be a list.");
            foreach (var node in rows)
            {
                if (!(node is YamlMappingNode row) || Scalar(row,"id").Length == 0) throw new FormatException("An inscription row needs an id.");
                var id=Scalar(row,"id");
                if (result.ContainsKey(id)) throw new FormatException("Duplicate inscription id '"+id+"'.");
                result.Add(id,row);
            }
            return result;
        }

        static YamlMappingNode Difference(YamlMappingNode value,YamlMappingNode baseline)
        {
            var result=new YamlMappingNode();
            foreach (var pair in value.Children)
            {
                if (!baseline.Children.TryGetValue(pair.Key,out var original)) result.Add(pair.Key,pair.Value);
                else if (pair.Value is YamlMappingNode map && original is YamlMappingNode before)
                {
                    var changed=Difference(map,before); if(changed.Children.Count>0) result.Add(pair.Key,changed);
                }
                else if (!Same(pair.Value,original)) result.Add(pair.Key,pair.Value);
            }
            return result;
        }

        static void Merge(YamlMappingNode target,YamlMappingNode change,string id)
        {
            foreach (var pair in change.Children)
            {
                if (!target.Children.TryGetValue(pair.Key,out var previous)) target.Add(pair.Key,pair.Value);
                else if (previous is YamlMappingNode map && pair.Value is YamlMappingNode next) Merge(map,next,id);
                else if (!Same(previous,pair.Value)) throw new FormatException("Conflicting customizations of retired and plain inscription '"+id+"'; source unchanged.");
            }
        }
        static bool Same(YamlNode a,YamlNode b)
        {
            if(a is YamlScalarNode sa && b is YamlScalarNode sb) return sa.Value==sb.Value;
            if(a is YamlSequenceNode la && b is YamlSequenceNode lb) return la.Children.Count==lb.Children.Count&&la.Children.Zip(lb.Children,Same).All(s=>s);
            if(a is YamlMappingNode ma && b is YamlMappingNode mb) return ma.Children.Count==mb.Children.Count&&ma.Children.All(p=>mb.Children.TryGetValue(p.Key,out var v)&&Same(p.Value,v));
            return false;
        }
        static YamlNode? Child(YamlMappingNode root,string key)=>root.Children.TryGetValue(new YamlScalarNode(key),out var value)?value:null;
        static string Scalar(YamlMappingNode root,string key)=>(Child(root,key) as YamlScalarNode)?.Value??"";
        static YamlMappingNode Copy(YamlMappingNode map)=>Read(Write(map));
        static YamlMappingNode Read(string text)
        {
            var stream=new YamlStream();stream.Load(new StringReader(text));
            if(stream.Documents.Count!=1||!(stream.Documents[0].RootNode is YamlMappingNode root)) throw new FormatException("Expected one YAML mapping document.");
            return root;
        }
        static string Write(YamlMappingNode root){var writer=new StringWriter();new YamlStream(new YamlDocument(root)).Save(writer,false);return writer.ToString();}
    }
}
