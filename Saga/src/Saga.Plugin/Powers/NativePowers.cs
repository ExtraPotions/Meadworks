// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Independently implemented from ProgressivePowers published behavior and user data. No unlicensed code inspected.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Powers;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using ExtraPotions.Meadworks.Crucible.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace ExtraPotions.Saga.Runtime {
    public static class NativePowers {
        static readonly Dictionary<string,string> bosses=new Dictionary<string,string>(StringComparer.Ordinal) {
            ["Eikthyr"]="Eikthyr",["TheElder"]="gd_king",["Bonemass"]="Bonemass",["Moder"]="Dragon",["Yagluth"]="GoblinKing",["Queen"]="SeekerQueen",["Fader"]="Fader",["Kall"]="FrozenKing_p3"
        };
        // Indexed by Boss. Kall's key is the one its final phase sets (FrozenKing sets defeated_frozenking after the first phase only).
        static readonly string[] worldKeys={"defeated_eikthyr","defeated_gdking","defeated_bonemass","defeated_dragon","defeated_goblinking","defeated_queen","defeated_fader","defeated_frozenking_p3"};
        static readonly Dictionary<Boss,SagaPowerEffect> templates=new Dictionary<Boss,SagaPowerEffect>();
        static Player? current;static float next;static readonly HashSet<ZDOID> credited=new HashSet<ZDOID>();
        static KitHudPanel? hud;static string hudLayout="";
        static UITooltip? nativeTooltip;static bool ownsNativeTooltip;static string oldTopic="",oldText="",appliedTopic="",appliedText="";
        static readonly List<HudRow> hudRows=new List<HudRow>();
        sealed class HudRow {public Boss Boss;public GameObject Root=null!;public TMP_Text Title=null!;public TMP_Text? Details;public Image Icon=null!;public bool IconOnly;}
        public static PowerRequirements Requirements=PowerRequirements.Defaults;
        public static PowerDefinitions Definitions=PowerDefinitions.Parse("",out _);
        public static bool RegisterBoss(string name,string prefab){if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(prefab)||name.IndexOfAny(new[]{',',':','\n','\r'})>=0||bosses.ContainsKey(name)||bosses.Values.Contains(prefab))return false;bosses.Add(name,prefab);NativeSaga.Data.Invalidate();return true;}
        public static IEnumerable<string> BossNames=>bosses.Keys;
        public static IReadOnlyDictionary<string,Delegate> Capability()=>new Dictionary<string,Delegate>{["RegisterBoss"]=new Func<string,string,bool>(RegisterBoss)};
        static string Read(Player player,string key)=>player.m_customData.TryGetValue(key,out var text)?text:"";
        static Dictionary<string,int> Kills(Player player){var result=new Dictionary<string,int>(StringComparer.Ordinal);foreach(var entry in Read(player,KillCounts.Key).Split(',')){var split=entry.Split(':');if(split.Length==2&&int.TryParse(split[1],NumberStyles.None,CultureInfo.InvariantCulture,out var count)&&count>=0)result[split[0]]=count;}return result;}
        static void SaveKills(Player player,Dictionary<string,int> counts)=>player.m_customData[KillCounts.Key]=string.Join(",",counts.OrderBy(p=>p.Key,StringComparer.Ordinal).Where(p=>p.Value>0).Select(p=>p.Key+":"+p.Value.ToString(CultureInfo.InvariantCulture)));
        public static string KillSummary(Player player)=>string.Join(", ",Kills(player).Select(p=>BossDisplay(p.Key)+" "+p.Value));
        public static string Summary(Player player)=>string.Join("\n",Attuned(player).Attuned.Select(b=>Tooltip(player,b)));
        public static void ApplyCommand(Player player,PowerCommand request){
            if(player!=Player.m_localPlayer||!NativeSaga.Active("powers"))return;
            var counts=Kills(player);var names=request.Boss=="all"?BossNames:new[]{request.Boss};
            foreach(var boss in names){if(!bosses.ContainsKey(boss))continue;if(request.Reset)counts.Remove(boss);else counts[boss]=(int)Math.Min(int.MaxValue,(long)(counts.TryGetValue(boss,out var n)?n:0)+request.Count);}
            SaveKills(player,counts);next=0;var target=request.Boss=="all"?SagaText.Text("power_target_all"):BossDisplay(request.Boss);player.Message(MessageHud.MessageType.Center,request.Reset?SagaText.Format("command_kills_reset",target):SagaText.Format("command_kills_granted",request.Count,target));
        }
        static Attunement Attuned(Player player)=>Attunement.ReadLimited(Read(player,Attunement.AttunedKey),Read(player,Attunement.AsideKey),Plugin.Settings!.Get(SagaSettings.Powers.MaxAttunedPowers));
        // The status effect hash per power; the HUD prefix needs it every frame and the name concatenation allocated.
        static readonly Dictionary<Boss,int> powerHashes=new Dictionary<Boss,int>();
        static int PowerHash(Boss boss){if(!powerHashes.TryGetValue(boss,out var hash))powerHashes[boss]=hash=("SagaPower_"+boss).GetStableHashCode();return hash;}
        static void Save(Player player,Attunement state){player.m_customData[Attunement.AttunedKey]=state.AttunedText;player.m_customData[Attunement.AsideKey]=state.AsideText;}
        static bool Power(string effect,out Boss boss){var name=(effect??"").Replace("GP_","");if(name=="TheQueen")name="Queen";if(name=="GoblinKing")name="Yagluth";if(name=="Dragon")name="Moder";if(name=="Ashlands")name="Fader";if(name=="DeepNorth")name="Kall";return PowerCatalog.TryParse(name,out boss);}
        public static int Mastery(Player player,Boss boss){var advanced=Plugin.Settings!.Get(SagaSettings.Powers.AdvancedPowerConfig)&&Definitions.Lists(boss);var max=advanced?Definitions.LevelCount(boss):7;var earned=Requirements.MasteryNamed(boss,Kills(player),max);int? forced=Plugin.Settings.Get(SagaSettings.Cheats.MasteryOverride)?Plugin.Settings.Get(SagaSettings.Cheats.MasteryOverrideLevel):(int?)null;return PowerEffects.EffectiveMastery(earned,forced,max);}
        static IReadOnlyList<EffectValue> Effects(Player player,Boss boss){var mastery=Mastery(player,boss);return Plugin.Settings!.Get(SagaSettings.Powers.AdvancedPowerConfig)&&Definitions.Lists(boss)?Definitions.Effects(boss,mastery):PowerEffects.FromFeatures(boss,mastery,f=>Plugin.Settings.GetBoxed(SagaSettings.PowerFeature(f)));}
        static StatusEffect? PowerSource(Boss boss)=>ObjectDB.instance?.m_StatusEffects.FirstOrDefault(effect=>effect!=null&&effect.name.StartsWith("GP_",StringComparison.Ordinal)&&Power(effect.name,out var mapped)&&mapped==boss);
        static string BossLabel(Boss boss){var source=PowerSource(boss);return source!=null?GameLocalization.instance.Localize(source.m_name):boss==Boss.Kall?GameLocalization.instance.Localize("$enemy_frozenking"):PowerCatalog.DisplayName(boss);}
        // Valheim 1.0.20 ships no GP_DeepNorth effect: Kall shows the game's own Frost status icon until it does.
        static Sprite? PowerIcon(Boss boss){var source=PowerSource(boss);if(source!=null||boss!=Boss.Kall)return source?.m_icon;var db=ObjectDB.instance;return db==null?null:(db.GetStatusEffect("Frost".GetStableHashCode())??db.GetStatusEffect("Freezing".GetStableHashCode()))?.m_icon;}
        static string MasteryText(Player player,Boss boss)=>Mastery(player,boss).ToString(CultureInfo.InvariantCulture)+"/"+(Plugin.Settings!.Get(SagaSettings.Powers.AdvancedPowerConfig)&&Definitions.Lists(boss)?Definitions.LevelCount(boss):7).ToString(CultureInfo.InvariantCulture);
        static string RechargeText(Player player)=>SagaText.Format("power_recharging",PowerRecharge.TimeLeft(player.m_guardianPowerCooldown));
        static string BossDisplay(string name)=>PowerCatalog.TryParse(name,out var boss)?BossLabel(boss):name;
        static string Needed(Player player,Boss boss,int level)=>string.Join(", ",Requirements.NeededNamed(boss,level,Kills(player)).Select(p=>BossDisplay(p.Key)+" "+p.Value));
        static string EffectText(EffectValue effect){
            if(effect.Effect==PowerEffect.HealthTick&&effect.Target=="Interval")return SagaText.Format("power_tick_interval",effect.Number);
            if(effect.Effect==PowerEffect.HealthTick&&effect.Target=="MinHealth")return SagaText.Format("power_min_health",effect.Number*100);
            var label=SagaText.Text("powerfx_"+effect.Effect.ToString().ToLowerInvariant());var target=effect.Target;
            if(target.Length>0&&SagaText.Catalog.Keys.Contains("power_target_"+target.ToLowerInvariant()))target=SagaText.Text("power_target_"+target.ToLowerInvariant());
            else if(Enum.TryParse(target,out global::Skills.SkillType skill))target=GameLocalization.instance.Localize("$skill_"+skill.ToString().ToLowerInvariant());
            if(target.Length>0)label+=" ("+target+")";if(effect.Flag)return label;
            if(effect.Effect==PowerEffect.Resistance)return label+": "+SagaText.Text("power_resistance_"+effect.Modifier.ToString().ToLowerInvariant());
            bool flat=effect.Effect==PowerEffect.Armor||effect.Effect==PowerEffect.CarryWeight||effect.Effect==PowerEffect.SkillLevel||effect.Effect==PowerEffect.HealthTick||effect.Effect==PowerEffect.BlockStaminaFlat||effect.Effect==PowerEffect.MaxFallSpeed;
            return label+" "+(effect.Number*(flat?1:100)).ToString("+0.#;-0.#;0",CultureInfo.InvariantCulture)+(flat?"":"%");
        }
        static string Tooltip(Player player,Boss boss){var mastery=Mastery(player,boss);var lines=new List<string>{BossLabel(boss)+" "+mastery};foreach(var effect in Effects(player,boss))lines.Add(EffectText(effect));if(Plugin.Settings!.Get(SagaSettings.Powers.ShowLockedFeatures)){bool advanced=Plugin.Settings.Get(SagaSettings.Powers.AdvancedPowerConfig)&&Definitions.Lists(boss);if(advanced){for(int level=mastery+1;level<=Definitions.LevelCount(boss);level++){var locked=Definitions.Effects(boss,level).Skip(Definitions.Effects(boss,level-1).Count);foreach(var effect in locked)lines.Add("<color=#888888>"+EffectText(effect)+" ("+Needed(player,boss,level)+")</color>");}}else foreach(var feature in PowerCatalog.For(boss).Where(f=>f.Index>mastery))foreach(var effect in feature.Effects(Plugin.Settings.GetBoxed(SagaSettings.PowerFeature(feature))))lines.Add("<color=#888888>"+EffectText(effect)+" ("+Needed(player,boss,feature.Index)+")</color>");}return string.Join("\n",lines);}
        // A character seeded before Kall existed keeps its seeded counts and gets only Kall from world progress, once.
        static void Seed(Player player) {
            bool all=Read(player,KillCounts.SeededKey)!="1",kall=Read(player,KillCounts.SeededKallKey)!="1";
            if(!all&&!kall||ZoneSystem.instance==null)return;
            var counts=Kills(player);foreach(Boss boss in Enum.GetValues(typeof(Boss)))if((boss==Boss.Kall?kall:all)&&ZoneSystem.instance.GetGlobalKey(worldKeys[(int)boss])&&(!counts.TryGetValue(PowerCatalog.Name(boss),out var n)||n<1))counts[PowerCatalog.Name(boss)]=1;
            SaveKills(player,counts);if(all&&Read(player,Attunement.AttunedKey).Length==0&&Power(player.m_guardianPower,out var selected))Save(player,Attunement.Empty.Use(selected,Plugin.Settings!.Get(SagaSettings.Powers.MaxAttunedPowers)));
            player.m_customData[KillCounts.SeededKey]="1";player.m_customData[KillCounts.SeededKallKey]="1";
        }
        public static void Tick(){BossKillCredit.Poll();PowerWheelRuntime.Poll();if(Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;var player=Player.m_localPlayer;
            if(current!=player){Remove(current);current=player;credited.Clear();DestroyHud();}
            if(player==null||!NativeSaga.Active("powers")||player.IsDead()){Remove(player);hud?.Update(false,false,1,1);RestoreNativeTooltip();return;}
            Seed(player);var state=Attuned(player);Save(player,state);
            foreach(Boss boss in Enum.GetValues(typeof(Boss))){var hash=("SagaPower_"+boss).GetStableHashCode();if(!state.Attuned.Contains(boss)){player.GetSEMan().RemoveStatusEffect(hash);continue;}
                if(!templates.TryGetValue(boss,out var template)){template=ScriptableObject.CreateInstance<SagaPowerEffect>();template.name="SagaPower_"+boss;template.Boss=boss;template.m_name=PowerCatalog.DisplayName(boss);template.m_ttl=0;template.m_startMessage="";template.m_stopMessage="";templates.Add(boss,template);}
                template.m_icon=PowerIcon(boss);
                template.m_hidden=!Plugin.Settings!.Get(SagaSettings.Powers.ShowPassiveInStatusBar);var effect=player.GetSEMan().GetStatusEffect(hash) as SagaPowerEffect??player.GetSEMan().AddStatusEffect(template) as SagaPowerEffect;
                if(effect!=null){effect.m_name=BossLabel(boss);effect.m_icon=template.m_icon;effect.m_hidden=template.m_hidden;effect.m_tooltip=Tooltip(player,boss);effect.Apply(Effects(player,boss));}
            }
            Draw(player,state);
        }
        static void Remove(Player? player){if(player==null)return;foreach(Boss boss in Enum.GetValues(typeof(Boss)))player.GetSEMan().RemoveStatusEffect(("SagaPower_"+boss).GetStableHashCode());}
        static void Place(RectTransform rect,Vector2 at,Vector2 size){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(at.x,-at.y);rect.sizeDelta=size;}
        static void DestroyHud(){hud?.Dispose();hud=null;hudRows.Clear();hudLayout="";}
        static void RestoreNativeTooltip(){if(nativeTooltip!=null){if(ownsNativeTooltip)UnityEngine.Object.Destroy(nativeTooltip);else if(nativeTooltip.m_topic==appliedTopic&&nativeTooltip.m_text==appliedText)nativeTooltip.Set(oldTopic,oldText);}nativeTooltip=null;ownsNativeTooltip=false;oldTopic=oldText=appliedTopic=appliedText="";}
        static void NativeTooltip(Hud gameHud,Player player,Boss boss){if(gameHud.m_gpIcon==null)return;var control=gameHud.m_gpIcon.gameObject;if(nativeTooltip==null||nativeTooltip.gameObject!=control){RestoreNativeTooltip();nativeTooltip=control.GetComponent<UITooltip>();ownsNativeTooltip=nativeTooltip==null;if(nativeTooltip==null)nativeTooltip=KitTooltip.Attach(control);if(nativeTooltip==null)return;oldTopic=nativeTooltip.m_topic;oldText=nativeTooltip.m_text;}appliedTopic=BossLabel(boss);appliedText=Tooltip(player,boss)+(player.m_guardianPowerCooldown>0?"\n"+RechargeText(player):"");nativeTooltip.Set(appliedTopic,appliedText);}
        [HarmonyPatch(typeof(Player),nameof(Player.GetGuardianPowerHUD))]static class GuardianHud {
            static bool Prefix(Player __instance,ref StatusEffect se,ref float cooldown){if(__instance!=Player.m_localPlayer||!NativeSaga.Active("powers"))return true;var state=Attuned(__instance);cooldown=__instance.m_guardianPowerCooldown;se=state.Attuned.Count==0?null!:__instance.GetSEMan().GetStatusEffect(PowerHash(state.Attuned.Last()))??PowerSource(state.Attuned.Last())!;return false;}
        }
        [HarmonyPatch(typeof(Hud),nameof(Hud.UpdateGuardianPower))]static class GuardianLabel {
            static void Postfix(Hud __instance,Player player){if(!Plugin.AllowsUi||player==null||player!=Player.m_localPlayer||!NativeSaga.Active("powers")||player.IsDead()){RestoreNativeTooltip();return;}var state=Attuned(player);if(state.Attuned.Count==0){RestoreNativeTooltip();return;}var newest=state.Attuned.Last();if(__instance.m_gpName!=null)__instance.m_gpName.text=BossLabel(newest);if(__instance.m_gpCooldown!=null)__instance.m_gpCooldown.text=player.m_guardianPowerCooldown>0?PowerRecharge.TimeLeft(player.m_guardianPowerCooldown):MasteryText(player,newest);NativeTooltip(__instance,player,newest);}
        }
        static void Draw(Player player,Attunement state){
            if(!Plugin.AllowsUi||Hud.instance==null)return;var settings=Plugin.Settings!;var mode=settings.Get(SagaSettings.Powers.DisplayMode);var spacing=settings.Get(SagaSettings.Powers.DisplaySpacing);var full=settings.Get(SagaSettings.Powers.FullPowerDisplays);var shown=state.Attuned.Reverse().Skip(1).Take(mode==PowerDisplayMode.NewestOnly?0:7).ToArray();if(shown.Length==0){DestroyHud();return;}
            var layout=mode+":"+full+":"+spacing+":"+string.Join(",",shown);
            if(hud?.Root==null||hudLayout!=layout){DestroyHud();hudLayout=layout;int fullCount=mode==PowerDisplayMode.Stacked?shown.Length:mode==PowerDisplayMode.Mixed?Math.Min(Math.Max(0,full-1),shown.Length):0;int compactCount=mode==PowerDisplayMode.Compact?shown.Length:0;int iconCount=shown.Length-fullCount-compactCount;float height=16+fullCount*(64+spacing)+compactCount*(28+spacing)+(iconCount>0?36:0);float width=fullCount>0||compactCount>0?245:Math.Max(44,16+iconCount*(32+spacing));
                hud=KitHudPanel.Create(Hud.instance.transform,"Saga_power_hud",new Vector2(width,height),new Vector2(12,205), global::ExtraPotions.Saga.Runtime.Plugin.Guid, "Saga_power_hud");if(hud==null)return;float y=8;int iconIndex=0;
                for(int n=0;n<shown.Length;n++){bool fullRow=n<fullCount,compact=mode==PowerDisplayMode.Compact,iconOnly=!fullRow&&!compact;float rowHeight=fullRow?64:iconOnly?32:28;float x=iconOnly?8+iconIndex++*(32+spacing):8;var root=new GameObject("Saga_power_"+shown[n],typeof(RectTransform));root.transform.SetParent(hud.Content,false);Place((RectTransform)root.transform,new Vector2(x,y),new Vector2(iconOnly?32:229,rowHeight));
                    var iconObject=new GameObject("icon",typeof(RectTransform),typeof(Image));iconObject.transform.SetParent(root.transform,false);var image=iconObject.GetComponent<Image>();image.preserveAspect=true;Place((RectTransform)iconObject.transform,Vector2.zero,new Vector2(fullRow?48:24,fullRow?48:24));hud.SetInteractive(image,true);
                    var title=KitCanvas.Text(root.transform,"name","",iconOnly?9:fullRow?16:13,Color.white,TextAlignmentOptions.Left,false);Place(title.rectTransform,new Vector2(iconOnly?0:fullRow?56:32,iconOnly?16:2),new Vector2(iconOnly?32:173,24));KitCanvas.Fit(title);TMP_Text? details=null;if(fullRow){details=KitCanvas.Text(root.transform,"effects","",11,Color.white,TextAlignmentOptions.Left,true);Place(details.rectTransform,new Vector2(56,26),new Vector2(173,34));}
                    hudRows.Add(new HudRow{Boss=shown[n],Root=root,Icon=image,Title=title,Details=details,IconOnly=iconOnly});if(!iconOnly)y+=rowHeight+spacing;
                }
            }
            if(hud.Root.parent is RectTransform parent&&Hud.instance.m_gpRoot?.transform is RectTransform nativeRoot){var corners=new Vector3[4];nativeRoot.GetWorldCorners(corners);var at=parent.InverseTransformPoint(corners[1]);hud.SetPosition(new Vector2(at.x-parent.rect.xMin,parent.rect.yMax-at.y-hud.Root.rect.height-spacing));}
            hud.Update(shown.Length>0,Cursor.visible,1,.85f);foreach(var row in hudRows){bool recharging=player.m_guardianPowerCooldown>0;var mastery=MasteryText(player,row.Boss);var shownValue=recharging?PowerRecharge.TimeLeft(player.m_guardianPowerCooldown):mastery;row.Title.text=row.IconOnly?shownValue:BossLabel(row.Boss)+"  "+shownValue;row.Icon.color=recharging?new Color(.45f,.45f,.45f,1):Color.white;row.Icon.sprite=templates.TryGetValue(row.Boss,out var template)?template.m_icon:null;if(row.Details!=null)row.Details.text=string.Join(", ",Effects(player,row.Boss).Select(EffectText));KitTooltip.Set(row.Root,BossLabel(row.Boss),Tooltip(player,row.Boss)+(recharging?"\n"+RechargeText(player):""));}
        }
        internal static bool KnownBossPrefab(string prefab,out string boss){boss="";if(string.IsNullOrEmpty(prefab))return false;foreach(var pair in bosses)if(pair.Value==prefab){boss=pair.Key;return true;}return false;}
        internal static bool KnownBossHash(int hash,out string boss){boss="";foreach(var pair in bosses)if(pair.Value.GetStableHashCode()==hash){boss=pair.Key;return true;}return false;}
        internal static bool IsKnownBoss(string boss)=>!string.IsNullOrEmpty(boss)&&bosses.ContainsKey(boss);
        internal static bool AlreadyCredited(ZDOID id)=>credited.Contains(id);
        internal static void ApplyKill(string boss,ZDOID id){var player=Player.m_localPlayer;if(!NativeSaga.Active("powers")||player==null||!bosses.ContainsKey(boss)||!credited.Add(id))return;var counts=Kills(player);counts[boss]=(int)Math.Min(int.MaxValue,(long)(counts.TryGetValue(boss,out var old)?old:0)+1);SaveKills(player,counts);next=0;}
        [HarmonyPatch(typeof(Character),nameof(Character.OnDeath))]static class Died {static void Prefix(Character __instance)=>BossKillCredit.Report(__instance);}
        [HarmonyPatch(typeof(ItemStand),nameof(ItemStand.Interact))]static class Stone {
            static bool Prefix(ItemStand __instance,Humanoid user,bool hold,ref bool __result){if(!NativeSaga.Active("powers")||hold||!(user is Player player)||player!=Player.m_localPlayer||!__instance.HaveAttachment()||!Power(__instance.m_guardianPower?.name??"",out var boss))return true;var preview=Attuned(player).Preview(boss,Plugin.Settings!.Get(SagaSettings.Powers.MaxAttunedPowers));Save(player,Attuned(player).Use(boss,Plugin.Settings.Get(SagaSettings.Powers.MaxAttunedPowers)));player.SetGuardianPower(__instance.m_guardianPower.name);player.Message(MessageHud.MessageType.Center,preview.Kind==AttuneKind.Release?SagaText.Format("power_release",BossLabel(boss)):preview.Kind==AttuneKind.Replace?SagaText.Format("power_replace",BossLabel(preview.Replaced!.Value),BossLabel(boss)):SagaText.Format("power_attune",BossLabel(boss)));next=0;__result=true;return false;}
        }
        [HarmonyPatch(typeof(ItemStand),nameof(ItemStand.GetHoverText))]static class Hover {static void Postfix(ItemStand __instance,ref string __result){var player=Player.m_localPlayer;if(player==null||!NativeSaga.Active("powers")||!__instance.HaveAttachment()||!Power(__instance.m_guardianPower?.name??"",out var boss))return;var preview=Attuned(player).Preview(boss,Plugin.Settings!.Get(SagaSettings.Powers.MaxAttunedPowers));__result+="\n"+(preview.Kind==AttuneKind.Release?SagaText.Format("power_release",BossLabel(boss)):preview.Kind==AttuneKind.Replace?SagaText.Format("power_replace",BossLabel(preview.Replaced!.Value),BossLabel(boss)):SagaText.Format("power_attune",BossLabel(boss)))+"\n"+Tooltip(player,boss);}}
        // Kall's summoning altar (offeraltar_FrozenKing_bossroom, an OfferingBowl) stands in for his missing boss stone: the
        // alternate use (AltPlace + Use) attunes or releases Kall under the stone rules, once his first level is earned.
        // The plain use and the item offering stay the game's summon. A future GP_DeepNorth stone works through Stone above.
        internal static bool IsKallAltar(OfferingBowl bowl)=>bowl!=null&&(bowl.m_bossPrefab!=null&&Utils.GetPrefabName(bowl.m_bossPrefab)=="FrozenKing"||Utils.GetPrefabName(bowl.gameObject).StartsWith("offeraltar_FrozenKing",StringComparison.Ordinal));
        static bool KallUnlocked(Player player)=>Mastery(player,Boss.Kall)>=1||Attuned(player).Attuned.Contains(Boss.Kall);
        const string AltUse="[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] ";
        public static bool AttuneAtAltar(Player player){if(player!=Player.m_localPlayer||!NativeSaga.Active("powers"))return false;var limit=Plugin.Settings!.Get(SagaSettings.Powers.MaxAttunedPowers);
            if(!KallUnlocked(player)){player.Message(MessageHud.MessageType.Center,SagaText.Format("power_altar_locked",BossLabel(Boss.Kall)));return true;}
            var preview=Attuned(player).Preview(Boss.Kall,limit);Save(player,Attuned(player).Use(Boss.Kall,limit));
            player.Message(MessageHud.MessageType.Center,preview.Kind==AttuneKind.Release?SagaText.Format("power_release",BossLabel(Boss.Kall)):preview.Kind==AttuneKind.Replace?SagaText.Format("power_replace",BossLabel(preview.Replaced!.Value),BossLabel(Boss.Kall)):SagaText.Format("power_attune",BossLabel(Boss.Kall)));next=0;return true;}
        [HarmonyPatch(typeof(OfferingBowl),nameof(OfferingBowl.Interact))]static class KallAltar {
            static bool Prefix(OfferingBowl __instance,Humanoid user,bool hold,bool alt,ref bool __result){if(!alt||hold||!(user is Player player)||player!=Player.m_localPlayer||!NativeSaga.Active("powers")||!IsKallAltar(__instance))return true;__result=AttuneAtAltar(player);return false;}
        }
        [HarmonyPatch(typeof(OfferingBowl),nameof(OfferingBowl.GetHoverText))]static class KallAltarHover {
            static void Postfix(OfferingBowl __instance,ref string __result){var player=Player.m_localPlayer;if(player==null||!NativeSaga.Active("powers")||!IsKallAltar(__instance))return;
                if(!KallUnlocked(player)){__result+="\n"+SagaText.Format("power_altar_locked",BossLabel(Boss.Kall));return;}
                var preview=Attuned(player).Preview(Boss.Kall,Plugin.Settings!.Get(SagaSettings.Powers.MaxAttunedPowers));
                __result+="\n"+GameLocalization.instance.Localize(AltUse)+(preview.Kind==AttuneKind.Release?SagaText.Format("power_release",BossLabel(Boss.Kall)):preview.Kind==AttuneKind.Replace?SagaText.Format("power_replace",BossLabel(preview.Replaced!.Value),BossLabel(Boss.Kall)):SagaText.Format("power_attune",BossLabel(Boss.Kall)))+"\n"+Tooltip(player,Boss.Kall);}
        }
        static bool OwnPowers(Player player)=>player==Player.m_localPlayer&&player.m_nview!=null&&player.m_nview.IsValid()&&player.m_nview.IsOwner()&&NativeSaga.Active("powers");
        static StatusEffect[] ActivationSources(Player player){
            var sources=Attuned(player).Attuned.Reverse().Select(PowerSource).Where(source=>source!=null).Cast<StatusEffect>().ToArray();
            var names=PowerWheelRules.ActivationOrder(Plugin.Settings!.Get(SagaSettings.Powers.ActivateAllAttuned),player.m_guardianPower,sources.Select(source=>source.name).ToArray());
            if(names.Count==sources.Length)return sources;
            return names.Select(name=>sources.First(source=>string.Equals(source.name,name,StringComparison.OrdinalIgnoreCase))).ToArray();
        }
        internal static Boss[] WheelBosses(Player player)=>Attuned(player).Attuned.ToArray();
        internal static string WheelLabel(Boss boss)=>BossLabel(boss);
        internal static Sprite WheelIcon(Boss boss)=>PowerIcon(boss)!;
        internal static bool WheelCanActivate(Boss boss)=>PowerSource(boss)!=null;
        internal static bool TrySelectWheel(Player player,Boss boss){
            if(player!=Player.m_localPlayer||!NativeSaga.Active("powers")||Plugin.Settings!.Get(SagaSettings.Powers.ActivateAllAttuned)||!Attuned(player).Attuned.Contains(boss))return false;
            var source=PowerSource(boss);
            if(source==null){player.Message(MessageHud.MessageType.Center,SagaText.Format("power_wheel_unavailable",BossLabel(boss)));return false;}
            player.SetGuardianPower(source.name);
            player.Message(MessageHud.MessageType.Center,SagaText.Format("power_wheel_selected",BossLabel(boss)));
            return true;
        }
        [HarmonyPatch(typeof(Player),nameof(Player.StartGuardianPower))]static class BlockStart {
            static bool Prefix(Player __instance,ref bool __result){if(!OwnPowers(__instance))return true;if(__instance.IsDead()||Plugin.Settings!.Get(SagaSettings.Powers.BlockPowerActivation)){__result=false;return false;}var sources=ActivationSources(__instance);if(sources.Length==0){__result=false;return false;}__instance.SetGuardianPower(sources[0].name);return true;}
        }
        static bool activatingExtras;
        [HarmonyPatch(typeof(Player),nameof(Player.ActivateGuardianPower))]static class BlockActivate {
            static bool Prefix(Player __instance,ref bool __result,out StatusEffect[]? __state){__state=null;if(activatingExtras||!OwnPowers(__instance))return true;if(__instance.IsDead()||Plugin.Settings!.Get(SagaSettings.Powers.BlockPowerActivation)){__result=false;return false;}var sources=ActivationSources(__instance);if(sources.Length==0){__result=false;return false;}if(__instance.m_guardianPowerCooldown<=0){__state=sources;__instance.SetGuardianPower(sources[0].name);}return true;}
            [HarmonyPriority(Priority.Last)]
            static void Postfix(Player __instance,StatusEffect[]? __state){if(__state==null||!__instance.GetSEMan().HaveStatusEffect(__state[0].NameHash()))return;float cooldown=__instance.m_guardianPowerCooldown;var selected=__instance.m_guardianPower;float adrenaline=__instance.m_adrenalineGuardianPower;
                try{activatingExtras=true;__instance.m_adrenalineGuardianPower=0;foreach(var source in __state.Skip(1)){__instance.SetGuardianPower(source.name);__instance.m_guardianPowerCooldown=0;__instance.ActivateGuardianPower();cooldown=PowerRecharge.Shared(cooldown,__instance.m_guardianPowerCooldown);}}
                catch(Exception error){NativeSaga.Report(error,"attuned power activation");}
                finally{cooldown=PowerRecharge.Shared(cooldown,__instance.m_guardianPowerCooldown);activatingExtras=false;__instance.m_adrenalineGuardianPower=adrenaline;try{__instance.SetGuardianPower(selected);}finally{__instance.m_guardianPowerCooldown=cooldown;}}
            }
        }
        public static void Shutdown(){BossKillCredit.Clear();PowerWheelRuntime.Shutdown();Remove(current);current=null;foreach(var template in templates.Values)UnityEngine.Object.Destroy(template);templates.Clear();credited.Clear();DestroyHud();RestoreNativeTooltip();if(Hud.instance!=null&&Player.m_localPlayer!=null)Hud.instance.UpdateGuardianPower(Player.m_localPlayer);next=0;}
    }
    public sealed class SagaPowerEffect:SE_Stats {
        public Boss Boss;
        IReadOnlyList<EffectValue> values=Array.Empty<EffectValue>();
        public void Apply(IReadOnlyList<EffectValue> effects){values=effects;
            m_runStaminaDrainModifier=Sum(PowerEffect.RunStaminaCost);m_jumpStaminaUseModifier=Sum(PowerEffect.JumpStaminaCost);m_swimStaminaUseModifier=Sum(PowerEffect.SwimStaminaCost);m_dodgeStaminaUseModifier=Sum(PowerEffect.DodgeStaminaCost);m_attackStaminaUseModifier=Sum(PowerEffect.AttackStaminaCost);m_blockStaminaUseModifier=Sum(PowerEffect.BlockStaminaCost);m_blockStaminaUseFlatValue=Sum(PowerEffect.BlockStaminaFlat);m_sneakStaminaUseModifier=Sum(PowerEffect.SneakStaminaCost);m_homeItemStaminaUseModifier=Sum(PowerEffect.ToolStaminaCost);
            m_speedModifier=Sum(PowerEffect.MovementSpeed);m_swimSpeedModifier=Sum(PowerEffect.SwimSpeed);m_jumpModifier=new Vector3(Sum(PowerEffect.JumpDistance),Sum(PowerEffect.JumpHeight),Sum(PowerEffect.JumpDistance));m_maxMaxFallSpeed=Sum(PowerEffect.MaxFallSpeed);m_windMovementModifier=Sum(PowerEffect.WindMovement);m_windRunStaminaModifier=Sum(PowerEffect.WindRunStaminaCost);m_fallDamageModifier=Sum(PowerEffect.FallDamage);
            m_healthRegenMultiplier=Mathf.Max(0,1+Sum(PowerEffect.HealthRegen));m_staminaRegenMultiplier=Mathf.Max(0,1+Sum(PowerEffect.StaminaRegen));m_eitrRegenMultiplier=Mathf.Max(0,1+Sum(PowerEffect.EitrRegen));m_addArmor=Sum(PowerEffect.Armor);m_armorMultiplier=Mathf.Max(0,1+Sum(PowerEffect.ArmorMultiplier));m_addMaxCarryWeight=Sum(PowerEffect.CarryWeight);m_noiseModifier=Sum(PowerEffect.Noise);m_stealthModifier=-Sum(PowerEffect.Stealth);m_adrenalineModifier=Sum(PowerEffect.AdrenalineGain);m_staggerModifier=Sum(PowerEffect.StaggerTaken);m_timedBlockBonus=Sum(PowerEffect.TimedBlockBonus);
            m_attributes=StatusEffect.StatusAttribute.None;foreach(var value in effects)if(value.Flag&&Enum.TryParse(value.Effect.ToString(),out StatusEffect.StatusAttribute flag))m_attributes|=flag;
            m_healthPerTick=Sum(PowerEffect.HealthTick);m_tickInterval=values.Where(e=>e.Effect==PowerEffect.HealthTick&&e.Target=="Interval").Select(e=>e.Number).DefaultIfEmpty(10).Last();m_healthPerTickMinHealthPercentage=values.Where(e=>e.Effect==PowerEffect.HealthTick&&e.Target=="MinHealth").Select(e=>e.Number).DefaultIfEmpty(0).Last();
            m_mods.Clear();foreach(var value in effects.Where(e=>e.Effect==PowerEffect.Resistance))foreach(var type in DamageTypes(value.Target))if(Enum.TryParse(value.Modifier.ToString(),out HitData.DamageModifier modifier))m_mods.Add(new HitData.DamageModPair{m_type=type,m_modifier=modifier});
        }
        float Sum(PowerEffect effect)=>values.Where(e=>e.Effect==effect&&e.Target.Length==0).Sum(e=>e.Number);
        float Skill(PowerEffect effect,global::Skills.SkillType skill)=>values.Where(e=>e.Effect==effect&&(e.Target=="All"||e.Target==skill.ToString())).Sum(e=>e.Number);
        public override void ModifySkillLevel(global::Skills.SkillType skill,ref float level)=>level+=Skill(PowerEffect.SkillLevel,skill);
        public override void ModifyRaiseSkill(global::Skills.SkillType skill,ref float value)=>value*=Mathf.Max(0,1+Skill(PowerEffect.SkillGain,skill));
        static IEnumerable<HitData.DamageType> DamageTypes(string target){if(target=="All")return new[]{HitData.DamageType.Blunt,HitData.DamageType.Slash,HitData.DamageType.Pierce,HitData.DamageType.Chop,HitData.DamageType.Pickaxe,HitData.DamageType.Fire,HitData.DamageType.Frost,HitData.DamageType.Lightning,HitData.DamageType.Poison,HitData.DamageType.Spirit};if(target=="Melee")return new[]{HitData.DamageType.Blunt,HitData.DamageType.Slash,HitData.DamageType.Pierce};if(target=="Elemental")return new[]{HitData.DamageType.Fire,HitData.DamageType.Frost,HitData.DamageType.Lightning,HitData.DamageType.Poison,HitData.DamageType.Spirit};return Enum.TryParse(target,out HitData.DamageType type)?new[]{type}:Array.Empty<HitData.DamageType>();}
        public override void ModifyAttack(global::Skills.SkillType skill,ref HitData hitData){var damage=values.Where(e=>e.Effect==PowerEffect.DamageDealt).ToArray();foreach(var type in DamageTypes("All").Concat(new[]{HitData.DamageType.Damage})){var more=damage.Where(e=>DamageTypes(e.Target).Contains(type)||type==HitData.DamageType.Damage&&(e.Target=="Pure"||e.Target=="All")).Sum(e=>e.Number);Scale(ref hitData.m_damage,type,Mathf.Max(0,1+more));}hitData.m_damage.Modify(Mathf.Max(0,1+Skill(PowerEffect.AttackDamage,skill)));}
        static void Scale(ref HitData.DamageTypes damage,HitData.DamageType type,float factor){switch(type){case HitData.DamageType.Damage:damage.m_damage*=factor;break;case HitData.DamageType.Blunt:damage.m_blunt*=factor;break;case HitData.DamageType.Slash:damage.m_slash*=factor;break;case HitData.DamageType.Pierce:damage.m_pierce*=factor;break;case HitData.DamageType.Chop:damage.m_chop*=factor;break;case HitData.DamageType.Pickaxe:damage.m_pickaxe*=factor;break;case HitData.DamageType.Fire:damage.m_fire*=factor;break;case HitData.DamageType.Frost:damage.m_frost*=factor;break;case HitData.DamageType.Lightning:damage.m_lightning*=factor;break;case HitData.DamageType.Poison:damage.m_poison*=factor;break;case HitData.DamageType.Spirit:damage.m_spirit*=factor;break;}}
    }
}
