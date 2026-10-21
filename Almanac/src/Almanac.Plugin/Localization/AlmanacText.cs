// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Almanac.Localization;
using ExtraPotions.Meadworks.Crucible;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.Localization
{
    /// <summary>
    /// Almanac's seven built-in catalogs, registered with Crucible so the game language and validated overrides apply.
    /// Source keeps English phrases; <see cref="TextKey"/> derives each catalog key, and a missing key shows the English.
    /// </summary>
    public static class AlmanacText
    {
        static MeadworksTranslations? catalog;
        static readonly Dictionary<string,string> keys=new Dictionary<string,string>(StringComparer.Ordinal);

        public static void Initialize(BepInPlugin metadata,ManualLogSource? log)
        {
            if(catalog!=null)return;
            var json=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            var assembly=typeof(AlmanacText).Assembly;
            foreach(var language in MeadworksLanguages.Supported)
                using(var stream=assembly.GetManifestResourceStream("Almanac.localization."+language+".json"))
                    if(stream!=null)using(var reader=new StreamReader(stream))json.Add(language,reader.ReadToEnd());
            catalog=MeadworksTranslations.Register(metadata,new ProductTranslations("almanac",json),log:log);
        }

        /// <summary>The current-language text of an English interface phrase.</summary>
        public static string T(string english)
        {
            if(string.IsNullOrEmpty(english)||catalog==null)return english;
            if(!keys.TryGetValue(english,out var key))keys[english]=key=TextKey.For(english);
            string value=catalog.Text(key);
            return string.Equals(value,key,StringComparison.Ordinal)?english:value;
        }

        public static void Tick()=>catalog?.Update();
        public static void Shutdown(){catalog?.Dispose();catalog=null;keys.Clear();}

        /// <summary>Kit dialogs name their buttons in English; relabel the ones Almanac opened.</summary>
        public static void LocalizeDialog(RectTransform? panel)
        {
            if(panel==null)return;
            foreach(var button in panel.GetComponentsInChildren<Button>(true))
            {
                string? caption=Caption(button.gameObject.name);
                if(caption==null)continue;
                var label=button.GetComponentInChildren<TMP_Text>(true);
                if(label!=null&&label.text!=caption)label.text=caption;
            }
        }

        static string? Caption(string name)
        {
            switch(name)
            {
                case "Save":return T("Save");
                case "Cancel":return T("Cancel");
                case "Confirm":return T("Confirm");
                case "Apply":return T("Apply");
                case "None":return T("None");
                case "All":return T("All");
                case "Add":return T("Add");
                case "Remove":return T("Remove");
                default:return null;
            }
        }
    }
}
