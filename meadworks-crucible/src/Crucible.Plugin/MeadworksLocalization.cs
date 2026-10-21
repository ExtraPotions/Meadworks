using System;
using System.Collections.Generic;
using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;
namespace ExtraPotions.Meadworks.Crucible {
    public sealed class MeadworksLocalization:IDisposable {
        readonly LocalizationCatalog catalog;
        readonly CustomLocalization localization;
        bool disposed;
        MeadworksLocalization(BepInPlugin metadata,string product,IReadOnlyDictionary<string,IReadOnlyDictionary<string,string>> tables) {
            catalog=new LocalizationCatalog(product,tables);
            localization=new CustomLocalization(metadata);
            LocalizationManager.Instance.AddLocalization(localization);
            foreach(var language in catalog.Tables)foreach(var translation in language.Value)localization.AddTranslation(language.Key,catalog.Token(translation.Key),translation.Value);
        }
        public static MeadworksLocalization Register(BepInPlugin metadata,string product,IReadOnlyDictionary<string,IReadOnlyDictionary<string,string>> tables)=>new MeadworksLocalization(metadata,product,tables);
        public string Text(string key,string language){if(disposed)throw new ObjectDisposedException(nameof(MeadworksLocalization));var token=catalog.Token(key);var selected=localization.GetTranslations(language);if(selected!=null&&selected.TryGetValue(token,out var value))return value;var english=localization.GetTranslations("English");return english!=null&&english.TryGetValue(token,out value)?value:key;}
        public string Token(string key)=>catalog.Token(key);
        public void Dispose(){if(disposed)return;disposed=true;foreach(var language in catalog.Tables)foreach(var key in language.Value.Keys)localization.ClearToken(language.Key,catalog.Token(key));}
    }
}
