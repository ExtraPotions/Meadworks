// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Authority = ExtraPotions.Meadworks.Crucible.Plugin;
namespace Timber.Camera
{
    // A camera-step snapshot only; no sibling reference or retained registry delegate across steps.
    internal sealed class MagpiePickup
    {
        readonly Tuple<Func<object, float>, Func<object, object, bool>, Func<object, object, bool>> contract;
        readonly Player player;
        readonly ZNet network;
        readonly long generation, world;
        readonly string session;
        internal float Radius { get; private set; }
        MagpiePickup(Player player, Tuple<Func<object, float>, Func<object, object, bool>, Func<object, object, bool>> contract)
        {
            this.player = player; this.contract = contract; network = ZNet.instance;
            var current = Authority.Session;
            generation = current.Generation; world = current.WorldId; session = current.SessionId;
        }
        internal bool Current => Authority.IsReady && Authority.Session.AuthorityReady && world != 0 &&
            Authority.Session.Generation == generation && Authority.Session.WorldId == world && Authority.Session.SessionId == session &&
            network != null && ReferenceEquals(network, ZNet.instance) && ZNet.m_world != null && network.GetWorldUID() == world &&
            player != null && ReferenceEquals(player, Player.m_localPlayer) && player.m_nview != null && player.m_nview.IsValid() && player.m_nview.IsOwner() &&
            CameraState.IsOut(player) && CameraSettings.CameraPickup.Value && Player.m_enableAutoPickup && !player.IsDead() && !player.IsTeleporting();
        internal static MagpiePickup? Resolve(Player player)
        {
            try
            {
                if (!Authority.IsReady || !Authority.Services.Capabilities.TryResolve<Tuple<Func<object, float>, Func<object, object, bool>, Func<object, object, bool>>>(
                    "meadworks.pickup", new System.Version(1, 0), out var contract) || contract == null || contract.Item1 == null || contract.Item2 == null || contract.Item3 == null) return null;
                var adapter = new MagpiePickup(player, contract);
                if (!adapter.Current) return null;
                float radius = contract.Item1(player);
                if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f) return null;
                adapter.Radius = radius;
                return adapter;
            }
            catch { return null; }
        }
        internal bool Wanted(ItemDrop item)
        {
            try { return Current && contract.Item2(player, item) && Current; }
            catch { return false; }
        }
        internal bool TryTake(ItemDrop item)
        {
            try { return Current && contract.Item3(player, item); }
            catch { return false; }
        }
    }
}
