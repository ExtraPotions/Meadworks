// Portions Copyright (C) 2026 Gerald Glasgow, GrindstoneSkills at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: CS1 page content, Crucible kit rendering, owned layout restoration and row actions.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace ExtraPotions.Saga.Runtime {
    public static class NativeBook {
        static readonly Dictionary<global::Skills.SkillType,Action<SkillPage>> writers=new Dictionary<global::Skills.SkillType,Action<SkillPage>> {
            [global::Skills.SkillType.Swords]=MeleePages.Swords,[global::Skills.SkillType.Knives]=MeleePages.Knives,[global::Skills.SkillType.Clubs]=MeleePages.Clubs,[global::Skills.SkillType.Polearms]=MeleePages.Polearms,
            [global::Skills.SkillType.Spears]=MeleePages.Spears,[global::Skills.SkillType.Axes]=MeleePages.Axes,[global::Skills.SkillType.Unarmed]=MeleePages.Unarmed,
            [global::Skills.SkillType.Bows]=RangedPages.Bows,[global::Skills.SkillType.Crossbows]=RangedPages.Crossbows,[global::Skills.SkillType.ElementalMagic]=MagicPages.ElementalMagic,[global::Skills.SkillType.BloodMagic]=MagicPages.BloodMagic,
            [global::Skills.SkillType.Blocking]=BlockingPage.Write,[global::Skills.SkillType.Run]=MovementPages.Run,[global::Skills.SkillType.Swim]=MovementPages.Swim,[global::Skills.SkillType.Jump]=MovementPages.Jump,
            [global::Skills.SkillType.Sneak]=MovementPages.Sneak,[global::Skills.SkillType.Dodge]=MovementPages.Dodge,[global::Skills.SkillType.Ride]=MovementPages.Ride,[global::Skills.SkillType.Crafting]=CraftingPage.Write,
            [global::Skills.SkillType.WoodCutting]=WoodcuttingPage.Write,[global::Skills.SkillType.Pickaxes]=PickaxesPage.Write,[global::Skills.SkillType.Fishing]=FishingPage.Write,
            [(global::Skills.SkillType)SagaSkills.DefenseType]=DefensePage.Write,[(global::Skills.SkillType)SagaSkills.ForagingType]=ForagingPage.Write
        };
        static readonly List<IDisposable> leases=new List<IDisposable>();
        static readonly Dictionary<string,Func<int,Tuple<string,Action>>> actions=new Dictionary<string,Func<int,Tuple<string,Action>>>(StringComparer.Ordinal);
        static readonly List<Pane> panes=new List<Pane>();
        static global::Skills.SkillType remembered;
        static int sortMode;
        static bool preparing;
        static List<global::Skills.Skill> Sorted(Player player) {
            var skills=player.GetSkills().GetSkillList();
            if(sortMode==1)return skills.OrderBy(s=>GameLocalization.instance.Localize("$skill_"+s.m_info.m_skill.ToString().ToLowerInvariant()),StringComparer.CurrentCulture).ToList();
            if(sortMode==2)return skills.OrderByDescending(s=>s.m_level).ThenBy(s=>(int)s.m_info.m_skill).ToList();
            return skills;
        }
        [HarmonyPatch(typeof(global::Skills),nameof(global::Skills.GetSkillList))]static class SortList {
            static void Postfix(ref List<global::Skills.Skill> __result){
                if(!preparing||sortMode==0)return;
                __result=sortMode==1?__result.OrderBy(s=>GameLocalization.instance.Localize("$skill_"+s.m_info.m_skill.ToString().ToLowerInvariant()),StringComparer.CurrentCulture).ToList():__result.OrderByDescending(s=>s.m_level).ThenBy(s=>(int)s.m_info.m_skill).ToList();
            }
        }
        public static int PageCount=>writers.Count;
        public static void Initialize(){
            sortMode=PlayerPrefs.GetInt("GrindstoneSkills_SkillSort",0);
            if(leases.Count>0)return;
            foreach(var pair in writers){var type=pair.Key;leases.Add(MeadworksSkills.RegisterPage(Plugin.Guid,type,(player,level)=>Write(player,type)));}AddRowAction("saga.angler",type=>type==(int)global::Skills.SkillType.Fishing?Tuple.Create(SagaText.Text("angler_log"),(Action)NativeAngler.Open):null!);}
        static IReadOnlyList<SkillPageLine> Write(Player player,global::Skills.SkillType type) {
            var info=player.GetSkills().m_skills.FirstOrDefault(s=>s.m_skill==type);
            var page=new SkillPage(player,type,info?.m_description??"");writers[type](page);
            var result=new List<SkillPageLine>{new SkillPageLine(page.About)};
            result.AddRange(page.Entries.Select(e=>new SkillPageLine(e.Text,e.IsHeading,e.Term,e.Tip)));
            foreach(var perk in page.Perks){var text=perk.Name+(page.Level>=perk.Level?"  "+SagaText.Text("perk_unlocked"):"  "+SagaText.Format("level_required",perk.Level));if(page.Level<perk.Level)text="<color=#888888>"+text+"</color>";result.Add(new SkillPageLine(text,false,perk.Name,perk.Tip));}
            if(type==global::Skills.SkillType.Sneak&&NativeSaga.SneakSpeedActive)result.Add(new SkillPageLine(SagaText.Format("perk_sneak_speed",ExtraPotions.Saga.Skills.SneakRules.SpeedMultiplier(page.Level,NativeSaga.SneakSpeed))));
            if(type==global::Skills.SkillType.Sneak)NativeSneak.Page(player,result);
            if(type==global::Skills.SkillType.Run)NativeRunning.Page(player,result);
            if(type==global::Skills.SkillType.Jump)NativeJumping.Page(player,result);
            if(type==global::Skills.SkillType.Dodge)NativeDodging.Page(player,result);
            NativeMelee.Page(player,type,result);
            NativeRanged.Page(player,type,result);
            if(type==global::Skills.SkillType.Crafting)NativeCrafting.Page(player,result);
            if(type==global::Skills.SkillType.Swim)NativeSwimming.Page(player,result);
            if(type==global::Skills.SkillType.Ride)NativeRiding.Page(player,result);
            float recovery=RecoveryXp.Rate(player,type);
            if(recovery>1)result.Add(new SkillPageLine(SagaText.Format("skill_recovery_rate",recovery)));
            return result;
        }
        public static void AddRowAction(string id,Func<int,Tuple<string,Action>> action){if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("Action identity required");if(action==null)actions.Remove(id);else actions[id]=action;}
        public static float GetLevel(object player,int type)=>player is Player p?MeadworksSkills.EffectiveLevel(p,(global::Skills.SkillType)type):0;
        public static bool SetLevel(object target,int type,float level,float accumulator) {
            if(!(target is Player player)||player!=Player.m_localPlayer||!NativeSaga.Active("progression")||!MeadworksSkills.EditDecision("com.meadworks.hex").Allowed||float.IsNaN(level)||float.IsInfinity(level)||float.IsNaN(accumulator)||float.IsInfinity(accumulator)||accumulator<0)return false;
            var skillType=(global::Skills.SkillType)type;if(!global::Skills.IsSkillValid(skillType))return false;
            var skill=player.GetSkills().GetSkill(skillType);if(skill==null)return false;
            skill.m_level=Mathf.Clamp(level,0,100);skill.m_accumulator=skill.m_level>=100?0:Mathf.Min(accumulator,SkillMath.NextLevelRequirement(skill.m_level));RecoveryXp.NoteCurrent(player);return true;
        }
        public static IReadOnlyList<Tuple<int,string,string>> AllSkills()=>Player.m_localPlayer?.GetSkills().m_skills.Where(s=>s.m_skill>0).Select(s=>Tuple.Create((int)s.m_skill,GameLocalization.instance.Localize("$skill_"+s.m_skill.ToString().ToLowerInvariant()),MeadworksSkills.TryGet(s.m_skill,out var custom)?custom.Definition.Owner:"valheim")).ToArray()??Array.Empty<Tuple<int,string,string>>();
        public static IReadOnlyDictionary<string,Delegate> Capability()=>new Dictionary<string,Delegate>{["GetLevel"]=new Func<object,int,float>(GetLevel),["SetLevel"]=new Func<object,int,float,float,bool>(SetLevel),["AllSkills"]=new Func<IReadOnlyList<Tuple<int,string,string>>>(AllSkills),["AddRowAction"]=new Action<string,Func<int,Tuple<string,Action>>>(AddRowAction)};
        static bool Allowed=>Plugin.AllowsUi&&NativeSaga.Active("progression")&&Plugin.Settings!.Get(SagaSettings.Progression.SkillBook);
        static Pane? Find(SkillsDialog dialog)=>panes.FirstOrDefault(p=>p.Dialog==dialog);
        [HarmonyPatch(typeof(SkillsDialog),nameof(SkillsDialog.Setup))]static class Open {
            static void Prefix(){preparing=Allowed;}
            static Exception? Finalizer(Exception? __exception){preparing=false;return __exception;}
            static void Postfix(SkillsDialog __instance,Player player){if(!Allowed||player==null)return;var pane=Find(__instance);if(pane==null){pane=Pane.Create(__instance);if(pane==null)return;panes.Add(pane);}pane.Open(player);}
        }
        [HarmonyPatch(typeof(SkillsDialog),nameof(SkillsDialog.SkillClicked))]static class Click {static void Postfix(SkillsDialog __instance,GameObject selectedObject){if(Allowed)Find(__instance)?.Show(__instance.m_elements.IndexOf(selectedObject));}}
        [HarmonyPatch(typeof(SkillsDialog),nameof(SkillsDialog.Update))]static class Follow {static bool Prefix(SkillsDialog __instance)=>Find(__instance)?.Pad()??true;static void Postfix(SkillsDialog __instance){var pane=Find(__instance);if(pane==null)return;if(!Allowed){pane.Dispose();panes.Remove(pane);return;}if(ZInput.IsExclusiveGamepadActive()&&!ZInput.IsTouchActive())pane.Show(__instance.m_selectionIndex);pane.Refresh();}}
        sealed class Pane {
            public SkillsDialog Dialog=null!;RectTransform frame=null!,list=null!,right=null!;Vector2 frameSize,listPosition,listSize;GameObject? sortControl;KitStatSheet sheet=null!;Player player=null!;int shown=-1,focus=-1;float refreshAt;readonly List<GameObject> buttons=new List<GameObject>();
            List<global::Skills.Skill> displayed=new List<global::Skills.Skill>();TMP_Text? pageTitle,pageLevel;Image? pageIcon;GameObject? sortLabel;
            static string SortCaption()=>sortMode==1?"Name":sortMode==2?"Level":"Default";
            public bool Pad(){if(!Allowed||!MeadworksInput.GamepadActive||MeadworksInput.Typing||!MeadworksInput.Allows(InputContext.Window))return true;if(focus==-1){if(MeadworksInput.PadDown(PadButton.DPadRight)){focus=-2;MeadworksInput.PadConsume(PadButton.DPadRight);return false;}return true;}
                if(MeadworksInput.PadDown(PadButton.DPadLeft)||MeadworksInput.PadDown(PadButton.B)){focus=-1;MeadworksInput.PadConsume(PadButton.DPadLeft);MeadworksInput.PadConsume(PadButton.B);return false;}
                if(focus==-2){var scroll=sheet.Area.GetComponentInChildren<ScrollRect>();if(MeadworksInput.PadDown(PadButton.DPadDown)){if(scroll!=null)scroll.verticalNormalizedPosition=Mathf.Max(0,scroll.verticalNormalizedPosition-.1f);MeadworksInput.PadConsume(PadButton.DPadDown);}if(MeadworksInput.PadDown(PadButton.DPadUp)){if(scroll!=null)scroll.verticalNormalizedPosition=Mathf.Min(1,scroll.verticalNormalizedPosition+.1f);MeadworksInput.PadConsume(PadButton.DPadUp);}if(MeadworksInput.PadDown(PadButton.DPadRight)&&buttons.Count>0){focus=0;buttons[focus].GetComponent<Button>().Select();MeadworksInput.PadConsume(PadButton.DPadRight);}return false;}
                if(buttons.Count==0){focus=-2;return false;}focus=Mathf.Min(focus,buttons.Count-1);if(MeadworksInput.PadDown(PadButton.DPadDown)){focus=(focus+1)%buttons.Count;MeadworksInput.PadConsume(PadButton.DPadDown);}if(MeadworksInput.PadDown(PadButton.DPadUp)){focus=(focus+buttons.Count-1)%buttons.Count;MeadworksInput.PadConsume(PadButton.DPadUp);}var button=buttons[focus].GetComponent<Button>();button.Select();if(MeadworksInput.PadDown(PadButton.A)){MeadworksInput.PadConsume(PadButton.A);button.onClick.Invoke();}return false;
            }
            public static Pane? Create(SkillsDialog dialog){var list=dialog.skillListScrollRect?.transform.parent as RectTransform;var frame=list?.parent as RectTransform;if(frame==null||list==null)return null;var pane=new Pane{Dialog=dialog,frame=frame,list=list,frameSize=frame.sizeDelta,listPosition=list.anchoredPosition,listSize=list.sizeDelta};frame.sizeDelta+=new Vector2(424,128);list.sizeDelta+=new Vector2(0,104);list.anchoredPosition+=new Vector2(-212,-24);
                var sortLabel=KitCanvas.Text(frame,"Saga_sort_label","Sort by",14,KitAppearance.Color(KitColorRole.Accent),TextAlignmentOptions.Left,false);
                sortLabel.rectTransform.anchorMin=sortLabel.rectTransform.anchorMax=sortLabel.rectTransform.pivot=new Vector2(0,1);sortLabel.rectTransform.anchoredPosition=new Vector2(24,-46);sortLabel.rectTransform.sizeDelta=new Vector2(64,24);
                var control=KitButton.Create(frame,"Saga_sort",SortCaption(),()=>{sortMode=(sortMode+1)%3;PlayerPrefs.SetInt("GrindstoneSkills_SkillSort",sortMode);PlayerPrefs.Save();dialog.Setup(pane.player);},new Vector2(88,-46),new Vector2(92,24));
                pane.sortControl=control?.gameObject; pane.sortLabel=sortLabel.gameObject;pane.right=(RectTransform)new GameObject("Saga_skillinfo",typeof(RectTransform)).transform;pane.right.SetParent(frame,false);pane.right.anchorMin=pane.right.anchorMax=new Vector2(.5f,1);pane.right.pivot=new Vector2(0,1);pane.right.anchoredPosition=new Vector2(list.anchoredPosition.x+list.sizeDelta.x/2+24,list.anchoredPosition.y);pane.right.sizeDelta=new Vector2(392,list.sizeDelta.y);
                pane.pageIcon=new GameObject("Saga_skill_icon",typeof(RectTransform),typeof(Image)).GetComponent<Image>();pane.pageIcon.transform.SetParent(pane.right,false);pane.pageIcon.preserveAspect=true;pane.pageIcon.raycastTarget=false;pane.pageIcon.rectTransform.anchorMin=pane.pageIcon.rectTransform.anchorMax=pane.pageIcon.rectTransform.pivot=new Vector2(0,1);pane.pageIcon.rectTransform.sizeDelta=new Vector2(30,30);
                pane.pageTitle=KitCanvas.Text(pane.right,"Saga_skill_title","",28,KitAppearance.Color(KitColorRole.Accent),TextAlignmentOptions.Left,false);if(KitCanvas.Heading!=null)pane.pageTitle.font=KitCanvas.Heading;
                pane.pageLevel=KitCanvas.Text(pane.right,"Saga_skill_level","",16,KitAppearance.Color(KitColorRole.Text),TextAlignmentOptions.Left,false);
                foreach(var text in new[]{pane.pageTitle,pane.pageLevel}){text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=new Vector2(0,1);text.rectTransform.sizeDelta=new Vector2(354,28);}
                pane.pageTitle.rectTransform.anchoredPosition=new Vector2(38,0);pane.pageLevel.rectTransform.anchoredPosition=new Vector2(38,-28);
                pane.sheet=KitStatSheet.Create(pane.right,"Saga_skill_sheet",new Vector2(0,-52),new Vector2(392/1.35f,(pane.right.sizeDelta.y-52)/1.35f));
                if(pane.sheet!=null)pane.sheet.Area.localScale=new Vector3(1.35f,1.35f,1);if(pane.sheet==null){pane.Dispose();return null;}return pane;}
            public void Open(Player owner){player=owner;shown=-1;focus=-1;if(sortControl!=null){var caption=sortControl.GetComponentInChildren<TMP_Text>();if(caption!=null)caption.text=SortCaption();}displayed=Sorted(player);var skills=displayed;var index=skills.FindIndex(s=>s.m_info.m_skill==remembered);Dialog.m_selectionIndex=index>=0?index:0;Show(Dialog.m_selectionIndex);}
            public void Refresh(){if(Time.unscaledTime<refreshAt||!Dialog.gameObject.activeInHierarchy||shown<0)return;refreshAt=Time.unscaledTime+1;var index=shown;shown=-1;Show(index);}
            public void Show(int index){if(player==null||index==shown)return;var skills=displayed;if(index<0||index>=skills.Count)return;shown=index;var skill=skills[index];remembered=skill.m_info.m_skill;var type=remembered;pageIcon!.sprite=skill.m_info.m_icon;pageTitle!.text=GameLocalization.instance.Localize("$skill_"+type.ToString().ToLowerInvariant());pageLevel!.text=SagaText.Format("level_required",skill.m_level.ToString("0.#"))+" - "+(skill.GetLevelPercentage()*100).ToString("0.#")+"%";
                sheet.Show(s=> {
                float running=skill.m_level;foreach(var effect in player.GetSEMan().m_statusEffects){float before=running;effect.ModifySkillLevel(type,ref running);float bonus=running-before;if(Mathf.Abs(bonus)>.001f)s.Add(GameLocalization.instance.Localize(effect.m_name),bonus.ToString("+0.#;-0.#"),GameLocalization.instance.Localize(effect.m_tooltip));}
                foreach(var bonus in MeadworksSkills.Modifiers.For(SkillId.FromRaw((int)type)).Where(m=>m.Kind==SkillModifierKind.Level&&Mathf.Abs(m.Value)>.001f))s.Add(bonus.Owner,bonus.Value.ToString("+0.#;-0.#"));
                float effective=MeadworksSkills.EffectiveLevel(player,type);if(Mathf.Abs(effective-skill.m_level)>.001f)s.Add(SagaText.Text("effective_level"),effective.ToString("0.#"));
                var lines=MeadworksSkills.PagesFor(player,type);if(lines.Count==0)s.Add(GameLocalization.instance.Localize(skill.m_info.m_description??""),"");foreach(var line in lines){if(line.Heading)s.Section(GameLocalization.instance.Localize(line.Text));else s.Add(GameLocalization.instance.Localize(line.Text),"",GameLocalization.instance.Localize(line.Tooltip));}
            });var rows=actions.Values.Select(action=>action((int)type)).Where(row=>row!=null).ToArray();float height=Mathf.Max(120,right.sizeDelta.y-52-rows.Length*28-(rows.Length>0?8:0));sheet.Area.sizeDelta=new Vector2(392/1.35f,height/1.35f);for(int position=0;position<rows.Length;position++){var row=rows[position];var control=position<buttons.Count?buttons[position].GetComponent<Button>():KitButton.Create(right,"Saga_action_"+position,row.Item1,row.Item2,new Vector2(8,-height-60-position*28),new Vector2(376,24));if(control==null)continue;if(position>=buttons.Count)buttons.Add(control.gameObject);control.GetComponent<RectTransform>().anchoredPosition=new Vector2(8,-height-60-position*28);var caption=control.GetComponentInChildren<TMP_Text>();if(caption!=null)caption.text=row.Item1;control.onClick.RemoveAllListeners();control.onClick.AddListener(()=>{try{row.Item2();}catch(Exception error){NativeSaga.Report(error,"skill row action");}});}while(buttons.Count>rows.Length){UnityEngine.Object.Destroy(buttons[buttons.Count-1]);buttons.RemoveAt(buttons.Count-1);}if(focus>=buttons.Count)focus=-2;}
            public void Dispose(){buttons.Clear();if(right!=null)UnityEngine.Object.Destroy(right.gameObject);if(frame!=null)frame.sizeDelta=frameSize;if(list!=null){list.anchoredPosition=listPosition;list.sizeDelta=listSize;}if(sortControl!=null)UnityEngine.Object.Destroy(sortControl);if(sortLabel!=null)UnityEngine.Object.Destroy(sortLabel);}
        }
        public static void Shutdown(){foreach(var pane in panes)pane.Dispose();panes.Clear();for(int i=leases.Count-1;i>=0;i--)leases[i].Dispose();leases.Clear();actions.Clear();}
    }
}
