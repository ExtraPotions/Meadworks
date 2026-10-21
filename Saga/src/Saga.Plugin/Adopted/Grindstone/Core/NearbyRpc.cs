// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/NearbyRpc.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A routed RPC for something only players near a spot can see (a callout), sent to their machines alone rather than
    /// to everybody on the server. The players are the server's player list, which every client has; each one's
    /// position is their character ZDO's, on whichever machine has that ZDO (every machine near them), and the RPC goes
    /// to that ZDO's owner (a player always owns their own). A player whose ZDO this machine does not have is far away.
    /// The sending machine always gets it too, at once and without a send. Players up to <see cref="Margin"/> past the
    /// range are included: the receiver tests its camera, which can sit away from the character.
    /// </summary>
    public static class NearbyRpc
    {
        private const float Margin = 20f;

        public static void Send(Vector3 position, float range, string method, params object[] parameters)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null)
                return;
            long self = ZNet.GetUID();
            rpc.InvokeRoutedRPC(self, method, parameters);
            if (ZNet.instance == null || ZDOMan.instance == null)
                return;
            float reach = (range + Margin) * (range + Margin);
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                long peer = PeerNear(info.m_characterID, position, reach);
                if (peer != 0L && peer != self)
                    rpc.InvokeRoutedRPC(peer, method, parameters);
            }
        }

        /// <summary>The owner of the character's ZDO when it is within the squared reach of the spot; 0 otherwise.</summary>
        private static long PeerNear(ZDOID character, Vector3 position, float reach)
        {
            ZDO zdo = character.IsNone() ? null : ZDOMan.instance.GetZDO(character);
            return zdo != null && (zdo.GetPosition() - position).sqrMagnitude <= reach ? zdo.GetOwner() : 0L;
        }
    }
}
