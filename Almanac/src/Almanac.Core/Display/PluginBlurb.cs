// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.RegularExpressions;

namespace ExtraPotions.Almanac.Display
{
    /// <summary>
    /// The plugin list's hover text: what the mod does, from its package manifest or assembly description, never its
    /// config file path. BepInPlugin metadata has no description of its own.
    /// </summary>
    public static class PluginBlurb
    {
        static readonly Regex Description=new Regex("\"description\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);

        /// <summary>The <c>description</c> of a Thunderstore manifest.json, or empty.</summary>
        public static string FromManifest(string? json)
        {
            if(string.IsNullOrEmpty(json))return "";
            var match=Description.Match(json);
            if(!match.Success)return "";
            try{return Regex.Unescape(match.Groups[1].Value).Trim();}
            catch(System.ArgumentException){return match.Groups[1].Value.Trim();}
        }

        /// <summary>The first non-empty description, else the name and version.</summary>
        public static string Text(string? manifest,string? assembly,string name,string? version)
        {
            if(!string.IsNullOrWhiteSpace(manifest))return manifest!.Trim();
            if(!string.IsNullOrWhiteSpace(assembly))return assembly!.Trim();
            return string.IsNullOrWhiteSpace(version)?name:name+" "+version;
        }
    }
}
