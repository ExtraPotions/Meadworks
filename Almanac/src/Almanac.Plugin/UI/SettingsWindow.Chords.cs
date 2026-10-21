// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Attributes;
using ExtraPotions.Almanac.Coexistence;
using ExtraPotions.Almanac.Index;
using ExtraPotions.Almanac.Runtime.Editing;
using UnityEngine;

namespace ExtraPotions.Almanac.Runtime.UI
{
    public sealed partial class SettingsWindow
    {
        IReadOnlyDictionary<string,string[]> chordClashes=new Dictionary<string,string[]>();

        static string ChordId(ConfigEntryBase entry)=>entry.ConfigFile.ConfigFilePath+"|"+entry.Definition.Section+"|"+entry.Definition.Key;

        // Runs on the same 0.3 s poll as the row refresh, so a rebind in any mod updates every affected row's warning.
        // Only key-typed entries are inspected; the full entry walk stays in Rebuild.
        void RefreshChordClashes()
        {
            var bindings=new List<ChordBinding>();
            try
            {
                foreach(var group in discovery.Index.Groups)
                foreach(var file in group.Files)
                {
                    if(file.Kind==FileKind.UnboundConfig||file.Kind==FileKind.DataFile)continue;
                    foreach(var entry in discovery.Entries(file.Path))
                    {
                        if(entry.SettingType!=typeof(KeyboardShortcut)&&entry.SettingType!=typeof(KeyCode))continue;
                        try
                        {
                            if(Hidden(entry))continue;
                            var native=new NativeEntry(entry);
                            if(native.Attributes.Browsable==false)continue;
                            string chord=Chord(native);
                            if(chord.Length>0)bindings.Add(new ChordBinding(ChordId(entry),group.DisplayName+" / "+text(Presentation.DisplayName(native.Attributes,entry.Definition.Key)),chord));
                        }
                        catch { } // One broken entry must not hide clashes between the others.
                    }
                }
                chordClashes=ChordClashPolicy.Find(bindings);
            }
            catch{chordClashes=new Dictionary<string,string[]>();}
        }

        string ChordClash(ConfigEntryBase entry)
            =>chordClashes.TryGetValue(ChordId(entry),out var others)&&others.Length>0?text("Shared chord")+": "+string.Join(", ",others):"";
    }
}
