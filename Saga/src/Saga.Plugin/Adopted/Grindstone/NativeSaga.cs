// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Integration;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone {
    public static class NativeSaga {
        public static ManualLogSource Log=null!;
        public static readonly NativeData Data=new NativeData();
        static readonly List<IDisposable> leases=new List<IDisposable>();
        static readonly HashSet<string> yielded=new HashSet<string>(StringComparer.Ordinal);
        static Harmony? harmony;
        static bool started;
        // Overlap inputs: the plugin set, plus the config files ReadSwitch read (entry count catches late Bind, events catch value changes).
        static readonly Dictionary<BepInEx.Configuration.ConfigFile,int> watched=new Dictionary<BepInEx.Configuration.ConfigFile,int>();
        static volatile bool overlapDirty;static int pluginCount=-1;
        static int runtimeFailures;static Player? previousPlayer;
        public static IReadOnlyList<string> Describe()=>new[]{"ready="+Plugin.Ready,"started="+started,"patches="+Harmony.GetAllPatchedMethods().Count(m=>Harmony.GetPatchInfo(m)?.Owners.Contains(Plugin.Guid+".native")==true),"yielded="+string.Join(",",yielded),"pages="+NativeBook.PageCount,"runtime_failures="+runtimeFailures};
        public static bool Active(string module) {
            if(!started||!Plugin.Ready||Plugin.Settings==null||!MeadworksAuthority.ProductActive(Plugin.Guid,out _)||!Plugin.Settings.Get(SagaSettings.General.Enabled)||yielded.Contains(module))return false;
            return module=="progression"||module=="catchup"||Plugin.Settings.Get(Switch(module)!);
        }
        // Each module's switch setting, found once per module name: the key concatenation allocated on every call, and per-frame hooks call Active.
        static readonly Dictionary<string,SagaSetting<bool>?> switches=new Dictionary<string,SagaSetting<bool>?>(StringComparer.Ordinal);
        static SagaSetting<bool>? Switch(string module){
            if(switches.TryGetValue(module,out var setting))return setting;
            var key=module=="weaponmastery"?"Weapon Mastery Enabled":module=="sneak_speed"||module=="sneak_effects"?"Sneak Enabled":char.ToUpperInvariant(module[0])+module.Substring(1)+" Enabled";
            setting=(SagaSetting<bool>?)SagaSettings.Find(SagaSettings.General.Section+"."+key);switches[module]=setting;return setting;
        }
        public static bool SneakSpeedActive=>Active("sneak_speed");
        public static bool SneakEffectsActive=>Active("sneak_effects");
        public static float SneakSpeed=>SneakSpeedActive?Plugin.Settings!.Get(SagaSettings.Sneak.SpeedAt100):1f;
        public static float SneakVisibility=>SneakEffectsActive?Plugin.Settings!.Get(SagaSettings.Sneak.VisibilitySkillFactor):1f;
        public static bool KeepCrouching=>SneakEffectsActive&&Plugin.Settings!.Get(SagaSettings.Sneak.KeepCrouching);
        public static float BackstabExperience=>SneakEffectsActive?Plugin.Settings!.Get(SagaSettings.Sneak.BackstabExperience):0f;
        public static bool TryView(Player player,out ZNetView? view){view=player?.m_nview;return view!=null&&view.IsValid();}
        // Per-frame hooks report through here: each context logs its first failure, then at most once a minute with the suppressed count.
        static readonly Dictionary<string,(double next,int suppressed)> reported=new Dictionary<string,(double,int)>(StringComparer.Ordinal);
        static readonly System.Diagnostics.Stopwatch reportClock=System.Diagnostics.Stopwatch.StartNew();
        public static void Report(Exception error,string context){
            runtimeFailures++;string line;context??="";
            lock(reported){
                double now=reportClock.Elapsed.TotalSeconds;
                if(reported.TryGetValue(context,out var gate)&&now<gate.next){reported[context]=(gate.next,gate.suppressed+1);return;}
                reported[context]=(now+60f,0);
                line="Saga "+context+": "+error.GetType().Name+": "+error.Message+(gate.suppressed>0?" ("+gate.suppressed+" more in the last minute)":"");
            }
            Log.LogWarning(line);
        }
        public static void Initialize(ManualLogSource log) {
            Log=log;
            SagaText.Initialize(log);
            var config=new NativeConfiguration();
            Data.AddPowers();
            DefenseSettings.Initialize(config);WoodcuttingSettings.Initialize(config);PickaxeSettings.Initialize(config);ForagingSettings.Initialize(config);FishingSettings.Initialize(config);DeathSettings.Initialize(config);
            foreach(var skill in SagaSkills.All.Where(s=>s.IsCustom)) {
                var definition=new SkillDefinition(Plugin.Guid,skill.Identity,skill.Name,skill.Description,publishKey:skill.PublishKey,importFrom:new[]{skill.ImportFrom},textKey:skill.TextKey);
                if(definition.Id.Value!=skill.GameType)throw new InvalidOperationException("Saga skill identity changed: "+skill.Identity);
                var captured=skill;
                leases.Add(MeadworksSkills.Register(definition,()=>EmbeddedIcon.Load("skill_"+captured.Skill.ToString().ToLowerInvariant()),SagaText.Catalog));
            }
            foreach(var skill in SagaSkills.All.Where(s=>!s.IsCustom&&s.IconResource.Length>0)) {
                var captured=skill;leases.Add(MeadworksSkills.RegisterIcon(Plugin.Guid,(global::Skills.SkillType)skill.GameType,()=>EmbeddedIcon.Load("skill_"+captured.Skill.ToString().ToLowerInvariant())));
            }
            leases.Add(MeadworksSkills.RegisterPublishedLevel(Plugin.Guid,global::Skills.SkillType.WoodCutting,"saga.wood_level"));
            leases.Add(MeadworksSkills.RegisterPublishedLevel(Plugin.Guid,global::Skills.SkillType.Pickaxes,"saga.mine_level"));
            leases.Add(MeadworksSkills.RegisterPublishedLevel(Plugin.Guid,global::Skills.SkillType.Fishing,"saga.fishing_level"));
            RecoveryXp.Tick();
            NativeBook.Initialize();
        }
        public static void Start() {
            RefreshOverlap();
            started=true;
            harmony=new Harmony(Plugin.Guid+".native");harmony.PatchAll(typeof(NativeSaga).Assembly);
        }
        static void MarkOverlapDirty(object sender,EventArgs e)=>overlapDirty=true;
        static bool OverlapInputsChanged(){
            if(overlapDirty||Chainloader.PluginInfos.Count!=pluginCount)return true;
            foreach(var pair in watched)if(pair.Key.Count!=pair.Value)return true;
            return false;
        }
        static void Watch(BepInEx.Configuration.ConfigFile config){
            if(!watched.ContainsKey(config)){config.SettingChanged+=MarkOverlapDirty;config.ConfigReloaded+=MarkOverlapDirty;}
            watched[config]=config.Count;
        }
        static void Unwatch(){foreach(var config in watched.Keys){config.SettingChanged-=MarkOverlapDirty;config.ConfigReloaded-=MarkOverlapDirty;}watched.Clear();overlapDirty=false;pluginCount=-1;}
        static void RefreshOverlap(){
            overlapDirty=false;pluginCount=Chainloader.PluginInfos.Count;
            var overlaps=SourceOverlap.Decide(new HashSet<string>(Chainloader.PluginInfos.Keys,StringComparer.Ordinal),ReadSwitch);
            foreach(var old in yielded.Where(m=>!overlaps.Any(o=>o.Module==m)).ToArray())MeadworksStepAside.WithdrawModule(Plugin.Guid,old);
            yielded.Clear();
            foreach(var item in overlaps){yielded.Add(item.Module);MeadworksStepAside.ReportModule(Plugin.Guid,item.Module,new SourceMod(item.Source,item.Name),"saga");}
        }
        static bool? ReadSwitch(string guid,string key) {
            if(!Chainloader.PluginInfos.TryGetValue(guid,out var info)||!(info.Instance is BepInEx.BaseUnityPlugin source))return null;
            Watch(source.Config);
            var matches=source.Config.Keys.Where(k=>k.Key==key).ToArray();if(matches.Length!=1)return null;
            var value=source.Config[matches[0]].BoxedValue;
            if(value is bool boolean)return boolean;
            if(value?.ToString()=="On")return true;if(value?.ToString()=="Off")return false;return null;
        }
        static void ResetDefense(){DefenseEffects.Shutdown(previousPlayer!);Recovery.Reset();Riposte.Reset();Hardened.Reset();LastStand.Reset();}
        public static void Tick(){if(!started)return;if(OverlapInputsChanged())RefreshOverlap();Data.Tick();BackstabCredit.Tick();RecoveryXp.Tick();NativeRunning.Tick();NativeSwimming.Tick();NativeDodging.Tick();NativeMelee.Tick();SagaText.Tick();NativePowers.Tick();NativeAngler.Tick();PowerCommands.Tick();FishLogCommand.Ensure();TensionBar.Tick();var player=Player.m_localPlayer;if(player!=previousPlayer){ResetDefense();previousPlayer=player;}if(player!=null){SagaConverter.ImportCharacter(player);Recovery.Tick(player,Time.deltaTime);DefenseEffects.Tick(Time.deltaTime);}}
        public static void Shutdown(){started=false;harmony?.UnpatchSelf();harmony=null;NativeRpcCleanup.Shutdown();NativePowers.Shutdown();NativeAngler.Shutdown();FishLogCommand.Shutdown();NativeBook.Shutdown();RecoveryXp.Shutdown();NativeRunning.Shutdown();NativeSwimming.Shutdown();NativeDodging.Shutdown();NativeMelee.Shutdown();ResetDefense();previousPlayer=null;FishMark.Shutdown();Ticker.Shutdown();Data.Clear();TensionBar.Dispose();for(var i=leases.Count-1;i>=0;i--)leases[i].Dispose();leases.Clear();foreach(var module in yielded)MeadworksStepAside.WithdrawModule(Plugin.Guid,module);yielded.Clear();Unwatch();SagaText.Shutdown();}
    }
    public static class DefenseSkill {
        public const float MaxLevel=100f;
        public static global::Skills.SkillType Type=>(global::Skills.SkillType)SagaSkills.DefenseType;
        public static bool Active=>NativeSaga.Active("defense");
        public static float Local()=>Of(Player.m_localPlayer);
        public static float Of(Player player)=>MeadworksSkills.EffectiveLevel(player,Type);
        public static bool IsLocal(Character character)=>character!=null&&character==Player.m_localPlayer;
        public static float Factor(float level)=>Mathf.Clamp01(level/100f);
        public static float Share(float percent,float level)=>Mathf.Max(0,percent)/100f*Factor(level);
        public static float LocalShare(float percent)=>Active?Share(percent,Local()):0;
        public static bool Reached(float level,float milestone)=>milestone<=100&&level>=milestone;
        public static bool LocalReached(float milestone)=>Active&&Reached(Local(),milestone);
    }
    public static class ForagingSkill {
        public const float MaxLevel=100f;
        public static global::Skills.SkillType Type=>(global::Skills.SkillType)SagaSkills.ForagingType;
        public static bool Active=>NativeSaga.Active("foraging");
        public static float Local()=>Of(Player.m_localPlayer);
        public static float Of(Player player)=>MeadworksSkills.EffectiveLevel(player,Type);
        public static float Factor(float level)=>Mathf.Clamp01(level/100f);
        public static float Level(Player player)=>Of(player);
        public static float Share(float percent,float level)=>Mathf.Max(0,percent)/100f*Factor(level);
        public static bool Reached(float level,float milestone)=>milestone<=100&&level>=milestone;
    }
    public static class DefenseIcon {public static Sprite Find()=>EmbeddedIcon.Load("skill_defense");}
    public static class ForagingIcon {public static Sprite Find()=>EmbeddedIcon.Load("skill_foraging");}
}
