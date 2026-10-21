// Cairn - ExtraPotions Meadworks post-processing fog and ambient occlusion removal.
// Portions Copyright (c) 2023 Pete Navarra (aka Vapok), NoFogBruh 2.1.2, MIT.
// Adapted from NoFogBruh/Features/DisableFogComponent.cs at 6a1bfe69271a0db58e51459767f9cbce5b3ac580
// https://github.com/Vapok/NoFogBruh. Full notice: THIRD_PARTY_NOTICES/NoFogBruh-MIT.txt.
// Changed by expDARE, 2026: existing Cairn C01/C02 decisions replace source config; match the exact generic calls
// instead of replacing surrounding instructions, preserving native branches/labels and other post-processing effects.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later AND MIT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ExtraPotions.Cairn.Visibility;
using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace ExtraPotions.Cairn.Runtime
{
    public static class CairnPostProcessing
    {
        public static void Install(Harmony harmony)
        {
            var target = AccessTools.DeclaredMethod(typeof(PostProcessingBehaviour), "OnPreRender");
            harmony.Patch(target, transpiler: new HarmonyMethod(typeof(CairnPostProcessing), nameof(Transpile)));
        }

        // NoFogBruh's native command-buffer remove/execute behavior, with Cairn's existing per-effect decisions.
        static void Fog(PostProcessingBehaviour instance, PostProcessingComponentCommandBuffer<FogModel> component)
        {
            if (CairnVisibility.Removes(VisibilityEffect.DistanceFog)) instance.RemoveCommandBuffer<FogModel>();
            else instance.TryExecuteCommandBuffer(component);
        }

        static void Ambient(PostProcessingBehaviour instance, PostProcessingComponentCommandBuffer<AmbientOcclusionModel> component)
        {
            if (CairnVisibility.Removes(VisibilityEffect.AmbientOcclusion)) instance.RemoveCommandBuffer<AmbientOcclusionModel>();
            else instance.TryExecuteCommandBuffer(component);
        }

        static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var targets = new Dictionary<Type, string>
            {
                [typeof(FogModel)] = nameof(Fog),
                [typeof(AmbientOcclusionModel)] = nameof(Ambient),
            };
            var matches = new List<(CodeInstruction Instruction, Type Model)>();
            foreach (var instruction in code)
            {
                if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) || !(instruction.operand is MethodInfo method)
                    || method.DeclaringType != typeof(PostProcessingBehaviour) || method.Name != "TryExecuteCommandBuffer" || !method.IsGenericMethod) continue;
                var model = method.GetGenericArguments();
                if (model.Length == 1 && targets.ContainsKey(model[0])) matches.Add((instruction, model[0]));
            }
            if (matches.Count != 2 || matches.Select(m => m.Model).Distinct().Count() != 2)
            {
                Debug.LogWarning("Cairn post-processing calls changed; keeping native fog and ambient occlusion command buffers.");
                return code;
            }
            foreach (var match in matches)
            {
                match.Instruction.opcode = OpCodes.Call;
                match.Instruction.operand = AccessTools.DeclaredMethod(typeof(CairnPostProcessing), targets[match.Model]);
            }
            return code;
        }
    }
}
