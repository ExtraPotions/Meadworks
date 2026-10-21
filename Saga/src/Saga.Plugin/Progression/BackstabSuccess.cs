// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
namespace ExtraPotions.Saga.Runtime {
    [HarmonyPatch(typeof(Character),"RPC_Damage",new[]{typeof(long),typeof(HitData)})]
    static class BackstabSuccess {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod){
            var original=instructions.ToList();try{
                bool installed=BackstabInsertion.TryInsert(original,__originalMethod,AccessTools.Method(typeof(BackstabCredit),nameof(BackstabCredit.ObserveSuccessfulBackstab)),out var output,out var reason);
                if(!installed)BackstabCredit.ProducerRejected(reason);return output;
            }catch(Exception error){BackstabCredit.ProducerRejected(error.GetType().Name);return original;}
        }
    }
}
