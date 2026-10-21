// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Preserve the native placement/remove paths while honoring Timber's configured camera reach above 50 meters.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Timber.Camera
{
    [HarmonyPatch]
    internal static class CameraRayPatches
    {
        static readonly MethodInfo native = AccessTools.Method(typeof(Physics), nameof(Physics.Raycast),
            new[] { typeof(Vector3), typeof(Vector3), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int) });
        static readonly MethodInfo replacement = AccessTools.Method(typeof(CameraReach), nameof(CameraReach.Raycast));

        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Player), nameof(Player.PieceRayTest));
            yield return AccessTools.Method(typeof(Player), nameof(Player.RemovePiece));
            yield return AccessTools.Method(typeof(Player), nameof(Player.CopyPiece));
            yield return AccessTools.Method(typeof(Player), nameof(Player.UpdateWearNTearHover));
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var code = instructions.ToList();
            if (code.Count(i => i.Calls(native)) != 1)
                throw new InvalidOperationException("Timber camera ray site changed: " + original.Name);
            foreach (var instruction in code)
            {
                if (instruction.Calls(native))
                {
                    var player = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                    yield return player;
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }
                yield return instruction;
            }
        }
    }
}
