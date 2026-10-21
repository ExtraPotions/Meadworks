// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Core/FishCallout.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Short words floating above the water, drawn with the game's floating damage text (<see cref="FloatingText"/>), if
    /// the viewer's own Fishing "Show Callouts" is on, and announcements for the whole server.
    /// <list type="bullet">
    /// <item><see cref="ShowLocal"/>: the angler's own events ("Perfect strike!", "Spent!"), on the angler's client only.</item>
    /// <item><see cref="Broadcast"/>: events everybody near should see (a big one, a snag landed): a routed RPC to
    /// the players near it (<see cref="Keys.RpcFishCallout"/>, <see cref="NearbyRpc"/>); each client with a camera within <see cref="FloatingText.Range"/> of
    /// the spot draws it.</item>
    /// <item><see cref="Announce"/>: a line top left for every player on the server (<see cref="Keys.RpcFishAnnounce"/>),
    /// for a legendary catch. A dedicated server shows nothing.</item>
    /// </list>
    /// Send the English text; it is shown as given. The routed RPCs are registered in a ZNet.Awake postfix, where the game
    /// creates a fresh ZRoutedRpc for each session.
    /// </summary>
    public static class FishCallout
    {
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
        private static class NetAwake
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                ZRoutedRpc.instance?.Register<Vector3, string>(Keys.RpcFishCallout, Receive);
                ZRoutedRpc.instance?.Register<string>(Keys.RpcFishAnnounce, ReceiveAnnouncement);
            }
        }

        /// <summary>Shows <paramref name="text"/> at <paramref name="position"/> on this client only.</summary>
        public static void ShowLocal(Vector3 position, string text)
        {
            if (FishingSettings.ShowCallouts.Value)
                FloatingText.Show(position, SagaText.Resolve(text));
        }

        /// <summary>Shows <paramref name="text"/> at <paramref name="position"/> to every player near it, this one included.</summary>
        public static void Broadcast(Vector3 position, string text)
        {
            if (!string.IsNullOrEmpty(text))
                NearbyRpc.Send(position, FloatingText.Range, Keys.RpcFishCallout, position, text);
        }

        /// <summary>Shows <paramref name="text"/> top left to every player on the server, this one included.</summary>
        public static void Announce(string text)
        {
            if (ZRoutedRpc.instance != null && !string.IsNullOrEmpty(text))
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Keys.RpcFishAnnounce, text);
        }

        private static void Receive(long sender, Vector3 position, string text) =>
            HookGuard.Run("fish callout", () => ShowLocal(position, text));

        private static void ReceiveAnnouncement(long sender, string text) =>
            HookGuard.Run("fish announcement", () => Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, SagaText.Resolve(text)));
    }
}
