// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using HarmonyLib;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexLocalTime
{
    private static readonly LocalClock clock = new();
    private static Player? actor;
    private static EnvMan? environment;
    private static long generation;
    /// <summary>Debug Mode's Shift+C day fraction; negative when unset. HexDebug owns it and clears it when Debug Mode ends.</summary>
    internal static float ViewFraction = -1f;

    internal static void Reset() { clock.Reset(); actor = null; environment = null; }

    // Only native lighting reads pass through this function. Never writes EnvMan's day fraction or debug flags.
    internal static float Presented(float native) => PresentationFraction(native);
    private static float PresentationFraction(float native)
    {
        var player = Player.m_localPlayer;
        if (ViewFraction >= 0f && HexDebug.Mode && HexMovement.Owned(player)) return ViewFraction;
        var settings = Plugin.Settings;
        var minute = settings?.Effective("local_time_minute", -1) ?? -1;
        if (!HexMovement.Owned(player) || EnvMan.instance == null || minute < 0)
        {
            Reset(); return native;
        }
        if (actor != player || environment != EnvMan.instance || generation != CruciblePlugin.Session.Generation)
        {
            Reset(); actor = player; environment = EnvMan.instance; generation = CruciblePlugin.Session.Generation;
        }
        return clock.Resolve(native, minute, settings!.Effective("local_time_freeze", false));
    }

    [HarmonyPatch]
    private static class Lighting
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(EnvMan), "FixedUpdate");
            yield return AccessTools.DeclaredMethod(typeof(EnvMan), "SetEnv");
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var codes = instructions.ToList();
            var fraction = AccessTools.Field(typeof(EnvMan), "m_smoothDayFraction");
            var begin = 0; var end = codes.Count; var expected = 1;
            if (__originalMethod.Name == "FixedUpdate")
            {
                end = codes.FindIndex(code => code.Calls(AccessTools.DeclaredMethod(typeof(EnvMan), "GetBiome")));
                var stores = codes.Take(Math.Max(0, end)).Select((code, index) => (code, index))
                    .Where(pair => pair.code.opcode == OpCodes.Stfld && Equals(pair.code.operand, fraction)).ToArray();
                if (end < 0 || stores.Length != 2)
                    throw new InvalidOperationException("Hex local time refused: native lighting boundaries changed.");
                begin = stores[1].index + 1; expected = 7;
            }
            var reads = codes.Skip(begin).Take(end - begin)
                .Where(code => code.opcode == OpCodes.Ldfld && Equals(code.operand, fraction)).ToArray();
            if (reads.Length != expected)
                throw new InvalidOperationException("Hex local time refused: native lighting fraction reads changed.");
            foreach (var code in codes)
            {
                yield return code;
                if (reads.Contains(code))
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(HexLocalTime), nameof(PresentationFraction)));
            }
        }
    }
}
