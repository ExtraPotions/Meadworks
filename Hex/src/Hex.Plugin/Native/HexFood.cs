// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API adaptation. VAT is a behavior only design reference; no source or assemblies accessed.
// Replaces only the native remaining-time field write, retaining native healing, food strength and forced refresh.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexFood
{
    private static void WriteRemaining(Player.Food food, float remaining, Player player)
    {
        if (remaining < food.m_time && HexMovement.On(player, "disable_food_degradation")) return;
        food.m_time = remaining;
    }
    [HarmonyPatch(typeof(Player), "UpdateFood")]
    private static class NativeFoodTime
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var field = AccessTools.Field(typeof(Player.Food), nameof(Player.Food.m_time));
            var codes = instructions.ToList();
            var writers = codes.Where(code => code.opcode == OpCodes.Stfld && Equals(code.operand, field)).ToList();
            if (writers.Count != 1 || writers[0].blocks.Count != 0)
                throw new InvalidOperationException("Hex food pause refused: native remaining-time writer changed.");
            var bridge = AccessTools.DeclaredMethod(typeof(HexFood), nameof(WriteRemaining));
            foreach (var code in codes)
            {
                if (ReferenceEquals(code, writers[0]))
                {
                    var actor = new CodeInstruction(OpCodes.Ldarg_0);
                    code.MoveLabelsTo(actor);
                    yield return actor;
                    code.opcode = OpCodes.Call;
                    code.operand = bridge;
                }
                yield return code;
            }
        }
    }
}
