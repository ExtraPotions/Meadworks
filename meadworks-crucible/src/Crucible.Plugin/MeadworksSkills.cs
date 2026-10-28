using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// One registered custom skill, as a product holds it. Levels live with the character on the player's own client
    /// (the game's <c>Skills</c>, saved in the player profile); the owning client publishes the effective level on its
    /// player ZDO under <see cref="SkillDefinition.PublishKey"/> once a second, so other peers read it with <see cref="Of"/>.
    /// </summary>
    public sealed class MeadworksSkill:IDisposable {
        readonly SkillRegistration registration;
        readonly Func<Sprite?>? icon;
        internal MeadworksSkill(SkillRegistration registration,Func<Sprite?>? icon,ProductTranslations? texts){
            this.registration=registration;this.icon=icon;Texts=texts;
            GameDefinition=new Skills.SkillDef{m_skill=Type,m_description=Definition.Description,m_increseStep=Definition.IncreaseStep};
        }
        public SkillDefinition Definition=>registration.Definition;
        public Skills.SkillType Type=>(Skills.SkillType)Definition.Id.Value;
        /// <summary>The definition every <c>Skills</c> component gets; its icon is filled in once the prefabs load.</summary>
        public Skills.SkillDef GameDefinition { get; }
        public ProductTranslations? Texts { get; }
        public bool Registered { get; private set; }=true;

        /// <summary>The level products act on: the game's level (status effects included) plus product level bonuses.</summary>
        public float Of(Player? player){
            if(player==null)return 0f;
            if(player==Player.m_localPlayer)return MeadworksSkills.Modifiers.EffectiveLevel(Definition.Id,player.GetSkillLevel(Type));
            var view=player.GetComponent<ZNetView>();
            return view!=null&&view.IsValid()?PublishedLevel.Sanitize(view.GetZDO().GetFloat(Definition.PublishKey,0f)):0f;
        }
        public float Local()=>Of(Player.m_localPlayer);
        /// <summary>The saved level of the local character, without status effects or product bonuses.</summary>
        public float SavedLevel()=>MeadworksSkills.Progress(Player.m_localPlayer,Type)?.Level??0f;

        /// <summary>Earned experience for the local character: the game's raise path (status effects, world rate, level-up message).</summary>
        public void Raise(float factor=1f){if(Registered)Player.m_localPlayer?.RaiseSkill(Type,factor);}

        /// <summary>Sets the local character's level: a Cheat (<see cref="SkillEditPolicy"/>); refused with a reason otherwise.</summary>
        public bool TrySetLevel(float level,out string message)=>MeadworksSkills.Edit(this,progress=>SkillMath.SetLevel(level),out message);
        public bool TryReset(out string message)=>MeadworksSkills.Edit(this,progress=>SkillProgress.Zero,out message);

        internal void FindIcon(){
            if(GameDefinition.m_icon!=null||icon==null||MeadworksHeadless.IsHeadless)return;
            try{GameDefinition.m_icon=icon();}catch(Exception error){MeadworksSkills.Log?.Warning("Skill icon for "+Definition.Identity+" failed: "+error.GetType().Name);}
        }

        public void Dispose(){if(!Registered)return;Registered=false;registration.Dispose();MeadworksSkills.Forget(this);}
    }

    /// <summary>
    /// CS1 skill registry adapter. Products register custom skills here instead of through Jotunn's SkillManager:
    /// Jotunn derives the number with <c>Math.Abs(hash)</c> and GrindstoneSkills with <c>hash &amp; 0x7FFFFFFF</c>, and
    /// Meadworks follows GrindstoneSkills so identities and imports line up (see <see cref="SkillId"/>). The game is
    /// taught the skills the way GrindstoneSkills <c>CustomSkills</c> does (read from assembly_valheim 2026-10-06):
    /// <list type="bullet">
    /// <item><c>Skills.Awake</c> postfix adds each definition (the Player prefab's list is copied per instance).</item>
    /// <item><c>Skills.IsSkillValid</c> postfix keeps saved levels of registered skills, so the profile persists them
    /// through the game's own <c>Skills.Save</c>/<c>Load</c>.</item>
    /// <item><c>Skills.Load</c> prefix peeks the raw save block before unknown types are dropped; <c>Player.Load</c>
    /// postfix (custom data is read after skills) runs the <see cref="SkillImport"/> plan once per character.</item>
    /// <item><c>Localization.SetupLanguage</c> postfix adds <c>skill_&lt;number&gt;</c> and the description in the
    /// selected language from the product's C05 catalog; <c>ZNetScene.Awake</c> postfix asks products for icons.</item>
    /// <item><c>Skills.RaiseSkill</c> prefix applies experience modifiers (and the server-authoritative
    /// <c>Skills.ExperienceMultiplier</c> for Meadworks skills); <c>Skills.LowerAllSkills</c> applies death-loss modifiers.</item>
    /// <item><c>Skills.CheatRaiseSkill</c>/<c>CheatResetSkill</c> accept custom skill words behind the Cheat gate.</item>
    /// </list>
    /// </summary>
    public static partial class MeadworksSkills {
        public static SkillRegistry Registry { get; }=new SkillRegistry();
        public static SkillModifiers Modifiers { get; }=new SkillModifiers();
        internal static MeadworksLog? Log;
        static readonly Dictionary<SkillId,MeadworksSkill> skills=new Dictionary<SkillId,MeadworksSkill>();
        static readonly SkillLevelPublisher publisher=new SkillLevelPublisher();
        static readonly ConditionalWeakTable<Skills,SavedBlock> pendingImports=new ConditionalWeakTable<Skills,SavedBlock>();
        static Harmony? harmony;
        static ConfigEntry<float>? experienceSetting;
        static long appliedTexts=-1;static float textTimer;
        sealed class SavedBlock{public IReadOnlyList<SkillRecord> Records=Array.Empty<SkillRecord>();}

        static readonly AccessTools.FieldRef<Skills,Dictionary<Skills.SkillType,Skills.Skill>> SkillData=AccessTools.FieldRefAccess<Skills,Dictionary<Skills.SkillType,Skills.Skill>>("m_skillData");
        static readonly AccessTools.FieldRef<Skills,Player> SkillsPlayer=AccessTools.FieldRefAccess<Skills,Player>("m_player");
        static readonly System.Reflection.MethodInfo? GetSkillMethod=AccessTools.Method(typeof(Skills),"GetSkill");
        static readonly System.Reflection.MethodInfo? RebalanceMethod=AccessTools.Method(typeof(Skills),"RebalanceSkills");

        public static IReadOnlyList<MeadworksSkill> All{get{lock(skills)return skills.Values.OrderBy(s=>s.Definition.Identity,StringComparer.Ordinal).ToArray();}}

        /// <summary>
        /// Registers a product's skill (normally in the plugin's Awake). <paramref name="icon"/> returns a 64x64 sprite once
        /// the game's prefabs are loaded (never called headless); <paramref name="texts"/> is the product's C05 catalog
        /// holding <see cref="SkillDefinition.NameKey"/> and <see cref="SkillDefinition.DescriptionKey"/>.
        /// </summary>
        public static MeadworksSkill Register(SkillDefinition definition,Func<Sprite?>? icon=null,ProductTranslations? texts=null){
            lock(extensionsGate)if(publishedProviders.ContainsKey(definition.PublishKey))throw new InvalidOperationException("Skill publish key already registered: "+definition.PublishKey);
            var skill=new MeadworksSkill(Registry.Register(definition),icon,texts);
            lock(skills)skills[definition.Id]=skill;
            AddTo(Player.m_localPlayer?.GetSkills());
            if(ZNetScene.instance!=null)skill.FindIcon();
            appliedTexts=-1;ApplyTexts();
            return skill;
        }

        internal static void Forget(MeadworksSkill skill){lock(skills){if(skills.TryGetValue(skill.Definition.Id,out var current)&&ReferenceEquals(current,skill))skills.Remove(skill.Definition.Id);}}

        public static bool TryGet(Skills.SkillType type,out MeadworksSkill skill){
            lock(skills){if(type>=0&&skills.TryGetValue(SkillId.FromRaw((int)type),out skill!))return true;}
            skill=null!;return false;
        }

        /// <summary>Adds a product modifier to any skill, custom or the game's. Dispose the handle (or remove the owner) to stop it.</summary>
        public static SkillModifier AddModifier(string owner,Skills.SkillType skill,SkillModifierKind kind,float value)=>Modifiers.Add(owner,SkillId.FromRaw((int)skill),kind,value);

        /// <summary>Effective local level plus product bonuses; custom skills and registered vanilla skills of peers come from their owner's ZDO.</summary>
        public static float EffectiveLevel(Player? player,Skills.SkillType type){
            if(player==null)return 0f;
            if(TryGet(type,out var custom))return custom.Of(player);
            if(player!=Player.m_localPlayer&&TryPublishedGameLevel(player,type,out var published))return published;
            return player==Player.m_localPlayer?Modifiers.EffectiveLevel(SkillId.FromRaw((int)type),player.GetSkillLevel(type)):player.GetSkillLevel(type);
        }

        public static SkillEditDecision EditDecision(string productId)=>Plugin.IsReady?SkillEditPolicy.Decide(MeadworksAuthority.Facts(productId)):SkillEditPolicy.Decide(new AuthorityFacts(SessionRole.Menu,false,false,ServerCrucible.Unknown,PeerClass.RequiredOnAllPeers));

        internal static SkillProgress? Progress(Player? player,Skills.SkillType type){
            var data=player==null?null:SkillData(player.GetSkills());
            return data!=null&&data.TryGetValue(type,out var skill)?new SkillProgress(skill.m_level,skill.m_accumulator):(SkillProgress?)null;
        }

        internal static bool Edit(MeadworksSkill skill,Func<SkillProgress,SkillProgress> change,out string message){
            var decision=EditDecision(skill.Definition.Owner);
            var player=Player.m_localPlayer;
            if(!decision.Allowed||player==null){message=decision.Allowed?"No local character has skills to edit.":decision.Message;return false;}
            var target=GetSkill(player.GetSkills(),skill.Type);
            if(target==null){message="The skill "+skill.Definition.Name+" is not known to this character.";return false;}
            var next=change(new SkillProgress(target.m_level,target.m_accumulator));
            target.m_level=next.Level;target.m_accumulator=next.Accumulator;
            message="";return true;
        }

        static Skills.Skill? GetSkill(Skills skills,Skills.SkillType type)=>GetSkillMethod?.Invoke(skills,new object[]{type}) as Skills.Skill;

        static void AddTo(Skills? target){
            if(target==null)return;
            foreach(var skill in All)
                if(!target.m_skills.Exists(def=>def!=null&&def.m_skill==skill.Type))target.m_skills.Add(skill.GameDefinition);
        }

        internal static void Install(ConfigFile config,ManualLogSource logger){
            Log=MeadworksLog.For(logger,"Crucible");
            experienceSetting=MeadworksConfig.Bind(config,"Skills","ExperienceMultiplier",1f,"Multiplies experience in every MeadWorks custom skill (products' own skills, not the game's). The server's value applies in multiplayer. Crucible owns this server setting; its value applies to joining clients.",ConfigScope.ServerAuthoritative,new AcceptableValueRange<float>(0f,10f));
            harmony=new Harmony(Plugin.Guid+".skills");
            Patch(typeof(Skills),"Awake",postfix:nameof(AfterSkillsAwake));
            Patch(typeof(Skills),"IsSkillValid",postfix:nameof(AfterIsSkillValid));
            Patch(typeof(Skills),"Load",prefix:nameof(BeforeSkillsLoad));
            Patch(typeof(Player),"Load",postfix:nameof(AfterPlayerLoad));
            Patch(typeof(Skills),"RaiseSkill",prefix:nameof(BeforeRaiseSkill));
            Patch(typeof(Skills),"LowerAllSkills",prefix:nameof(BeforeLowerAll),postfix:nameof(AfterLowerAll));
            Patch(typeof(Skills),"CheatRaiseSkill",prefix:nameof(BeforeCheatRaise),postfix:nameof(AfterCheatRaise));
            Patch(typeof(Skills),"CheatResetSkill",prefix:nameof(BeforeCheatReset),postfix:nameof(AfterCheatReset));
            Patch(typeof(ZNetScene),"Awake",postfix:nameof(AfterZNetSceneAwake));
            var localization=MeadworksTranslations.LocalizationType.Get();
            if(localization!=null)Patch(localization,"SetupLanguage",postfix:nameof(AfterSetupLanguage));
            else Log.Warning("Localization type not found; custom skill names stay untranslated.");
        }

        static void Patch(Type type,string method,string? prefix=null,string? postfix=null){
            var target=AccessTools.Method(type,method);
            if(target==null){Log?.Warning("Skill registry: "+type.Name+"."+method+" not found; that part is off.");return;}
            harmony!.Patch(target,prefix:prefix==null?null:new HarmonyMethod(typeof(MeadworksSkills),prefix),postfix:postfix==null?null:new HarmonyMethod(typeof(MeadworksSkills),postfix));
        }

        internal static void Uninstall(){harmony?.UnpatchSelf();harmony=null;publisher.Reset();ResetExtensions();}

        internal static void Update(float deltaTime){
            textTimer+=deltaTime;
            if(textTimer>=2f){textTimer=0f;ApplyTexts();}
            var player=Player.m_localPlayer;
            if(player==null){publisher.Reset();return;}
            var view=player.GetComponent<ZNetView>();
            if(view==null||!view.IsValid()||!view.IsOwner())return;
            if(!HasPublishedLevels())return;
            // Levels are read only on the frames the publisher writes them (once per interval), not every frame.
            if(!publisher.Due(player.GetPlayerID(),deltaTime))return;
            var current=All;
            var gameLevels=PublishedGameLevels(player);
            var writes=publisher.Publish(current.Select(s=>new KeyValuePair<string,float>(s.Definition.PublishKey,s.Of(player))).Concat(gameLevels));
            if(writes.Count==0)return;
            var zdo=view.GetZDO();
            foreach(var write in writes)zdo.Set(write.Key,write.Value);
        }

        // Texts: re-applied when a language is set up and when a product's catalog changes (C05 overrides).
        static void ApplyTexts(){
            long revision=All.Sum(s=>s.Texts?.Revision??0L)+Registry.Revision;
            if(revision==appliedTexts)return;
            var type=MeadworksTranslations.LocalizationType.Get();
            var instance=type==null?null:Traverse.Create(type).Field("m_instance").GetValue();
            if(instance==null)return;
            AddNames(instance);appliedTexts=revision;
        }

        static void AddNames(object localization,string? setupLanguage=null){
            // Read the language from the given instance: Localization.instance would re-enter its own
            // constructor while SetupLanguage is running and loop until the game quits.
            // SetupLanguage also runs inside the native constructor before PlatformPrefs is ready.
            // Its actual language argument is authoritative there; polling uses the completed instance.
            var selected=setupLanguage??AccessTools.Method(localization.GetType(),"GetSelectedLanguage")?.Invoke(localization,null)?.ToString();
            string language=MeadworksLanguages.Normalize(selected);
            var addWord=AccessTools.Method(localization.GetType(),"AddWord");
            foreach(var skill in All){
                skill.GameDefinition.m_description=SkillTexts.Description(skill.Definition,skill.Texts,language);
                addWord?.Invoke(localization,new object[]{SkillTexts.GameWord(skill.Definition.Id),SkillTexts.Name(skill.Definition,skill.Texts,language)});
            }
            Traverse.Create(localization).Field("m_cache").Method("EvictAll").GetValue();
        }

        static void AfterSetupLanguage(object __instance,string __0){try{AddNames(__instance,__0);}catch(Exception error){Log?.Warning("Skill names not added: "+error.GetType().Name);}}

        static void AfterSkillsAwake(Skills __instance){AddTo(__instance);ApplyRegisteredIcons(__instance);}

        static void AfterIsSkillValid(Skills.SkillType __0,ref bool __result){if(!__result&&__0>=0)__result=Registry.IsRegistered(SkillId.FromRaw((int)__0));}

        static void AfterZNetSceneAwake(){foreach(var skill in All)skill.FindIcon();ApplyRegisteredIcons(Player.m_localPlayer?.GetSkills());}

        static void BeforeSkillsLoad(Skills __instance,ZPackage __0){
            if(!All.Any(s=>s.Definition.ImportFrom.Count>0))return;
            int position=__0.GetPos();
            try{
                var block=new SavedBlock{Records=SkillSaveData.Read(__0.GetArray(),position)};
                pendingImports.Remove(__instance);pendingImports.Add(__instance,block);
            }catch(Exception error){Log?.Warning("Skill import skipped, unreadable skills block: "+error.GetType().Name);}
            finally{__0.SetPos(position);}
        }

        static void AfterPlayerLoad(Player __instance){
            var target=__instance.GetSkills();
            if(target==null||!pendingImports.TryGetValue(target,out var block))return;
            pendingImports.Remove(target);
            try{
                var plan=SkillImport.Plan(block.Records,All.Select(s=>s.Definition),__instance.m_customData.ContainsKey);
                foreach(var action in plan){
                    if(action.Applied){
                        var skill=GetSkill(target,(Skills.SkillType)action.Target.Id.Value);
                        if(skill==null)continue;
                        skill.m_level=action.Imported.Level;skill.m_accumulator=action.Imported.Accumulator;
                    }
                    __instance.m_customData[action.Marker]=action.SourceIdentity;
                    Log?.Info("Skill import "+action);
                }
            }catch(Exception error){Log?.Warning("Skill import failed: "+error.GetType().Name);}
        }

        static void BeforeRaiseSkill(Skills __instance,Skills.SkillType __0,ref float __1){
            if(__0<=0)return;
            __1=ApplyRegisteredExperience(__instance,__0,__1);
            var id=SkillId.FromRaw((int)__0);
            bool custom=Registry.IsRegistered(id);
            float setting=custom&&experienceSetting!=null?MeadworksConfig.Effective(experienceSetting,Plugin.Guid,ConfigScope.ServerAuthoritative,1f):1f;
            float multiplier=Modifiers.ExperienceMultiplier(id);
            if(multiplier!=1f||setting!=1f)__1=SkillMath.ExperienceFactor(__1,multiplier,setting);
        }

        static void BeforeLowerAll(Skills __instance,out Dictionary<Skills.SkillType,float>? __state){
            __state=null;
            var data=SkillData(__instance);
            if(data==null)return;
            foreach(var pair in data){
                if(pair.Key<0||Modifiers.DeathLossMultiplier(SkillId.FromRaw((int)pair.Key))==1f)continue;
                (__state??=new Dictionary<Skills.SkillType,float>())[pair.Key]=pair.Value.m_level;
            }
        }

        static void AfterLowerAll(Skills __instance,Dictionary<Skills.SkillType,float>? __state){
            if(__state==null)return;
            var data=SkillData(__instance);
            foreach(var saved in __state){
                if(!data.TryGetValue(saved.Key,out var skill))continue;
                float lost=saved.Value-skill.m_level;
                skill.m_level=SkillMath.SetLevel(saved.Value-lost*Modifiers.DeathLossMultiplier(SkillId.FromRaw((int)saved.Key))).Level;
            }
        }

        static bool Allowed(MeadworksSkill skill){
            var decision=EditDecision(skill.Definition.Owner);
            if(!decision.Allowed)global::Console.instance?.Print("Skill "+skill.Definition.Name+": "+decision.Message);
            return decision.Allowed;
        }

        static void CheatRaise(Skills skills,MeadworksSkill custom,float value,bool showMessage){
            var skill=GetSkill(skills,custom.Type);
            if(skill==null)return;
            var next=SkillMath.CheatRaise(new SkillProgress(skill.m_level,skill.m_accumulator),value);
            skill.m_level=next.Level;
            if(skills.m_useSkillCap)RebalanceMethod?.Invoke(skills,new object[]{custom.Type});
            if(!showMessage)return;
            // The game's own level-up line; MessageHud localizes both words.
            SkillsPlayer(skills)?.Message(MessageHud.MessageType.TopLeft,"$msg_skillup $"+SkillTexts.GameWord(custom.Definition.Id)+": "+(int)skill.m_level,0,skill.m_info?.m_icon);
            global::Console.instance?.Print("Skill "+custom.Definition.Name+" = "+skill.m_level);
        }

        static bool BeforeCheatRaise(Skills __instance,string __0,float __1,bool __2){
            var definition=Registry.Find(__0);
            if(definition==null||!TryGet((Skills.SkillType)definition.Id.Value,out var custom))return true;
            if(Allowed(custom))CheatRaise(__instance,custom,__1,__2);
            return false;
        }

        static void AfterCheatRaise(Skills __instance,string __0,float __1){
            if(__0==null||__0.ToLowerInvariant()!="all")return;
            foreach(var custom in All)if(EditDecision(custom.Definition.Owner).Allowed)CheatRaise(__instance,custom,__1,false);
        }

        static bool BeforeCheatReset(Skills __instance,string __0){
            var definition=Registry.Find(__0);
            if(definition==null||!TryGet((Skills.SkillType)definition.Id.Value,out var custom))return true;
            if(Allowed(custom)){__instance.ResetSkill(custom.Type);global::Console.instance?.Print("Skill "+custom.Definition.Name+" reset");}
            return false;
        }

        static void AfterCheatReset(Skills __instance,string __0){
            if(__0==null||__0.ToLowerInvariant()!="all")return;
            foreach(var custom in All)if(EditDecision(custom.Definition.Owner).Allowed)__instance.ResetSkill(custom.Type);
        }
    }
}
