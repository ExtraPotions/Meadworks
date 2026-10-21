// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using System.Reflection;
using ExtraPotions.Wildblood.Adopted.Runtime;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The server's handler for a new global key - every boss defeat arrives here, from whichever machine owned the
    /// boss. The tier is read before and after the key lands and a rise is announced. It runs inside the game's own key
    /// handling, so a failure here is reported under the mod's name and swallowed rather than losing the key.
    /// </summary>
    [HarmonyPatch(typeof(ZoneSystem), "RPC_SetGlobalKey")]
    public static class TierRisePatch
    {
        private static void Prefix(out int __state)
        {
            int before = 0;
            SafeCall.Run("ZoneSystem.RPC_SetGlobalKey tier before", () => before = WorldTier.Current());
            __state = before;
        }

        private static void Postfix(int __state) =>
            SafeCall.Run("ZoneSystem.RPC_SetGlobalKey tier after", () => TierAnnounce.IfRisen(__state));
    }

    /// <summary>
    /// World start, on every machine: the tier message's handler is registered before any boss can fall, and a listed
    /// boss key that no known boss sets is logged once.
    /// </summary>
    [HarmonyPatch(typeof(ZoneSystem), "Start")]
    public static class TierStartPatch
    {
        private static void Postfix() => SafeCall.Run("ZoneSystem.Start tiers", Start);

        private static void Start()
        {
            TierAnnounce.EnsureRegistered();
            WorldTier.WarnUnknownKeys();
        }
    }

    /// <summary>
    /// Every change to the world's global keys, on every machine: the game adds, removes and clears them only through
    /// these three, whether the server sets a key or a client receives the list, and recomputes the world's rates after
    /// each (the fourth, which also follows a reset). The world tier counts again on its next read
    /// (<see cref="WorldTier.KeysChanged"/>), so a tier read every frame is a cached number, never stale.
    /// </summary>
    /// <remarks>A game that renamed one of them skips that one (the tier then also counts again every few seconds).</remarks>
    [HarmonyPatch]
    public static class TierKeysPatch
    {
        private static readonly string[] Changes = { "GlobalKeyAdd", "GlobalKeyRemove", "ClearGlobalKeys", "UpdateWorldRates" };

        private static bool Prepare() => TargetMethods().GetEnumerator().MoveNext();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in Changes)
            {
                MethodInfo? method = AccessTools.Method(typeof(ZoneSystem), name);
                if (method != null)
                {
                    yield return method;
                }
            }
        }

        private static void Postfix() => WorldTier.KeysChanged();
    }
}
