using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Wildroot.Runtime {
 internal sealed class LocalizationLoader:IDisposable {
  readonly WildrootConfig settings;readonly ManualLogSource log;readonly MeadworksTranslations translations;
  internal LocalizationLoader(WildrootConfig settings,string assets,BepInPlugin metadata,ManualLogSource log){
   this.settings=settings;this.log=log;
   // Preserve existing config-file locations while sharing C05's validation, fallback and watcher.
   translations=MeadworksTranslations.Register(metadata,"wildroot",Path.Combine(assets,"localization"),Path.Combine(Paths.ConfigPath,"Wildroot","localization"),log);
  }
  void SelectLanguage()=>translations.LanguageSetting=settings.Language.Value;
  internal string Language{get{SelectLanguage();return translations.Language;}}
  internal long Revision=>translations.Translations.Revision;
  internal string Text(string key){SelectLanguage();return translations.Text(key);}
  internal string Format(string key,params object[] arguments){SelectLanguage();return translations.Format(key,arguments);}
  internal void Update(){
   SelectLanguage();translations.Update();
   if(!settings.Get("Presentation.ExportEnglishReference",false))return;
   try{ExportEnglish();if(settings.Find("Presentation.ExportEnglishReference") is BepInEx.Configuration.ConfigEntry<bool> entry)entry.Value=false;}
   catch(IOException){log.LogWarning("Wildroot English reference export failed");}
   catch(UnauthorizedAccessException){log.LogWarning("Wildroot English reference export access denied");}
  }
  internal string ExportEnglish()=>translations.ExportEnglish();
  public void Dispose()=>translations.Dispose();
 }
}
