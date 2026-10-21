// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Arsenal;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using ExtraPotions.Bestiary.Runtime.Native.Crossbow;
using ExtraPotions.Bestiary.Runtime.Native.Headsman;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Bestiary.Runtime.Native
{
    /// <summary>
    /// Every player carries the bone weapons' holds (<see cref="ArsenalAtgeirHold"/>, <see cref="XbowHold"/> with
    /// <see cref="XbowPlayerRig"/>, <see cref="GreataxeHold"/>) on every peer that draws, from one patch. They only pose
    /// and animate what is seen (a player's attacks run on that player's own machine), so a dedicated server gets none.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.Awake))]
    public static class PlayerHolds
    {
        private static void Postfix(Player __instance)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return;
            }
            SafeCall.Run("Player.Awake bone weapon holds", static player => Add(player.gameObject), __instance);
        }

        private static void Add(GameObject player)
        {
            if (player.GetComponent<ArsenalAtgeirHold>() == null)
            {
                Adapters.NativeRuntime.Own(player.AddComponent<ArsenalAtgeirHold>());
            }
            if (player.GetComponent<XbowHold>() == null)
            {
                Adapters.NativeRuntime.Own(player.AddComponent<XbowHold>());
                Adapters.NativeRuntime.Own(player.AddComponent<XbowPlayerRig>());
            }
            if (player.GetComponent<GreataxeHold>() == null)
            {
                Adapters.NativeRuntime.Own(player.AddComponent<GreataxeHold>());
            }
        }
    }
}
