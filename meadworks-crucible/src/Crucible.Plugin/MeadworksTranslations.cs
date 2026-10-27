using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Plugin adapter for <see cref="ProductTranslations"/>: loads built-in JSON tables, polls the hot-reload overrides
    /// folder, exports the English reference and mirrors tokens into Jötunn so <c>$meadworks_product_key</c> works.
    /// </summary>
    public sealed class MeadworksTranslations:IDisposable {
        public static LocalizationRegistry Registry { get; }=new LocalizationRegistry();
        readonly string pluginGuid;
        const float PollSeconds=2f;
        readonly ProductTranslations translations;
        readonly TranslationOverrideWatcher watcher;
        readonly CustomLocalization jotunn;
        readonly ManualLogSource? log;
        readonly HashSet<(string Language,string Token)> mirrored=new HashSet<(string,string)>();
        long mirroredRevision=-1;float nextPoll;bool disposed;
        // The override files are checked on a worker thread: a stat on a busy disk can block for 10-39 ms.
        // The worker reads a file only when its stamp changed; the result is applied here.
        string[]? overridePaths;OverrideStamps? stamps;Task<List<Scan>>? pending;
        sealed class Scan{public int Index;public bool Exists;public long Length;public byte[]? Bytes;public Exception? Error;}
        public ProductTranslations Translations=>translations;
        /// <summary>Folder scanned for <c>&lt;Language&gt;.json</c> overrides.</summary>
        public string OverridesDirectory { get; }
        /// <summary>"Auto" follows the game language; otherwise a supported language name.</summary>
        public string LanguageSetting { get; set; }="Auto";

        MeadworksTranslations(BepInPlugin metadata,ProductTranslations translations,string? overridesDirectory,ManualLogSource? log){
            this.translations=Registry.Register(translations);
            pluginGuid=metadata?.GUID??"";
            try{
                this.log=log;watcher=new TranslationOverrideWatcher(translations);
                OverridesDirectory=overridesDirectory??Path.Combine(Paths.ConfigPath,"Meadworks",translations.Product,"localization");
                jotunn=new CustomLocalization(metadata);LocalizationManager.Instance.AddLocalization(jotunn);
                Mirror();
                SettingTranslations.Add(pluginGuid,translations);
            }catch{Registry.Remove(translations.Product);throw;}
        }
        public static MeadworksTranslations Register(BepInPlugin metadata,ProductTranslations translations,string? overridesDirectory=null,ManualLogSource? log=null)=>new MeadworksTranslations(metadata,translations,overridesDirectory,log);
        /// <summary>Loads <c>&lt;Language&gt;.json</c> for each supported language present in <paramref name="builtinDirectory"/>; English is required.</summary>
        public static MeadworksTranslations Register(BepInPlugin metadata,string product,string builtinDirectory,string? overridesDirectory=null,ManualLogSource? log=null){
            var json=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(var language in MeadworksLanguages.Supported){var path=Path.Combine(builtinDirectory,language+".json");if(File.Exists(path))json.Add(language,File.ReadAllText(path));}
            return Register(metadata,new ProductTranslations(product,json),overridesDirectory,log);
        }

        internal static readonly ResolveOnce<Type> LocalizationType=new ResolveOnce<Type>(()=>AccessTools.TypeByName("Localization"));
        static readonly ResolveOnce<System.Reflection.FieldInfo> LocalizationInstance=new ResolveOnce<System.Reflection.FieldInfo>(()=>{var t=LocalizationType.Get();return t==null?null:AccessTools.Field(t,"m_instance");});
        static readonly ResolveOnce<System.Reflection.MethodInfo> SelectedLanguage=new ResolveOnce<System.Reflection.MethodInfo>(()=>{var t=LocalizationType.Get();return t==null?null:AccessTools.Method(t,"GetSelectedLanguage");});

        public static string GameLanguage(){
            // Reading Localization.instance constructs the singleton and touches PlatformPrefs.
            // Product Awake/config descriptions may run before that platform service is ready.
            // Follow the existing skills adapter: observe only an already-created instance.
            // The lookups are cached: TypeByName scans every type of every assembly, and this runs several times a frame.
            var type=LocalizationType.Get();var singleton=type==null?null:LocalizationInstance.Get()?.GetValue(null);
            var selected=singleton==null?null:SelectedLanguage.Get()?.Invoke(singleton,null)?.ToString();
            return MeadworksLanguages.Normalize(selected);
        }
        public string Language=>string.Equals(LanguageSetting,"Auto",StringComparison.OrdinalIgnoreCase)?GameLanguage():MeadworksLanguages.Normalize(LanguageSetting);
        public string Text(string key)=>translations.Text(key,Language);
        public string Format(string key,params object[] arguments)=>translations.Format(key,Language,arguments);
        public string Token(string key)=>translations.Token(key);

        /// <summary>Call from the product's Update; checks each language's override every two seconds, off the main thread.</summary>
        public void Update(){
            if(disposed)return;
            if(pending!=null){
                if(!pending.IsCompleted)return;
                var done=pending;pending=null;
                if(done.Status==TaskStatus.RanToCompletion)foreach(var scan in done.Result)Apply(scan);
                Mirror();
            }
            if(Time.unscaledTime<nextPoll)return;
            nextPoll=Time.unscaledTime+PollSeconds;
            var languages=MeadworksLanguages.Supported;
            if(overridePaths==null){overridePaths=new string[languages.Count];for(int i=0;i<languages.Count;i++)overridePaths[i]=Path.Combine(OverridesDirectory,languages[i]+".json");stamps=new OverrideStamps(languages.Count);}
            var paths=overridePaths;var known=stamps!;
            // Only this task touches the stamps until it completes; Update starts no other while it runs.
            pending=Task.Run(()=>ScanFiles(paths,known));
            Mirror();
        }
        static List<Scan> ScanFiles(string[] paths,OverrideStamps known){
            var changed=new List<Scan>();
            for(int i=0;i<paths.Length;i++){
                try{
                    var info=new FileInfo(paths[i]);bool exists=info.Exists;long length=exists?info.Length:0;
                    if(!known.Changed(i,exists,length,exists?info.LastWriteTimeUtc.Ticks:0))continue;
                    // An oversized file is never read; the watcher reports it from the length.
                    changed.Add(new Scan{Index=i,Exists=exists,Length=length,Bytes=exists&&length<=TranslationJson.MaximumBytes?File.ReadAllBytes(paths[i]):null});
                }catch(Exception error){known.Forget(i);changed.Add(new Scan{Index=i,Error=error});}
            }
            return changed;
        }
        readonly Dictionary<int,string> readProblems=new Dictionary<int,string>();
        void Apply(Scan scan){
            var language=MeadworksLanguages.Supported[scan.Index];
            // A file that stays unreadable is rescanned every poll; it is reported once until it reads again.
            if(scan.Error!=null){
                var problem=scan.Error is UnauthorizedAccessException?"access denied":"could not be read";
                if(readProblems.TryGetValue(scan.Index,out var known)&&known==problem)return;
                readProblems[scan.Index]=problem;
                log?.LogWarning("[Meadworks:"+translations.Product+"] translation override "+problem+": "+language);return;
            }
            readProblems.Remove(scan.Index);
            var bytes=scan.Bytes;
            var result=watcher.Observe(language,scan.Exists,scan.Length,()=>bytes??Array.Empty<byte>());
            if(result.Outcome==OverrideOutcome.Rejected||result.Outcome==OverrideOutcome.TooLarge)log?.LogWarning("[Meadworks:"+translations.Product+"] translation override "+language+": "+result.Error);
        }
        /// <summary>Writes <c>English.reference.json</c> into the overrides folder and returns its path.</summary>
        public string ExportEnglish(){Directory.CreateDirectory(OverridesDirectory);var path=Path.Combine(OverridesDirectory,"English.reference.json");File.WriteAllText(path,translations.ExportEnglish());return path;}

        void Mirror(){
            if(mirroredRevision==translations.Revision)return;mirroredRevision=translations.Revision;
            foreach(var entry in mirrored)jotunn.ClearToken(entry.Language,entry.Token);
            mirrored.Clear();
            foreach(var language in translations.Languages)foreach(var pair in translations.Table(language)){var token=translations.Token(pair.Key);jotunn.AddTranslation(language,token,pair.Value);mirrored.Add((language,token));}
        }
        public void Dispose(){if(disposed)return;disposed=true;foreach(var entry in mirrored)jotunn.ClearToken(entry.Language,entry.Token);mirrored.Clear();Registry.Remove(translations.Product);SettingTranslations.Remove(pluginGuid,translations);}
    }

    /// <summary>Unity side of <see cref="FontResolver"/>. Fonts created from the OS are owned by the caller.</summary>
    public static class MeadworksFonts {
        public static FontChoice Choose(string language){
            if(MeadworksHeadless.IsHeadless)return FontResolver.Select(language,Array.Empty<string>(),false);
            bool game=false;
            if(FontResolver.UsesGameFont(language))foreach(var font in Resources.FindObjectsOfTypeAll<Font>())if(font&&font.name.IndexOf(FontResolver.GameFont,StringComparison.OrdinalIgnoreCase)>=0){game=true;break;}
            string[] installed;try{installed=Font.GetOSInstalledFontNames();}catch(Exception){installed=Array.Empty<string>();}
            return FontResolver.Select(language,installed,game);
        }
        /// <summary>On a headless server no OS font is created; the built-in fallback is returned.</summary>
        public static Font For(string language,int size=18){
            var choice=Choose(language);
            switch(choice.Kind){
                case FontKind.Game:foreach(var font in Resources.FindObjectsOfTypeAll<Font>())if(font&&font.name.IndexOf(FontResolver.GameFont,StringComparison.OrdinalIgnoreCase)>=0)return font;break;
                case FontKind.OperatingSystem:return Font.CreateDynamicFontFromOSFont(choice.Name,size);
            }
            return Resources.GetBuiltinResource<Font>(FontResolver.BuiltinFont);
        }
    }
}
