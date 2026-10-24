// SPDX-License-Identifier: GPL-3.0-only
// Portions from CreatureManager 1.3.1 (sighsorry1029), GPL-3.0
// e0a216a1ab73fc9fbec45060ed46dae031859b5f
// Target switching adapted from CreatureManager e0a216a1ab73fc9fbec45060ed46dae031859b5f
// Changed by expDARE, 2026: effective settings, owned session reset and existing pursuit priorities.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
namespace ExtraPotions.Wildblood.AdoptedCm;
[HarmonyPatch]
internal static class CreatureTargetingPatch
{
    static MethodBase TargetMethod()=>AccessTools.DeclaredMethod(typeof(MonsterAI),"UpdateTarget",new[]{typeof(Humanoid),typeof(float),typeof(bool).MakeByRefType(),typeof(bool).MakeByRefType()});
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes=instructions.ToList();
        var find=AccessTools.DeclaredMethod(typeof(BaseAI),"FindEnemy",Type.EmptyTypes);
        var character=AccessTools.DeclaredField(typeof(BaseAI),"m_character");var view=AccessTools.DeclaredField(typeof(BaseAI),"m_nview");
        if(find==null||character==null||view==null||codes.Count(code=>code.Calls(find))!=1)
        {
            ExtraPotions.Wildblood.Runtime.Plugin.NativeLogger?.LogWarning("Unique MonsterAI.UpdateTarget enemy-search anchor unavailable; player target switching inactive.");return codes;
        }
        int index=codes.FindIndex(code=>code.Calls(find));
        codes.InsertRange(index+1,new[]{new CodeInstruction(OpCodes.Ldarg_0),new CodeInstruction(OpCodes.Ldarg_0),new CodeInstruction(OpCodes.Ldfld,character),new CodeInstruction(OpCodes.Ldarg_0),new CodeInstruction(OpCodes.Ldfld,view),new CodeInstruction(OpCodes.Call,AccessTools.DeclaredMethod(typeof(CreatureTargeting),nameof(CreatureTargeting.SelectTarget)))});
        return codes;
    }
}
[HarmonyPatch(typeof(Character),"OnDestroy")]
internal static class CreatureTargetingDestroyPatch
{
    static void Prefix(Character __instance){CreatureTargeting.ForgetCharacter(__instance);CreatureSpawnBlocker.ForgetCharacter(__instance);CreatureModifierManager.ForgetCreatureCaches(__instance);}
}
