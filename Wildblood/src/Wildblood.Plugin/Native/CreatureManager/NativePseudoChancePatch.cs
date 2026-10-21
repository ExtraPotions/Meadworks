// SPDX-License-Identifier: GPL-3.0-only
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0
// 47e45a6e8c3b389a51e42fc7387d1a4fb90af473
// Changed by expDARE, 2026: retain the pinned two-site matcher and native amounts; stabilize keep-native pseudo chances.
// Valheim 1.0.20 guard compatibility adapted from CreatureManager 1.3.2, e6ce82aa6f1c560174931cc08afb19195c39fe0e.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Runtime;
namespace ExtraPotions.Wildblood.AdoptedCm;

// New boundary around the source matcher. The actual native algorithm continues to roll and reset counters.
internal static class NativePseudoChance
{
    internal static float ScaleChance(float chance, float vanillaFactor, CharacterDrop.Drop row, Character character)
    {
        if (ExtraPotions.Wildblood.Loot.AmountScaling.ScalesQuantity(row.m_levelMultiplier, row.m_levelMultiplierNotAmount) &&
            chance > 0f && chance <= 0.3f && WildbloodModules.Active(WildbloodModule.Loot) &&
            ZoneSystem.instance != null && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoPseudoDrops) && character != null)
        {
            EliteController? controller = character.GetComponent<EliteController>();
            if (controller != null && controller.Ready && controller.View != null && controller.View.IsValid() &&
                controller.View.IsOwner() && (controller.KeepsNativeLevel || RuleState.Active.KeepsLevel(controller.Traits.Stars, character.IsBoss())))
                return chance;
        }
        return chance * vanillaFactor;
    }
    // Keep the same unchecked native multiplication; the adopted source matcher validates both sites atomically.
    internal static int ScaleAmount(int amount, int vanillaFactor, CharacterDrop.Drop row, Character character) => unchecked(amount * vanillaFactor);
}

[HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
internal static class NativePseudoChancePatch
{
    internal static bool Applied { get; private set; }
    private static bool mismatchWarningLogged;
    private static readonly FieldInfo instructionLabels = AccessTools.Field(typeof(CodeInstruction), "labels");
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter("asharppen.valheim.drop_that")]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        Applied = false;
        List<CodeInstruction> codes = instructions.ToList();
        FieldInfo levelMultiplier = AccessTools.Field(typeof(CharacterDrop.Drop), nameof(CharacterDrop.Drop.m_levelMultiplier));
        FieldInfo chanceOnly = AccessTools.Field(typeof(CharacterDrop.Drop), nameof(CharacterDrop.Drop.m_levelMultiplierNotAmount));
        FieldInfo character = AccessTools.Field(typeof(CharacterDrop), "m_character");
        MethodInfo chance = AccessTools.Method(typeof(NativePseudoChance), nameof(NativePseudoChance.ScaleChance));
        MethodInfo amount = AccessTools.Method(typeof(NativePseudoChance), nameof(NativePseudoChance.ScaleAmount));
        List<(int Index, CodeInstruction Drop, MethodInfo Method)> matches = new();
        int multiplierReads = 0;
        for (int i = 1; i + 6 < codes.Count; i++)
        {
            if (!codes[i].LoadsField(levelMultiplier)) continue;
            multiplierReads++;
            // Match the two guarded multiplications, not other chance/resource/pseudo-drop
            // arithmetic. Discover locals instead of depending on their compiler-assigned slots.
            if (LoadLocalIndex(codes[i - 1]) < 0 ||
                (codes[i + 1].opcode != OpCodes.Brfalse && codes[i + 1].opcode != OpCodes.Brfalse_S) ||
                LoadLocalIndex(codes[i + 2]) < 0)
            {
                continue;
            }

            int operands = i + 2;
            bool amountGuard = i + 8 < codes.Count && codes[i + 3].LoadsField(chanceOnly) &&
                (codes[i + 4].opcode == OpCodes.Brtrue || codes[i + 4].opcode == OpCodes.Brtrue_S) &&
                BranchesToSameTarget(codes, codes[i + 1], codes[i + 4]) &&
                LoadLocalIndex(codes[i + 2]) == LoadLocalIndex(codes[i - 1]);
            if (amountGuard) operands += 3;
            if (LoadLocalIndex(codes[operands]) < 0 || LoadLocalIndex(codes[operands + 1]) < 0) continue;
            bool isChance = codes[operands + 2].opcode == OpCodes.Conv_R4;
            if (isChance == amountGuard) continue;
            int multiply = operands + (isChance ? 3 : 2);
            if (codes[multiply].opcode != OpCodes.Mul ||
                StoreLocalIndex(codes[multiply + 1]) != LoadLocalIndex(codes[operands]))
            {
                continue;
            }

            matches.Add((multiply, codes[i - 1], isChance ? chance : amount));
        }

        if (character == null || chanceOnly == null || chance == null || amount == null || multiplierReads != 2 ||
            matches.Count != 2 || matches.Count(match => match.Method == chance) != 1 ||
            matches.Count(match => match.Method == amount) != 1)
        {
            if (!mismatchWarningLogged)
            {
                mismatchWarningLogged = true;
                CreatureManagerPlugin.Log.LogWarning("Could not locate both CharacterDrop star multipliers; Wildblood stable pseudo-drop chance is unavailable; the existing drop calculation is preserved.");
            }
            return codes;
        }

        // Validate both sites before changing either. A partial patch could keep exponential
        // amounts with base chances (or the reverse). Drop instances and their metadata stay intact.
        foreach (var match in matches.OrderByDescending(match => match.Index))
        {
            CodeInstruction original = codes[match.Index];
            CodeInstruction loadDrop = new(match.Drop.opcode, match.Drop.operand);
            original.MoveLabelsTo(loadDrop);
            loadDrop.blocks.AddRange(original.blocks);
            codes.RemoveAt(match.Index);
            codes.InsertRange(match.Index, new[]
            {
                loadDrop,
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, character),
                new CodeInstruction(OpCodes.Call, match.Method)
            });
        }

        Applied = true;
        return codes;
    }

    private static bool BranchesToSameTarget(List<CodeInstruction> codes, CodeInstruction first, CodeInstruction second)
    {
        // Harmony may allocate different labels for one original branch destination.
        return first.operand != null && second.operand != null && instructionLabels != null &&
            codes.Any(code => instructionLabels.GetValue(code) is IList labels &&
                labels.Contains(first.operand) && labels.Contains(second.operand));
    }

    private static int LoadLocalIndex(CodeInstruction code)
    {
        if (code.opcode == OpCodes.Ldloc_0) return 0;
        if (code.opcode == OpCodes.Ldloc_1) return 1;
        if (code.opcode == OpCodes.Ldloc_2) return 2;
        if (code.opcode == OpCodes.Ldloc_3) return 3;
        return code.opcode == OpCodes.Ldloc || code.opcode == OpCodes.Ldloc_S
            ? OperandLocalIndex(code.operand) : -1;
    }

    private static int StoreLocalIndex(CodeInstruction code)
    {
        if (code.opcode == OpCodes.Stloc_0) return 0;
        if (code.opcode == OpCodes.Stloc_1) return 1;
        if (code.opcode == OpCodes.Stloc_2) return 2;
        if (code.opcode == OpCodes.Stloc_3) return 3;
        return code.opcode == OpCodes.Stloc || code.opcode == OpCodes.Stloc_S
            ? OperandLocalIndex(code.operand) : -1;
    }

    private static int OperandLocalIndex(object operand) => operand switch
    {
        LocalVariableInfo local => local.LocalIndex,
        byte index => index,
        short index => index,
        int index => index,
        _ => -1
    };
}
