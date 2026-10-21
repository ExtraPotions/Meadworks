using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Crucible;
using Wayfarer.Core;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;
namespace Wayfarer.CrucibleIntegration {
    /// <summary>Wayfarer's Crucible services: server-authoritative settings, teleport authority operations, navigation capability, kit UI and C05 translations. The plugin registers the product and owns the registration.</summary>
    public sealed class CrucibleServices:IDisposable {
        readonly ProductRegistration registration;MeadworksTranslations? localization;readonly MeadworksLog log;TeleportAuthorityAdapter? authority;
        ConfigEntry<bool> allowTeleport=null!,allowBed=null!;
        ConfigEntry<bool> stones=null!,map=null!,boat=null!,grave=null!,player=null!;
        public SessionSnapshot Session=>CruciblePlugin.Session;
        public TeleportPolicy EffectivePolicy=>new TeleportPolicy(allowTeleport.Value,allowBed.Value,stones.Value,map.Value,boat.Value,grave.Value,player.Value,PlayerCategories());
        // Travel destinations the admin lets every player use (Players May Use), whatever the Mischief Travel policy.
        IReadOnlyCollection<Category> PlayerCategories(){
            var c=new List<Category>();
            if(MeadworksMischief.PlayersMay(stones))c.Add(Category.Stones);if(MeadworksMischief.PlayersMay(map))c.Add(Category.Map);
            if(MeadworksMischief.PlayersMay(boat))c.Add(Category.Boat);if(MeadworksMischief.PlayersMay(grave))c.Add(Category.Grave);
            if(MeadworksMischief.PlayersMay(player))c.Add(Category.Player);
            return c;
        }
        /// <summary>The local player's Mischief travel access (C13): single player and the host always; on a server, the Travel policy (admins by default) or destinations the admin lets players use.</summary>
        public bool TravelCheats=>travelCheats.Value;
        // C13 asks for the platform user and session facts; the panel, map patch and navigation ask several times a frame.
        readonly PerFrame<bool> travelCheats;
        static readonly string ProductId=MeadworksIntegration.Metadata().Id;
        public double MonotonicTime=>SessionIntegration.Now;
        public CrucibleServices(ProductRegistration registration,BepInPlugin metadata,ConfigFile config,ManualLogSource logger,Func<NavigationSnapshot> navigation) {
            this.registration=registration;
            travelCheats=new PerFrame<bool>(()=>UnityEngine.Time.frameCount,()=>MeadworksMischief.Decide(ProductId,MischiefCategory.Travel).Allowed);
            log=MeadworksLog.For(logger,"Wayfarer");
            try {
                allowTeleport=Bind(config,ServerConfigurationKeys.Section,ServerConfigurationKeys.AllowTeleport,true,"Master switch for every Wayfarer destination on this server.",ConfigScope.ServerAuthoritative);
                allowBed=Bind(config,ServerConfigurationKeys.Section,ServerConfigurationKeys.AllowBed,true,"Allow Bed teleport for every player.",ConfigScope.ServerAuthoritative);
                stones=MeadworksConfig.BindCheat(config,CheatKeys.Section,CheatKeys.Stones,false,"Travel cheat: teleport to discovered Sacrificial Stones. Admins only on a server unless the admin lets players use it.",MischiefCategory.Travel);
                map=MeadworksConfig.BindCheat(config,CheatKeys.Section,CheatKeys.Map,false,"Travel cheat: teleport to any clicked map point. Admins only on a server unless the admin lets players use it.",MischiefCategory.Travel);
                boat=MeadworksConfig.BindCheat(config,CheatKeys.Section,CheatKeys.Boat,false,"Travel cheat: teleport to a seen boat. Admins only on a server unless the admin lets players use it.",MischiefCategory.Travel);
                grave=MeadworksConfig.BindCheat(config,CheatKeys.Section,CheatKeys.Grave,false,"Travel cheat: teleport to your own grave. Admins only on a server unless the admin lets players use it.",MischiefCategory.Travel);
                player=MeadworksConfig.BindCheat(config,CheatKeys.Section,CheatKeys.Player,false,"Travel cheat: teleport to a player who shares their map position. Admins only on a server unless the admin lets players use it.",MischiefCategory.Travel);
                authority=new TeleportAuthorityAdapter(registration,()=>EffectivePolicy);
                if(MeadworksHeadless.AllowsUi)registration.AddUi(MeadworksIntegration.Ui());
                var provider=new NavigationProvider(navigation);
                registration.AddCapability<IWayfarerNavigation>("meadworks.navigation",new System.Version(1,0),provider);
                registration.AddCapability<IWayfarerNavigation>("meadworks.mischief",new System.Version(1,0),provider);
                // C05: the embedded assets/localization catalog under the "wayfarer" token namespace, with validated hot-reload overrides in BepInEx/config/Meadworks/wayfarer/localization.
                if(MeadworksHeadless.AllowsUi)localization=MeadworksTranslations.Register(metadata,new ProductTranslations("wayfarer",EmbeddedCatalog()),log:logger);
            } catch {Dispose();throw;}
        }
        public ConfigEntry<T> Bind<T>(ConfigFile config,string section,string key,T defaultValue,ConfigDescription description)=>MeadworksConfig.Bind(config,section,key,defaultValue,description);
        public ConfigEntry<T> Bind<T>(ConfigFile config,string section,string key,T defaultValue,string description,ConfigScope scope)=>MeadworksConfig.Bind(config,section,key,defaultValue,description,scope);
        public Task<OperationResult<TeleportGrant>> AuthorizeAsync(TeleportRequest request,CancellationToken cancellation)=>CruciblePlugin.RequestClient.InvokeAsync<TeleportRequest,TeleportGrant>(SessionIntegration.Authorize,request,cancellation);
        public Task<OperationResult<bool>> CompleteAsync(TeleportCompletion completion,CancellationToken cancellation)=>CruciblePlugin.RequestClient.InvokeAsync<TeleportCompletion,bool>(SessionIntegration.Complete,completion,cancellation);
        static IReadOnlyDictionary<string,string> EmbeddedCatalog(){
            var json=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var assembly=typeof(CrucibleServices).Assembly;
            foreach(var language in MeadworksLanguages.Supported)
                using(var stream=assembly.GetManifestResourceStream("Wayfarer.localization."+language+".json"))
                    if(stream!=null)using(var reader=new StreamReader(stream))json.Add(language,reader.ReadToEnd());
            return json;
        }
        public string Text(string key,string language)=>localization?.Translations.Text(key,language)??key;
        /// <summary>Client frame tick: polls translation overrides (C05).</summary>
        public void Update()=>localization?.Update();
        /// <summary>C09 per-module step-aside: reports the module and its source to Crucible (notice once, Wayfarer C05 text).</summary>
        public void ReportStepAside(string moduleId,string sourceGuid,string sourceName)=>MeadworksStepAside.ReportModule(ProductId,moduleId,new SourceMod(sourceGuid,sourceName),"wayfarer");
        public void Info(string message)=>log.Info(message);
        public void Error(object error)=>log.Error(error);
        public void Dispose(){authority?.Dispose();localization?.Dispose();authority=null;localization=null;}
    }
}
