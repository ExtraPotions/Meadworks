using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>C09 converter adapters over BepInEx config files (pure rules in <see cref="ConfigConverter"/>).</summary>
    public static partial class MeadworksConversion {
        const long MaxSourceBytes=1024*1024;

        /// <summary>Reads a source mod's .cfg as "Prefix.Section.Key" settings for <see cref="ConfigConverter.Preview"/>.</summary>
        public static IReadOnlyList<KeyValuePair<string,string>> ReadSourceConfig(string path,string prefix){
            var info=new FileInfo(path);
            if(!info.Exists)return Array.Empty<KeyValuePair<string,string>>();
            if(info.Length>MaxSourceBytes)throw new InvalidDataException("Source config exceeds 1 MiB.");
            return CfgText.Read(File.ReadAllText(info.FullName),prefix);
        }

        /// <summary>Loads a mapping file; FormatException for malformed files, NotSupportedException for a newer schema.</summary>
        public static ConfigConverter LoadMapping(string path){
            var info=new FileInfo(path);
            if(info.Length>MaxSourceBytes)throw new InvalidDataException("Mapping exceeds 1 MiB.");
            return new ConfigConverter(ConversionMapping.Parse(File.ReadAllText(info.FullName)));
        }

        public static IConversionTarget Target(ConfigFile config)=>new ConfigFileTarget(config);

        /// <summary>
        /// Burrow's RenamedKeys over the shared plan: the old line's value when the cfg has no new key yet, else
        /// <paramref name="fallback"/>. Call before binding the new key; the carried old line is dropped on next save.
        /// </summary>
        public static T CarryRenamed<T>(ConfigFile config,RenamedKey rename,T fallback){
            var orphans=Orphans(config);if(orphans==null||rename==null)return fallback;
            var text=orphans.ToDictionary(o=>o.Key.Section+"."+o.Key.Key,o=>o.Value,StringComparer.Ordinal);
            var present=config.Keys.Select(k=>k.Section+"."+k.Key).Concat(orphans.Keys.Select(k=>k.Section+"."+k.Key));
            var plan=RenamedKeys.Plan(text,present,new[]{rename});
            if(plan.Carry.Count==0)return fallback;
            try{
                var value=TomlTypeConverter.ConvertToValue<T>(plan.Carry[0].Value);
                orphans.Remove(new ConfigDefinition(rename.Section,rename.OldKey));
                return value;
            }catch(Exception){return fallback;}
        }

        static Dictionary<ConfigDefinition,string>? Orphans(ConfigFile? config){
            if(config==null)return null;
            var property=typeof(ConfigFile).GetProperty("OrphanedEntries",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            return property?.GetValue(config) as Dictionary<ConfigDefinition,string>;
        }

        sealed class ConfigFileTarget:IValidatedConversionTarget {
            readonly ConfigFile config;
            internal ConfigFileTarget(ConfigFile config){this.config=config??throw new ArgumentNullException(nameof(config));}
            public int SchemaVersion=>config.TryGetEntry<int>("Meadworks","SchemaVersion",out var schema)?schema.Value:0;
            public void Backup(){if(File.Exists(config.ConfigFilePath))File.Copy(config.ConfigFilePath,config.ConfigFilePath+".import-"+DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)+".bak",false);}
            List<(ConfigEntryBase Entry,object Value,object Previous)> Plan(IReadOnlyDictionary<string,object> values){
                var planned=new List<(ConfigEntryBase Entry,object Value,object Previous)>();
                foreach(var pair in values){
                    // Sections and keys may both contain dots. Resolve the actual bound definition rather than
                    // inventing a split that changes an existing product's config path.
                    var matches=config.Keys.Where(key=>string.Equals(key.Section+"."+key.Key,pair.Key,StringComparison.Ordinal)).Take(2).ToArray();
                    if(matches.Length==0)throw new ArgumentException("Unknown target setting: "+pair.Key);
                    if(matches.Length!=1)throw new ArgumentException("Ambiguous target setting: "+pair.Key);
                    var entry=config[matches[0]];
                    var value=entry.SettingType.IsInstanceOfType(pair.Value)?pair.Value:TomlTypeConverter.ConvertToValue(Convert.ToString(pair.Value,CultureInfo.InvariantCulture),entry.SettingType);
                    planned.Add((entry,value,entry.BoxedValue));
                }
                return planned;
            }
            public void Validate(IReadOnlyDictionary<string,object> values)=>Plan(values);
            public void WriteAtomic(IReadOnlyDictionary<string,object> values){
                var planned=Plan(values);
                var destination=Path.GetFullPath(config.ConfigFilePath);
                var temporary=destination+".import-"+Guid.NewGuid().ToString("N")+".tmp";
                var save=config.SaveOnConfigSet;bool temporaryCreated=false;config.SaveOnConfigSet=false;
                try{
                    // Reserve an exact sibling we own. Serialize through BepInEx without changing the live file path.
                    using(var reserved=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))temporaryCreated=true;
                    foreach(var p in planned)p.Entry.BoxedValue=p.Value;
                    var ownerField=typeof(ConfigFile).GetField("_ownerMetadata",BindingFlags.Instance|BindingFlags.NonPublic)
                        ??throw new NotSupportedException("BepInEx config owner metadata is unavailable.");
                    var candidate=new ConfigFile(temporary,false,(BepInEx.BepInPlugin?)ownerField.GetValue(config)){SaveOnConfigSet=false};
                    var bind=typeof(ConfigFile).GetMethods(BindingFlags.Instance|BindingFlags.Public).Single(method=>method.Name=="Bind"&&method.IsGenericMethodDefinition&&method.GetParameters().Length==3&&method.GetParameters()[0].ParameterType==typeof(ConfigDefinition));
                    foreach(var definition in config.Keys){
                        var entry=config[definition];
                        ConfigEntryBase copy;
                        try{copy=(ConfigEntryBase)bind.MakeGenericMethod(entry.SettingType).Invoke(candidate,new[]{(object)definition,entry.DefaultValue,entry.Description})!;}
                        catch(TargetInvocationException error)when(error.InnerException!=null){ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
                        copy.BoxedValue=entry.BoxedValue;
                    }
                    var orphanCopy=Orphans(candidate)??throw new NotSupportedException("BepInEx orphaned settings are unavailable.");
                    foreach(var pair in Orphans(config)??throw new NotSupportedException("BepInEx orphaned settings are unavailable."))orphanCopy.Add(pair.Key,pair.Value);
                    candidate.Save();
                    using(var written=new FileStream(temporary,FileMode.Open,FileAccess.Write,FileShare.None))written.Flush(true);
                    if(File.Exists(destination))File.Replace(temporary,destination,null);else File.Move(temporary,destination);
                    temporaryCreated=false;
                }
                catch{foreach(var p in planned)p.Entry.BoxedValue=p.Previous;throw;}
                finally{config.SaveOnConfigSet=save;if(temporaryCreated&&File.Exists(temporary))File.Delete(temporary);}
            }
        }
    }

    /// <summary>
    /// C09 step-aside over BepInEx's chainloader: a product declares its source mods once and asks
    /// <see cref="ShouldYield"/> after every plugin has loaded (Start or later, not Awake). Each notice is written
    /// once per product and installed source set, remembered in Crucible's config.
    /// </summary>
    public static class MeadworksStepAside {
        static readonly StepAsideNotices notices=new StepAsideNotices();static ConfigEntry<string>? shown;

        internal static void Bind(ConfigFile config){
            shown=config.Bind("StepAside","NoticesShown","",new ConfigDescription("Step-aside notices already shown; managed by Meadworks.",null,new ConfigurationManagerAttributes{Browsable=false}));
            notices.Load(shown.Value);
        }

        public static void Declare(string productId,string productName,IEnumerable<SourceMod> sources)=>Plugin.Services.StepAside.Declare(productId,productName,sources);

        public static StepAsideDecision Decide(string productId)=>Plugin.Services.StepAside.Decide(productId,Chainloader.PluginInfos.Keys);

        /// <summary>True when the product must stay off because a source mod is installed; writes the notice once.</summary>
        public static bool ShouldYield(string productId){
            var decision=Decide(productId);
            if(decision.Yield&&notices.ShouldShow(decision)){
                MeadworksLog.For(Plugin.Log??BepInEx.Logging.Logger.CreateLogSource("Crucible"),"Crucible").Warning(decision.Notice);
                if(shown!=null)shown.Value=notices.Format();
            }
            return decision.Yield;
        }

        /// <summary>
        /// Reports that one module of a product stays off because <paramref name="source"/> covers it (C09 per-module
        /// step-aside); the rest of the product keeps running and whole-product step-aside is unchanged. The notice is
        /// shown once per product, module and source (HUD when ready, otherwise the log, always logged), with the text
        /// of <paramref name="textKey"/> (default <c>step_aside_&lt;module&gt;</c>, <c>{0}</c> = source name) from the
        /// product's C05 catalog <paramref name="translationProduct"/> in the game language, else Crucible's English
        /// sentence. Returns true when the notice was shown now.
        /// </summary>
        public static bool ReportModule(string productId,string moduleId,SourceMod source,string? translationProduct=null,string? textKey=null){
            var aside=new ModuleStepAside(productId,moduleId,source,textKey);
            Plugin.Services.StepAside.ReportModule(aside);
            if(!notices.ShouldShow(aside))return false;
            var text=ModuleNotice(aside,translationProduct);
            (Plugin.Log??BepInEx.Logging.Logger.CreateLogSource("Crucible")).LogInfo("[Meadworks:Crucible] "+text);
            if(MeadworksHeadless.Channel==NoticeChannel.Hud)MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center,text);
            if(shown!=null)shown.Value=notices.Format();
            return true;
        }

        /// <summary>Whole products and modules that step aside now, with display names, for Almanac's compatibility list.</summary>
        public static IReadOnlyList<StepAsideReport> Reports()=>Plugin.Services.StepAside.Reports(Chainloader.PluginInfos.Keys,id=>{
            var name=Plugin.Services.Snapshot().FirstOrDefault(p=>p.Id==id)?.Name;const string brand="ExtraPotions Meadworks: ";
            return name!=null&&name.StartsWith(brand,StringComparison.Ordinal)?name.Substring(brand.Length):name;});

        /// <summary>Clears a module report when its source goes away or is switched off; the shown-once record stays.</summary>
        public static bool WithdrawModule(string productId,string moduleId)=>Plugin.Services.StepAside.WithdrawModule(productId,moduleId);

        /// <summary>The module's notice text in the game language, for products that also show it in their own UI.</summary>
        public static string ModuleNotice(ModuleStepAside aside,string? translationProduct=null){
            if(aside==null)throw new ArgumentNullException(nameof(aside));
            ProductTranslations? translations=null;
            if(!string.IsNullOrWhiteSpace(translationProduct))MeadworksTranslations.Registry.TryGet(translationProduct!,out translations);
            var name=Plugin.Services.Snapshot().FirstOrDefault(p=>p.Id==aside.ProductId)?.Name;
            return ModuleStepAsideText.Notice(aside,name,translations,MeadworksTranslations.GameLanguage());
        }
    }
}
