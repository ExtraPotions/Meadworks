// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexSpeeds
{
    private static float FlySpeed(float native, Character actor) => native * HexMovement.Factor(actor, "fly_speed");

    [HarmonyPatch(typeof(Character), "UpdateDebugFly")]
    private static class Fly
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var field = AccessTools.Field(typeof(Character), nameof(Character.m_debugFlySpeed));
            var reads = codes.Select((code, index) => (code, index))
                .Where(pair => pair.code.opcode == OpCodes.Ldsfld && Equals(pair.code.operand, field)).ToList();
            if (reads.Count != 2 || reads.Any(pair => pair.index + 1 >= codes.Count || codes[pair.index + 1].opcode != OpCodes.Conv_R4))
                throw new InvalidOperationException("Hex fly speed refused: native debug-flight speed reads changed.");
            var conversions = reads.Select(pair => codes[pair.index + 1]).ToList();
            foreach (var code in codes)
            {
                yield return code;
                if (!conversions.Contains(code)) continue;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(HexSpeeds), nameof(FlySpeed)));
            }
        }
    }

    private struct MountState
    {
        public OwnedScale Jog, Walk, Run, Swim;
    }

    // The rider's owner normally owns the mount. Never modify a mount simulated by another peer.
    private static float MountFactor(Character mount)
    {
        var player = Player.m_localPlayer;
        if (!HexMovement.Owned(player) || mount == null || mount.IsPlayer() || mount.IsDead()
            || mount.m_nview == null || !mount.m_nview.IsValid() || !mount.m_nview.IsOwner()) return 1f;
        var saddle = mount.GetComponentInChildren<Sadle>();
        return saddle != null && saddle.m_character == mount && saddle.IsLocalUser()
            && player.IsAttached() ? HexMovement.Factor(player, "mounted_speed") : 1f;
    }

    [HarmonyPatch]
    private static class Mount
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(Character), "UpdateWalking");
            yield return AccessTools.DeclaredMethod(typeof(Character), "UpdateSwimming");
        }
        private static void Prefix(Character __instance, out MountState __state)
        {
            var factor = MountFactor(__instance);
            __state = new MountState {
                Jog = new OwnedScale(__instance.m_speed, factor),
                // Native explicit walking bypasses GetJogSpeedFactor, unlike ordinary jogging.
                Walk = new OwnedScale(__instance.m_walkSpeed, factor * HexMovement.Factor(__instance, "walk_speed")),
                Run = new OwnedScale(__instance.m_runSpeed, factor), Swim = new OwnedScale(__instance.m_swimSpeed, factor) };
            __instance.m_speed = __state.Jog.Applied;
            __instance.m_walkSpeed = __state.Walk.Applied;
            __instance.m_runSpeed = __state.Run.Applied;
            __instance.m_swimSpeed = __state.Swim.Applied;
        }
        private static void Finalizer(Character __instance, MountState __state)
        {
            __instance.m_speed = __state.Jog.Restore(__instance.m_speed);
            __instance.m_walkSpeed = __state.Walk.Restore(__instance.m_walkSpeed);
            __instance.m_runSpeed = __state.Run.Restore(__instance.m_runSpeed);
            __instance.m_swimSpeed = __state.Swim.Restore(__instance.m_swimSpeed);
        }
    }
}
