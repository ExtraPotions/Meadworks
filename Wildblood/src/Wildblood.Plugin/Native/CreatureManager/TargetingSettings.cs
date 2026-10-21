// SPDX-License-Identifier: GPL-3.0-only
// Portions from CreatureManager 1.3.1 (sighsorry1029), GPL-3.0
// e0a216a1ab73fc9fbec45060ed46dae031859b5f
// Target switching adapted from CreatureManager e0a216a1ab73fc9fbec45060ed46dae031859b5f
// Changed by expDARE, 2026: effective settings, owned session reset and existing pursuit priorities.
using System;
using HarmonyLib;
using UnityEngine;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Adopted.Mutations;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Wildblood.AdoptedCm;
internal static class TargetingSettings
{
    static readonly Func<BaseAI,Vector3,bool> havePath=AccessTools.MethodDelegate<Func<BaseAI,Vector3,bool>>(AccessTools.DeclaredMethod(typeof(BaseAI),"HavePath",new[]{typeof(Vector3)}));
    internal static bool Enabled=>Plugin.NativeRulesReady&&WildbloodModules.Active(WildbloodModule.AI);
    internal static float Interval=>Plugin.Settings?.Get(WildbloodSettings.AI.Interval)??10f;
    internal static int Chance=>Plugin.Settings?.Get(WildbloodSettings.AI.Chance)??25;
    internal static float Hold=>Plugin.Settings?.Get(WildbloodSettings.AI.Hold)??5f;
    internal static long Generation=>CruciblePlugin.Session.Generation;
    internal static bool Priority(MonsterAI ai,Character creature)=>RelentlessHunters.IsHunting(ai)||Devourers.BehaviourOf(creature)?.IsProvoked==true;
    internal static bool HasPath(MonsterAI ai,Player player)=>havePath(ai,player.transform.position);
}
