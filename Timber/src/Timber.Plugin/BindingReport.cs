// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Read-only loaded-config scan as in Almanac ConfigDiscovery; C09 supplies canonical C07 collision decisions.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
namespace ExtraPotions.Timber.Runtime {
    public static class BindingReport {
        sealed class Binding {
            public string Id="",Label="",Text="";
            public bool Own;
        }
        public static IReadOnlyList<string> Lines(bool startup=false) {
            var bindings=new List<Binding>();var unreadable=new List<string>();
            foreach(var info in Chainloader.PluginInfos.Values.OrderBy(p=>p.Metadata.GUID,StringComparer.Ordinal)) {
                if(info.Instance==null)continue;
                try {
                    foreach(var pair in info.Instance.Config.OrderBy(p=>p.Key.Section,StringComparer.Ordinal).ThenBy(p=>p.Key.Key,StringComparer.Ordinal)) {
                        var entry=pair.Value;string text;bool own=info.Metadata.GUID==Plugin.Guid;
                        if(entry is ConfigEntry<KeyboardShortcut> shortcut)text=(own&&Plugin.PlayerLocks!=null?Plugin.PlayerLocks.Effective(shortcut,shortcut.Value):shortcut.Value).Serialize();
                        else if(entry is ConfigEntry<KeyCode> code)text=(own&&Plugin.PlayerLocks!=null?Plugin.PlayerLocks.Effective(code,code.Value):code.Value).ToString();
                        else continue;
                        string key=pair.Key.Section+"."+pair.Key.Key;
                        bindings.Add(new Binding {Id="binding."+bindings.Count,Label=info.Metadata.Name+" / "+key,Text=text,Own=own});
                    }
                } catch(Exception error) { unreadable.Add(TimberText.Format("bindings_unreadable",info.Metadata.Name,error.GetType().Name)); }
            }
            var byId=bindings.ToDictionary(b=>b.Id,StringComparer.Ordinal);
            var converter=new ConfigConverter(new ConversionMapping(1,"Loaded binding report",0,
                bindings.Select(b=>new ConversionRule(b.Id,b.Id,"KeyboardShortcut",actionBinding:true))));
            var preview=converter.Preview(bindings.Select(b=>new KeyValuePair<string,string>(b.Id,b.Text)));
            var conflicts=new List<string>();
            foreach(var collision in preview.BindingConflicts) {
                var group=collision.Choices.Select(c=>byId[c.TargetId]).ToArray();
                if(group.Any(b=>b.Own))conflicts.Add(TimberText.Format("bindings_conflict",KeyLabels.Short(HotkeyChord.Parse(collision.TargetId)),string.Join("; ",group.Select(b=>b.Label))));
            }
            if(startup)return conflicts;
            var lines=new List<string> {TimberText.Text("bindings_scope")};
            foreach(var binding in bindings.Where(b=>b.Own))lines.Add(binding.Label+" = "+(HotkeyChord.TryParse(binding.Text,out var chord)?KeyLabels.Short(chord):binding.Text));
            lines.AddRange(conflicts.Count==0?new[]{TimberText.Text("bindings_none")}:conflicts);
            lines.AddRange(unreadable);lines.AddRange(preview.Errors);lines.Add(TimberText.Text("bindings_tip"));return lines;
        }
        public static string Summary() {
            var lines=Lines(startup:true);return lines.Count==0?TimberText.Text("bindings_none"):string.Join(" | ",lines);
        }
    }
}
