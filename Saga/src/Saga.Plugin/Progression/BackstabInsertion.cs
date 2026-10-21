// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
namespace ExtraPotions.Saga.Runtime {
    public static class BackstabInsertion {
        static readonly FieldInfo? Labels=AccessTools.Field(typeof(CodeInstruction),"labels");
        // The bundled Harmony labels use the Mono mscorlib type; inspect without binding that generic Label type in netstandard.
        static bool HasLabels(CodeInstruction code)=>!(Labels?.GetValue(code) is ICollection labels)||labels.Count!=0;
        public static bool TryInsert(IEnumerable<CodeInstruction> input,MethodBase original,MethodInfo callback,out List<CodeInstruction> output,out string reason){
            output=input.ToList();reason="native success anchor unavailable";
            var body=original.GetMethodBody();var args=original.GetParameters();var owner=original.DeclaringType;
            if(owner==null||owner.Name!="Character"||original.Name!="RPC_Damage"||original.IsStatic||args.Length!=2||args[0].ParameterType!=typeof(long)||args[1].ParameterType.Name!="HitData"||body==null||body.LocalVariables.Count==0||body.LocalVariables[0].LocalType!=owner)return false;
            if(!callback.IsStatic||callback.ReturnType!=typeof(void)||callback.GetParameters().Length!=2||callback.GetParameters().Any(p=>p.ParameterType!=owner))return false;
            var stamp=AccessTools.Field(owner,"m_backstabTime");var bonus=AccessTools.Field(args[1].ParameterType,"m_backstabBonus");var apply=AccessTools.Method(args[1].ParameterType,"ApplyModifier",new[]{typeof(float)});
            if(stamp==null||bonus==null||apply==null)return false;
            if(output.Count(code=>code.opcode==OpCodes.Stfld&&Equals(code.operand,stamp))!=1){reason="native success stamp count changed";return false;}
            var matches=new List<int>();
            for(int i=0;i+7<output.Count;i++){
                var time=output[i+1].operand as MethodInfo;
                if(output[i].opcode==OpCodes.Ldarg_0&&output[i+1].opcode==OpCodes.Call&&time!=null&&time.Name=="get_time"&&time.DeclaringType?.FullName=="UnityEngine.Time"&&output[i+2].opcode==OpCodes.Stfld&&Equals(output[i+2].operand,stamp)&&output[i+3].opcode==OpCodes.Ldarg_2&&output[i+4].opcode==OpCodes.Ldarg_2&&output[i+5].opcode==OpCodes.Ldfld&&Equals(output[i+5].operand,bonus)&&output[i+6].opcode==OpCodes.Callvirt&&Equals(output[i+6].operand,apply))matches.Add(i);
            }
            if(matches.Count!=1){reason="native success anchor count "+matches.Count;return false;}
            int start=matches[0],insert=start+7;
            if(output.Skip(start).Take(8).Any(code=>code.blocks.Count!=0)||output.Skip(start+1).Take(7).Any(code=>HasLabels(code))){reason="native success anchor boundary changed";return false;}
            // Every original instruction object, label and exception block remains exactly where Harmony supplied it.
            output.InsertRange(insert,new[]{new CodeInstruction(OpCodes.Ldarg_0),new CodeInstruction(OpCodes.Ldloc_0),new CodeInstruction(OpCodes.Call,callback)});reason="";return true;
        }
    }
}
