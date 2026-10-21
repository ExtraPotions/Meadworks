// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Larder.Runtime.Adopted.FeastMaster;
using ExtraPotions.Larder.Runtime.Adopted.FoodHud;
using ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking;
using Jotunn.Utils;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using FMSettings = ExtraPotions.Larder.Runtime.Adopted.FeastMaster.Settings;

namespace ExtraPotions.Larder.Runtime
{
    /// <summary>
    /// Larder (Hearth): food and mead values, eating, recovery, exertion, kitchen stations and the Cooking skill's
    /// perks. Required on all peers (design section 5): item values live in every peer's shared data and the station
    /// and barrel RPCs need a handler on every peer; major.minor must match.
    /// Native reference modules share configuration, authority, skill hooks and capabilities through Crucible.
    /// Source coexistence is per module; server configuration stays bound when a reference owns gameplay.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [BepInDependency(CrucibleUiPlugin.Guid, CrucibleUiPlugin.Version)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = LarderCore.ProductId;
        public const string Name = LarderCore.Name;
        public const string Version = LarderCore.Version;
        public const string Family = LarderCore.Family;

        private ProductRegistration? registration;
        private MeadworksLog? log;
        private LarderReferenceImport? referenceImport;
        private bool started;
        private static bool foodsAside,cookingAside,restDelayAside;
        private const string FoodSourceId=Guid+".food",CookingSourceId=Guid+".cooking",RestDelaySourceId=Guid+".rest_delay";

        /// <summary>Registered with Crucible and running.</summary>
        public static bool Ready { get; private set; }

        /// <summary>This process may build Larder UI (a graphical client). False on a dedicated server.</summary>
        public static bool AllowsUi { get; private set; }
        public static bool SteppedAside=>foodsAside&&cookingAside;
        public static bool RestDelaySteppedAside=>restDelayAside;
        public static bool GameplayAvailable=>Ready&&MeadworksAuthority.ProductActive(Guid,out _);

        public static bool ModuleActive(string module){
            if(!GameplayAvailable)return false;
            switch(module){
                case "Foods":case "Eating":case "Recovery":case "Exertion":case "Kitchen":return !foodsAside;
                case "Cooking":return !cookingAside;
                case "Cheats":return !foodsAside&&!cookingAside;
                default:return false;
            }
        }

        static IReadOnlyList<string> Describe()=>new[]{
            "food/eating/recovery/exertion/kitchen: "+(foodsAside?"steps aside for FeastMaster":ModuleActive("Foods")?"active":"session inactive"),
            "Cooking: "+(cookingAside?"steps aside for GrindstoneSkills":ModuleActive("Cooking")?"active":"session inactive"),
            "Rested delay: "+(restDelayAside?"steps aside for OpenKeep":RestDelayPatch.Prepare()?"custom delay":"native delay"),
            "cheat gate: "+MeadworksAuthority.Decide(Guid,ConfigScope.Cheat).Reason,
            "native patch groups: "+string.Join(", ",HarmonyLib.Harmony.GetAllPatchedMethods().Where(method=>{
                var info=HarmonyLib.Harmony.GetPatchInfo(method);return info!=null&&info.Owners.Any(owner=>owner.StartsWith(Guid+".",StringComparison.Ordinal));
            }).Select(method=>method.DeclaringType?.Name+"."+method.Name).OrderBy(name=>name,StringComparer.Ordinal))
        };

        private void Awake()
        {
            log = MeadworksLog.For(Logger, Name);
            try
            {
                if (!CruciblePlugin.IsReady)
                    throw new InvalidOperationException("Required Crucible is not ready");
                registration = CruciblePlugin.Services.Register(new ProductMetadata(Guid, Name, new System.Version(Version),
                    new System.Version(0, 1), new System.Version(0, 1), MultiplayerPolicy.RequiredOnAllPeers, Family));
                AllowsUi = MeadworksHeadless.AllowsUi;
                LarderText.Initialize(Info.Metadata,Logger);
                if(CruciblePlugin.Services.StepAside.Declared(FoodSourceId).Count==0)
                    MeadworksStepAside.Declare(FoodSourceId,"Larder food and kitchen rules",new[]{new SourceMod("com.FeastMaster","FeastMaster")});
                if(CruciblePlugin.Services.StepAside.Declared(CookingSourceId).Count==0)
                    MeadworksStepAside.Declare(CookingSourceId,"Larder Cooking rules",new[]{new SourceMod("com.GrindstoneSkills","GrindstoneSkills")});
                if(CruciblePlugin.Services.StepAside.Declared(RestDelaySourceId).Count==0)
                    MeadworksStepAside.Declare(RestDelaySourceId,"Larder Rested delay",new[]{new SourceMod("milkyteam.openkeep","OpenKeep")});
                if(AllowsUi)NativeFoodHud.Register();
                NativeFeastMaster.Initialize(Config,log,ModuleActive);
                SafeStamina.Initialize(Config,log);
                FoodTopUp.Initialize(Config,registration);
                registration.AddCapability<Func<string,Tuple<float,bool>>>("meadworks.larder.kitchen_uses",new System.Version(1,0),prefab=>
                    ModuleActive("Cooking")?Tuple.Create(KitchenUses.BestDish(prefab),KitchenUses.IsIngredient(prefab)):Tuple.Create(0f,false));
                registration.AddCapability<Func<Tuple<int,float>>>("meadworks.larder.hud.foodslots",new System.Version(1,0),()=>
                    AllowsUi&&(ModuleActive("Eating")||FoodSlotsHud.CurrentHudExtended)?NativeFoodHud.CapabilityGeometry():Tuple.Create(0,0f));
                registration.AddCapability<Func<int>>("meadworks.larder.foodslots.count",new System.Version(1,0),()=>ModuleActive("Eating")?NativeFeastMaster.Value(FMSettings.FoodSlots):-1);
                registration.AddCapability<Func<IReadOnlyList<string>>>("meadworks.diagnostics."+Guid,new System.Version(1,0),Describe);
                Ready = true;
                log.Info(AllowsUi ? "Larder registered; native food and Cooking modules start after discovery."
                    : "Larder registered headless; native world rules are available without HUD objects.");
            }
            catch (Exception error)
            {
                Shutdown();
                log.Error("Larder initialization rejected: " + error);
            }
        }

        private void Start(){
            if(!Ready||log==null)return;
            try{
                foodsAside=MeadworksStepAside.ShouldYield(FoodSourceId);
                cookingAside=MeadworksStepAside.ShouldYield(CookingSourceId);
                restDelayAside=MeadworksStepAside.ShouldYield(RestDelaySourceId);
                PatchSwitch.MarkDirty();
                NativeCooking.Initialize(Config,log,()=>ModuleActive("Cooking"));
                referenceImport=new LarderReferenceImport(Config,log);
                started=true;
            }catch(Exception error){log.Error("Larder modules rejected: "+error);Shutdown();}
        }

        private void Update(){
            if(!Ready)return;
            LarderText.Tick();
            if(!started)return;
            SafeStamina.Tick();
            NativeFeastMaster.Tick();
            FoodTopUp.Tick();
            NativeCooking.Tick();
            referenceImport?.Tick();
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            Ready = false;
            started=false;
            referenceImport?.Dispose();referenceImport=null;
            foreach(Action stop in new Action[]{FoodTopUp.Shutdown,NativeCooking.Shutdown,SafeStamina.Shutdown,NativeFeastMaster.Shutdown,LarderText.Shutdown})
                try{stop();}catch(Exception error){log?.Warning("Larder teardown "+stop.Method.DeclaringType?.FullName+"."+stop.Method.Name+": "+error);}
            registration?.Dispose();
            registration = null;
            foodsAside=false;cookingAside=false;restDelayAside=false;
            AllowsUi=false;
        }
    }
}
