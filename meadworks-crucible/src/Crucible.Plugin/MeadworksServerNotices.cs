// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Native Valheim signatures were inspected, not copied.
using System;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Native HUD placement understood by installed and unmodified game clients.</summary>
    public enum ServerNoticePlacement { TopLeft, Center }

    /// <summary>
    /// Server-authored literal notices through Valheim's existing ShowMessage receiver. Main thread only.
    /// This is a delivery utility, not an admin command or authorization grant: callers must authorize their action first.
    /// A successful send means the native route accepted the notice, not that a remote player read or displayed it.
    /// No custom receiver, client dependency, persistent state or retry queue is created.
    /// </summary>
    public static class MeadworksServerNotices {
        public const int MaximumTextLength = 2048;

        /// <summary>
        /// Sends already formatted text to one connected ready peer. A local host's routed peer ID is also accepted.
        /// Returns false outside a current authoritative world, for an unregistered source, invalid text or stale peer.
        /// Pass literal text suitable for vanilla clients, not an unresolved product localization key.
        /// </summary>
        public static bool TrySend(string sourceProductId, long targetPeerId, string text,
            ServerNoticePlacement placement = ServerNoticePlacement.Center) {
            if (!CanSend(sourceProductId, text, placement)) return false;
            var net = ZNet.instance;
            var rpc = ZRoutedRpc.instance;
            if (net == null || rpc == null || !net.IsServer()) return false;
            if (targetPeerId == ZRoutedRpc.Everybody) return false; // use the explicit broadcast API
            if (targetPeerId == ZNet.GetUID()) {
                if (!MeadworksHeadless.AllowsUi || MessageHud.instance == null) return false;
            } else if (!net.GetPeers().Any(peer => peer.m_uid == targetPeerId && peer.IsReady()
                && peer.m_rpc != null && peer.m_rpc.IsConnected())) return false;
            return Route(rpc, targetPeerId, text, placement);
        }

        /// <summary>
        /// Sends already formatted text to all current native recipients, including the graphical host.
        /// A dedicated server needs no MessageHud. True means routing was accepted, not a delivery acknowledgement.
        /// </summary>
        public static bool TryBroadcast(string sourceProductId, string text,
            ServerNoticePlacement placement = ServerNoticePlacement.Center) {
            if (!CanSend(sourceProductId, text, placement)) return false;
            var net = ZNet.instance;
            var rpc = ZRoutedRpc.instance;
            return net != null && rpc != null && net.IsServer() && Route(rpc, ZRoutedRpc.Everybody, text, placement);
        }

        private static bool CanSend(string sourceProductId, string text, ServerNoticePlacement placement) {
            if (!Plugin.IsReady || !Plugin.Session.IsAuthority || !Plugin.Session.AuthorityReady
                || string.IsNullOrWhiteSpace(sourceProductId) || string.IsNullOrWhiteSpace(text)
                || text.Length > MaximumTextLength || text.IndexOf('\0') >= 0
                || (placement != ServerNoticePlacement.TopLeft && placement != ServerNoticePlacement.Center)) return false;
            return Plugin.Services.Snapshot().Any(product => string.Equals(product.Id, sourceProductId, StringComparison.Ordinal));
        }

        private static bool Route(ZRoutedRpc rpc, long target, string text, ServerNoticePlacement placement) {
            try {
                var nativeType = placement == ServerNoticePlacement.Center ? MessageHud.MessageType.Center : MessageHud.MessageType.TopLeft;
                rpc.InvokeRoutedRPC(target, "ShowMessage", (int)nativeType, text);
                return true;
            } catch (Exception error) {
                Plugin.Log?.LogWarning("Native server notice routing failed: " + error.GetType().Name);
                return false;
            }
        }
    }
}
