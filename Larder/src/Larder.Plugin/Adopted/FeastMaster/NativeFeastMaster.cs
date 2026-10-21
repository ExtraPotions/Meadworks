// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Owning plugin integration for the pinned FeastMaster native lifecycle; no independent ticker or transport.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    public static class NativeFeastMaster
    {
        public static NativeBindings Bindings { get; private set; }
        public static MeadworksLog Log { get; private set; }
        public static ConfigEntry<bool> InstantKitchenSetting { get; private set; }
        public static ConfigEntry<bool> EndlessFoodSetting { get; private set; }
        public static ConfigEntry<bool> EatAnythingSetting { get; private set; }
        public static bool InstantKitchen => Value(InstantKitchenSetting);
        public static bool EndlessFood => Value(EndlessFoodSetting);
        public static bool EatAnything => Value(EatAnythingSetting);
        static Harmony harmony;
        static bool running;
        static Func<string,bool> active;
        static readonly Dictionary<ConfigEntryBase,string> entries=new Dictionary<ConfigEntryBase,string>();
        static readonly Dictionary<string,bool> states=new Dictionary<string,bool>();
        static readonly Dictionary<Type,string> groups=new Dictionary<Type,string>
        {
            [typeof(PlayerEatFoodPatch)]="Foods",
            [typeof(SEManAddStatusEffectPatch)]="Foods",
            [typeof(ContinuousFoodHealingPatch)]="Recovery",
            [typeof(RestDelayPatch)]="Recovery",
            [typeof(StaminaRegenMultiplierPatch)]="Recovery",
            [typeof(SwimmingRegenDelayPatch)]="Recovery",
            [typeof(EitrRegenMultiplierPatch)]="Recovery",
            [typeof(StaminaRegenDelayPatch)]="Recovery",
            [typeof(EitrRegenDelayPatch)]="Recovery",
            [typeof(UpdateStatsPatch)]="Recovery",
            [typeof(BaseValuesPatch)]="Exertion",
            [typeof(RunCostPatch)]="Exertion",
            [typeof(JumpCostPatch)]="Exertion",
            [typeof(DodgeCostPatch)]="Exertion",
            [typeof(SneakCostPatch)]="Exertion",
            [typeof(SwimCostPatch)]="Exertion",
            [typeof(BlockCostPatch)]="Exertion",
            [typeof(AttackCostPatch)]="Exertion",
            [typeof(ToolCostPatch)]="Exertion",
            [typeof(FishingCostPatch)]="Exertion",
            [typeof(FishStaminaUsePatch)]="Exertion",
            [typeof(HarpoonCostPatch)]="Exertion",
            [typeof(WorldRatesPatch)]="Exertion",
            [typeof(CookingPatch)]="Kitchen",
            [typeof(FermentationTimePatch)]="Kitchen",
            [typeof(TapYieldPatch)]="Kitchen",
            [typeof(FeastStackPatch)]="Kitchen",
            [typeof(FeastShareLeftPatch)]="Kitchen",
            [typeof(FeastHoverPatch)]="Kitchen",
            [typeof(EatAgainPatch)]="Eating",
            [typeof(DrowningDamagePatch)]="Exertion",
            [typeof(PlayerFoodDegradationPatch)]="Eating",
            [typeof(FoodSlotsCanEatPatch)]="Eating",
            [typeof(FoodSlotsEatPatch)]="Eating",
            [typeof(AutoEatPatch)]="Eating",
        };
        static readonly Skills.SkillType[] gainSkills={Skills.SkillType.Run,Skills.SkillType.Jump,Skills.SkillType.Sneak,Skills.SkillType.Swim,Skills.SkillType.Fishing};
        static readonly SkillModifier[] gains=new SkillModifier[5];
        internal static void Note(ConfigEntryBase entry,string module)=>entries[entry]=module;
        public static bool Active(string module)=>running&&active!=null&&active(module);
        public static T Value<T>(ConfigEntry<T> entry)
        {
            if(entry==null)return default;
            object value=ValueBoxed(entry);
            return value is T typed?typed:(T)entry.DefaultValue;
        }
        public static object ValueBoxed(ConfigEntryBase entry)
        {
            if(entry is ConfigEntry<bool> cheat && MeadworksConfig.TryGetMetadata(entry,out var metadata)&&metadata.Scope==ConfigScope.Cheat)
                return Active("Cheats")&&MeadworksConfig.Effective(cheat,Plugin.Guid,ConfigScope.Cheat,false);
            object value=entries.TryGetValue(entry,out string module)&&!Active(module)?entry.DefaultValue:entry.BoxedValue;
            return value is float number&&(float.IsNaN(number)||float.IsInfinity(number))?entry.DefaultValue:value;
        }
        internal static bool AllowsPatch(Type type)
        {
            if(type==typeof(ExtraPotions.Larder.Runtime.Adopted.FoodHud.FoodSlotFrames))
                return MeadworksHeadless.AllowsUi&&(Active("Eating")||ExtraPotions.Larder.Runtime.Adopted.FoodHud.FoodSlotFrames.Prepare());
            if(type==typeof(UpdateStatsPatch))return Active("Recovery")||Active("Exertion");
            if(type==typeof(StaminaRegenMultiplierPatch))return Active("Recovery")||SafeStamina.Enabled;
            if(type==typeof(CookingPatch)||type==typeof(FermentationTimePatch))return Active("Kitchen")||InstantKitchen;
            if(type==typeof(EatAgainPatch)||type==typeof(FoodSlotsCanEatPatch))return Active("Eating")||EatAnything;
            return !groups.TryGetValue(type,out string module)||Active(module);
        }
        public static void AddPatchTypes(IEnumerable<Type> types,string module="Eating")
        {
            var array=types.ToArray();foreach(var type in array)groups[type]=module;
            PatchSwitch.AddPatchTypes(array);
        }
        public static void Initialize(ConfigFile config,MeadworksLog log,Func<string,bool> moduleActive)
        {
            if(running)throw new InvalidOperationException("Larder native food lifecycle already initialized.");
            Log=log;active=moduleActive;Bindings=new NativeBindings(config);running=true;
            FeastMasterData.Initialize(Bindings);Settings.Initialize(Bindings);
            InstantKitchenSetting=MeadworksConfig.BindCheat(config,"9 - Cheats","Instant Kitchen",false,"Stations finish at the next tick, fermenters are ready at once, and nothing burns.");
            EndlessFoodSetting=MeadworksConfig.BindCheat(config,"9 - Cheats","Endless Food",false,"Active food timers hold while enabled.");
            EatAnythingSetting=MeadworksConfig.BindCheat(config,"9 - Cheats","Eat Anything",false,"Food slots never report full and food can be eaten again below its complete duration.");
            Note(InstantKitchenSetting,"Cheats");Note(EndlessFoodSetting,"Cheats");Note(EatAnythingSetting,"Cheats");
            ItemValues.HookConfig(config);WorldRates.HookConfig(config);config.SettingChanged+=Changed;config.ConfigReloaded+=Reloaded;
            harmony=new Harmony("com.meadworks.larder.feastmaster");ItemValues.UseHarmony(harmony);
            PatchSwitch.Initialize(harmony,typeof(NativeFeastMaster).Assembly);
            FeastMasterData.LoadConfigurations();
            if(ZNetScene.instance)ScenePrefabs.Postfix(ZNetScene.instance);
            PatchSwitch.MarkDirty();
        }
        static void Changed(object sender,SettingChangedEventArgs args)=>PatchSwitch.MarkDirty();
        static void Reloaded(object sender,EventArgs args)=>PatchSwitch.MarkDirty();
        public static void Tick()
        {
            if(!running)return;
            bool changed=false;
            foreach(string module in new[]{"Foods","Eating","Recovery","Exertion","Kitchen","Cooking","Cheats"})
            {
                bool now=Active(module);
                if(!states.TryGetValue(module,out bool before)||before!=now){states[module]=now;changed=true;}
            }
            if(changed){ItemValues.MarkAll();PatchSwitch.MarkDirty();}
            PatchSwitch.Tick();ItemValues.Tick();RefreshGains();
        }
        static void RefreshGains()
        {
            var settings=new[]{Settings.RunSkillGain,Settings.JumpSkillGain,Settings.SneakSkillGain,Settings.SwimSkillGain,Settings.FishingSkillGain};
            for(int i=0;i<gains.Length;i++)
            {
                float value=Value(settings[i]);
                if(!Active("Exertion")||value==1f){gains[i]?.Dispose();gains[i]=null;}
                else if(gains[i]==null)gains[i]=MeadworksSkills.AddModifier(Plugin.Guid,gainSkills[i],SkillModifierKind.Experience,value);
                else if(gains[i].Value!=value)gains[i].Value=value;
            }
        }
        public static void Shutdown()
        {
            if(!running)return;
            running=false;
            // Disabled reads expose the bound originals before gameplay patches/spawn callbacks are retired.
            try{ItemValues.ApplyAll();}
            finally
            {
                foreach(var gain in gains)gain?.Dispose();Array.Clear(gains,0,gains.Length);
                Bindings.Config.SettingChanged-=Changed;Bindings.Config.ConfigReloaded-=Reloaded;
                ItemValues.UnhookConfig(Bindings.Config);WorldRates.UnhookConfig(Bindings.Config);
                PatchSwitch.Shutdown();harmony?.UnpatchSelf();
                ItemValues.Reset();FeastMasterData.Reset();CookTimes.Reset();Rested.Reset();RestDelay.Reset();entries.Clear();
                states.Clear();active=null;harmony=null;
            }
        }
    }
}
