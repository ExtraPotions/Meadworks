// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ExtraPotions.Ember.Runtime
{
    [HarmonyPatch(typeof(Player), "FindHoverObject")]
    internal static class InteractionReach
    {
        const float Multiplier = 1.5f;
        internal static bool Installed { get; private set; }

        internal static float Scale(float nativeRange, Player actor)
        {
            if (!actor || actor != Player.m_localPlayer || !EmberModules.ProductActive(out _)
                || nativeRange <= 0 || float.IsNaN(nativeRange) || float.IsInfinity(nativeRange)
                || nativeRange > float.MaxValue / Multiplier) return nativeRange;
            return nativeRange * Multiplier;
        }

        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var field = typeof(Player).GetField(nameof(Player.m_maxInteractDistance), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var uses = code.Select((instruction, index) => new { instruction, index })
                .Where(row => Equals(row.instruction.operand, field)).ToList();
            Installed = false;
            if (field == null || field.IsStatic || field.FieldType != typeof(float) || uses.Count != 1)
                return Reject(code);
            var read = uses[0].index;
            if (read == 0 || code[read].opcode != OpCodes.Ldfld || code[read - 1].opcode != OpCodes.Ldarg_0
                || code[read].blocks.Count != 0) return Reject(code);
            // Scale this read only; the same native field also controls doodad detachment.
            code.InsertRange(read + 1, new[] {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, typeof(InteractionReach).GetMethod(nameof(Scale), BindingFlags.Static | BindingFlags.NonPublic))
            });
            Installed = true;
            return code;
        }

        static IEnumerable<CodeInstruction> Reject(List<CodeInstruction> code)
        {
            UnityEngine.Debug.LogWarning("Ember interaction reach left native: unexpected Player.FindHoverObject distance-read shape.");
            return code;
        }
    }
}
