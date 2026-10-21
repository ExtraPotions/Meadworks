// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;

namespace ExtraPotions.Bestiary.Runtime.Native.Mimic
{
    /// <summary>
    /// Where a crypt chest may turn out to be a mimic: the game fills a chest once, on its owner, when it first exists;
    /// a chest claimed for a mimic skips that fill (<see cref="CryptSwap.Claim"/>). A chest that wakes with a claimed but
    /// unfinished swap has it finished by its owner. Both run inside the chest's own wake-up, so a failure is reported
    /// and swallowed: the worst case is an ordinary chest.
    /// </summary>
    [HarmonyPatch(typeof(Container), "AddDefaultItems")]
    public static class CryptChestPatch
    {
        private static bool Prefix(Container __instance)
        {
            return !SafeCall.Run("Container.AddDefaultItems mimic", static chest => CryptSwap.Claim(chest), __instance, false);
        }
    }

    [HarmonyPatch(typeof(Container), "Awake")]
    public static class CryptChestAwakePatch
    {
        private static void Postfix(Container __instance) =>
            SafeCall.Run("Container.Awake mimic", static chest => CryptSwap.Resume(chest), __instance);
    }
}
