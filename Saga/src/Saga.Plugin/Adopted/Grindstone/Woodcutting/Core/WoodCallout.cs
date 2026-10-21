// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Core/WoodCallout.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Short words floating above a tree or a log for everybody near it: "Timber!", "Clean split!", a find. The machine
    /// where it happened sends a routed RPC to the players near it (<see cref="Keys.RpcWoodCallout"/>, <see cref="NearbyRpc"/>); every client with a camera
    /// within <see cref="Range"/> of the spot, and its Woodcutting "Show Callouts" on, draws it with the game's own
    /// floating damage text (<see cref="FloatingText"/>). A dedicated server draws nothing. Send the English text; it is
    /// shown as given (GrindstoneSkills has no localization keys yet).
    /// </summary>
    public static class WoodCallout
    {
        /// <summary>How far away a callout can be seen, in metres.</summary>
        public const float Range = FloatingText.Range;

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
        private static class NetAwake
        {
            [HarmonyPostfix]
            private static void Postfix() => ZRoutedRpc.instance?.Register<Vector3, string>(Keys.RpcWoodCallout, Receive);
        }

        /// <summary>Shows <paramref name="text"/> at <paramref name="position"/> to every player near it, this one included.</summary>
        public static void Broadcast(Vector3 position, string text)
        {
            if (!string.IsNullOrEmpty(text))
                NearbyRpc.Send(position, FloatingText.Range, Keys.RpcWoodCallout, position, text);
        }

        private static void Receive(long sender, Vector3 position, string text) =>
            HookGuard.Run("wood callout", () => Show(position, text));

        private static void Show(Vector3 position, string text)
        {
            if (WoodcuttingSettings.ShowCallouts.Value)
                FloatingText.Show(position, SagaText.Resolve(text));
        }
    }
}
