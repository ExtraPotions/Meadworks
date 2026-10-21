// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Almanac.Coexistence
{
    /// <summary>One bound key setting: a stable id, the "Product / Setting" label shown to players, and its normalized chord.</summary>
    public sealed class ChordBinding
    {
        public ChordBinding(string id,string label,string chord){Id=id??"";Label=label??"";Chord=chord??"";}
        public string Id{get;}
        public string Label{get;}
        public string Chord{get;}
    }

    /// <summary>Finds key settings from different mods that are bound to the same chord.</summary>
    public static class ChordClashPolicy
    {
        /// <summary>Maps each clashing id to the sorted labels of the other settings that share its chord. Unbound and unique chords are absent.</summary>
        public static IReadOnlyDictionary<string,string[]> Find(IEnumerable<ChordBinding> bindings)
        {
            var result=new Dictionary<string,string[]>(StringComparer.Ordinal);
            foreach(var group in bindings.Where(b=>b.Chord.Length>0&&b.Id.Length>0).GroupBy(b=>b.Chord,StringComparer.OrdinalIgnoreCase))
            {
                var members=group.GroupBy(b=>b.Id,StringComparer.Ordinal).Select(g=>g.First()).ToArray();
                if(members.Length<2)continue;
                foreach(var member in members)
                    result[member.Id]=members.Where(other=>!ReferenceEquals(other,member)).Select(other=>other.Label)
                        .Distinct(StringComparer.Ordinal).OrderBy(label=>label,StringComparer.Ordinal).ToArray();
            }
            return result;
        }
    }
}
