// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/MineCallout.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Short words floating above a rock, drawn with the game's floating damage text (<see cref="FloatingText"/>), if the
    /// viewer's own Pickaxes "Show Callouts" is on.
    /// <list type="bullet">
    /// <item><see cref="ShowLocal"/>: the miner's own events ("Clean strike!", the Echo's distance), on the miner's client
    /// only.</item>
    /// <item><see cref="Broadcast"/>: events everybody near should see (finds), from any machine, the rock's owner
    /// included: a routed RPC to the players near the spot (<see cref="Keys.RpcMineCallout"/>, <see cref="NearbyRpc"/>); each client with a camera within
    /// <see cref="Range"/> of the spot draws it. A dedicated server draws nothing.</item>
    /// </list>
    /// Send the English text; it is shown as given. The routed RPC is registered in a ZNet.Awake postfix, where the game
    /// creates a fresh ZRoutedRpc for each session.
    /// </summary>
    public static class MineCallout
    {
        /// <summary>How far away a callout can be seen, in metres.</summary>
        public const float Range = FloatingText.Range;

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
        private static class NetAwake
        {
            [HarmonyPostfix]
            private static void Postfix() => ZRoutedRpc.instance?.Register<Vector3, string>(Keys.RpcMineCallout, Receive);
        }

        /// <summary>Shows <paramref name="text"/> at <paramref name="position"/> on this client only.</summary>
        public static void ShowLocal(Vector3 position, string text)
        {
            if (PickaxeSettings.ShowCallouts.Value)
                FloatingText.Show(position, SagaText.Resolve(text));
        }

        /// <summary>Shows <paramref name="text"/> at <paramref name="position"/> to every player near it, this one included.</summary>
        public static void Broadcast(Vector3 position, string text)
        {
            if (!string.IsNullOrEmpty(text))
                NearbyRpc.Send(position, Range, Keys.RpcMineCallout, position, text);
        }

        private static void Receive(long sender, Vector3 position, string text) =>
            HookGuard.Run("mine callout", () => ShowLocal(position, text));
    }
}
