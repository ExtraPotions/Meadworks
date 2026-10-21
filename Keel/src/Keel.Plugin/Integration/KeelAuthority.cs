// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Crucible's authority gate, answered once per frame (2026-10-09 static audit).
using System;
using ExtraPotions.Keel.Performance;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// MeadworksAuthority.Decide and ProductActive rebuild and sort the whole product list on every call. Keel asks them
    /// dozens of times per frame (every Tick, the ship panel, sail opacity, each pen, the helm patch on every physics step),
    /// so each answer is kept for the rest of the frame. A session change moves the stamp and ends it at once; a
    /// handshake that lands mid-frame is seen one frame later at worst.
    /// </summary>
    internal static class KeelAuthority
    {
        private readonly struct ProductState
        {
            public ProductState(bool active, ConfigBlockReason reason)
            {
                Active = active;
                Reason = reason;
            }

            public bool Active { get; }
            public ConfigBlockReason Reason { get; }
        }

        private static readonly FrameMemo<ProductState> product = new FrameMemo<ProductState>(() =>
        {
            var active = MeadworksAuthority.ProductActive(Plugin.Guid, out var reason);
            return new ProductState(active, reason);
        });

        private static readonly FrameMemo<AuthorityDecision> serverAuthoritative = new FrameMemo<AuthorityDecision>(() => MeadworksAuthority.Decide(Plugin.Guid, ConfigScope.ServerAuthoritative));
        private static readonly FrameMemo<AuthorityDecision> cheat = new FrameMemo<AuthorityDecision>(() => MeadworksAuthority.Decide(Plugin.Guid, ConfigScope.Cheat));

        /// <summary>Same answer as <see cref="MeadworksAuthority.ProductActive"/>, computed once per frame and session state.</summary>
        public static bool ProductActive(out ConfigBlockReason reason)
        {
            var state = product.Get(Time.frameCount, Stamp());
            reason = state.Reason;
            return state.Active;
        }

        /// <summary>Same answer as <c>MeadworksAuthority.Decide(Keel, scope)</c> for the two synchronized scopes, once per frame.</summary>
        public static AuthorityDecision Decide(ConfigScope scope)
        {
            switch (scope)
            {
                case ConfigScope.ServerAuthoritative: return serverAuthoritative.Get(Time.frameCount, Stamp());
                case ConfigScope.Cheat: return cheat.Get(Time.frameCount, Stamp());
                default: return MeadworksAuthority.Decide(Plugin.Guid, scope);
            }
        }

        /// <summary>The effective value of a setting under the authority gate, like MeadworksConfig.Effective with the kept decision.</summary>
        public static T Effective<T>(BepInEx.Configuration.ConfigEntry<T> entry, ConfigScope scope, T safe) =>
            ConfigValues.Resolve(Decide(scope), entry.Value, safe);

        /// <summary>Session generation, role and authority readiness; any change ends the kept answers at once.</summary>
        private static long Stamp()
        {
            if (!CruciblePlugin.IsReady) return -1;
            var session = CruciblePlugin.Session;
            return unchecked(session.Generation * 64 + (long)session.Role * 2 + (session.AuthorityReady ? 1 : 0));
        }

        public static void Reset()
        {
            product.Invalidate();
            serverAuthoritative.Invalidate();
            cheat.Invalidate();
        }
    }
}
