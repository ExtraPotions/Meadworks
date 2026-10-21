// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Progression;
using ExtraPotions.Saga.Skills;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using HarmonyLib;
namespace ExtraPotions.Saga.Runtime {
    [HarmonyPatch(typeof(global::Skills),nameof(global::Skills.RaiseSkill))]
    static class GainNotices {
        static void Prefix(global::Skills __instance,global::Skills.SkillType __0,out SkillProgress? __state){__state=null;if(__instance.m_player!=Player.m_localPlayer||!NativeSaga.Active("progression")||!SkillNotices.ShouldShow(Plugin.Settings!.Get(SagaSettings.Progression.SkillGainNotices),SagaSkills.TryGetByType((int)__0,out _)))return;var skill=__instance.GetSkill(__0);if(skill!=null)__state=new SkillProgress(skill.m_level,skill.m_accumulator);}
        static void Postfix(global::Skills __instance,global::Skills.SkillType __0,SkillProgress? __state){if(!__state.HasValue)return;var skill=__instance.GetSkill(__0);if(skill==null)return;var before=__state.Value;var gain=SkillMath.ExperienceToReach(before.Level,skill.m_level)+skill.m_accumulator-before.Accumulator;if(gain<=0||float.IsNaN(gain)||float.IsInfinity(gain))return;__instance.m_player.Message(MessageHud.MessageType.TopLeft,SagaText.Format("skill_gain",GameLocalization.instance.Localize("$skill_"+__0.ToString().ToLowerInvariant()),gain.ToString("0.##")));}
    }
}
