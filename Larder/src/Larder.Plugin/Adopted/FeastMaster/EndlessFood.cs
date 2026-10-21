// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Agreed C02 cheat boundary; only the native food-time assignment changes, so healing/totals still tick normally.
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    [HarmonyPatch(typeof(Player),nameof(Player.UpdateFood))]
    public static class EndlessFoodPatch
    {
        public static bool Prepare()=>NativeFeastMaster.EndlessFood;
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var field=AccessTools.Field(typeof(Player.Food),nameof(Player.Food.m_time));
            var write=AccessTools.Method(typeof(EndlessFoodPatch),nameof(WriteTime));
            int found=0;
            foreach(var instruction in instructions)
            {
                if(instruction.opcode==OpCodes.Stfld&&Equals(instruction.operand,field))
                {
                    instruction.opcode=OpCodes.Call;instruction.operand=write;found++;
                }
                yield return instruction;
            }
            if(found!=1)throw new InvalidOperationException("Native food countdown shape changed; Endless Food stays off.");
        }
        public static void WriteTime(Player.Food food,float next)
        {
            if(!NativeFeastMaster.EndlessFood)food.m_time=next;
        }
    }
}
