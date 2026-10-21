// Changed by expDARE, 2026: retained source adaptation notices.
// Kindle - Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from OpenKeep 3.0.0, ad278ecf323901c3645eb1f9f6fac6833a0f8cb1:
// OpenKeep/OpenKeep/src/Tracker/TrackerStyle.cs. Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// See THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Changed by expDARE: Serif default/kit font fallback, Crucible language font resolver and owned-font cleanup.
using System;
using ExtraPotions.Kindle.Config;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
namespace ExtraPotions.Kindle.Runtime
{
    internal static class TrackerStyle
    {
        private static readonly string[] names={"Valheim-AveriaSansLibre","Valheim-AveriaSerifLibre","Valheim-Norsebold"};
        private static string loaded="";
        private static TMP_FontAsset? resolved,ownedAsset;
        private static Font? ownedFont;
        internal static float Size=>KindleTracker.Settings.Get(KindleSettings.Tracker.FontSize);
        internal static Color Dim=>new Color(0.72f,0.72f,0.72f,1f);
        internal static Color Have=>Colour(KindleSettings.Tracker.HaveColour,Color.white);
        internal static Color Missing=>Colour(KindleSettings.Tracker.MissingColour,new Color(1f,0.42f,0.35f));
        internal static Color Ready=>Colour(KindleSettings.Tracker.ReadyColour,new Color(1f,0.713f,0.361f));
        private static string Language=>Localization.instance != null ? Localization.instance.GetSelectedLanguage() : "English";
        internal static string Signature=>KindleTracker.Settings.Get(KindleSettings.Tracker.Font)+"|"+Size+"|"+Language+"|"+(ObjectDB.instance ? ObjectDB.instance.m_recipes.Count : 0);
        private static Color Colour(KindleSetting<string> setting,Color fallback)=>ColorUtility.TryParseHtmlString(KindleTracker.Settings.Get(setting),out var value) ? value : fallback;
        internal static TMP_FontAsset? Font
        {
            get
            {
                string key=KindleTracker.Settings.Get(KindleSettings.Tracker.Font)+"|"+Language;
                if(loaded==key && resolved)return resolved;
                Shutdown();loaded=key;
                if(!FontResolver.UsesGameFont(Language))
                {
                    var choice=MeadworksFonts.Choose(Language);var source=MeadworksFonts.For(Language,Mathf.RoundToInt(Size));
                    if(choice.Kind==FontKind.OperatingSystem)ownedFont=source;
                    if(source)
                    {
                        try{ownedAsset=TMP_FontAsset.CreateFontAsset(source);resolved=ownedAsset;}
                        catch(Exception){resolved=null;}
                    }
                }
                else
                {
                    var selection=KindleTracker.Settings.Get(KindleSettings.Tracker.Font);
                    foreach(var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                        if(asset && asset.name==names[Mathf.Clamp((int)selection,0,names.Length-1)]){resolved=asset;break;}
                }
                if(!resolved)resolved=KitCanvas.Font;
                return resolved;
            }
        }
        internal static void Shutdown()
        {
            if(ownedAsset)
            {
                foreach(var texture in ownedAsset.atlasTextures)if(texture)UnityEngine.Object.Destroy(texture);
                if(ownedAsset.material)UnityEngine.Object.Destroy(ownedAsset.material);
                UnityEngine.Object.Destroy(ownedAsset);
            }
            if(ownedFont)UnityEngine.Object.Destroy(ownedFont);
            ownedAsset=null;ownedFont=null;resolved=null;loaded="";
        }
    }
}
