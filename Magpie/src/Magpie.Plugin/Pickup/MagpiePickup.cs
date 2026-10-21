// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Threading;
using ExtraPotions.Magpie.StepAside;
using ExtraPotions.Meadworks.Crucible;
using Authority = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Magpie.Runtime.Pickup
{
    internal static class MagpiePickup
    {
        internal const string Capability = "meadworks.pickup";
        static int thread;
        internal static void Register(ProductRegistration registration)
        {
            thread = Thread.CurrentThread.ManagedThreadId;
            registration.AddCapability(Capability, new System.Version(1, 0),
                Tuple.Create<Func<object, float>, Func<object, object, bool>, Func<object, object, bool>>(Radius, Wanted, TryTake));
        }
        internal static void Reset() => thread = 0;
        sealed class Owner
        {
            internal Player Player = null!;
            internal ZNet Network = null!;
            internal long Generation, World;
            internal string Session = "";
            internal bool Current() => thread != 0 && thread == Thread.CurrentThread.ManagedThreadId && Plugin.AllowsUi &&
                MagpieRuntime.ModuleOn(MagpieModule.Pickup) && global::Player.m_enableAutoPickup &&
                Plugin.ToggleOwner(Player, Network, Generation, World, Session);
        }
        static Owner? Capture(object opaque)
        {
            if (thread == 0 || thread != Thread.CurrentThread.ManagedThreadId || !(opaque is Player player) || !Authority.IsReady) return null;
            var session = Authority.Session;
            var owner = new Owner { Player = player, Network = ZNet.instance, Generation = session.Generation,
                World = session.WorldId, Session = session.SessionId };
            return owner.Current() ? owner : null;
        }
        static float Radius(object opaque)
        {
            try
            {
                var owner = Capture(opaque); if (owner == null) return 0f;
                float radius = PickupEngine.Radius(owner.Player);
                return owner.Current() && !float.IsNaN(radius) && !float.IsInfinity(radius) && radius > 0f ? radius : 0f;
            }
            catch { return 0f; }
        }
        static bool Target(ItemDrop item)
        {
            if (item == null || item.m_nview == null || !item.m_nview.IsValid() || ZDOMan.instance == null) return false;
            var zdo = item.m_nview.GetZDO();
            return zdo != null && zdo.IsValid() && ReferenceEquals(ZDOMan.instance.GetZDO(zdo.m_uid), zdo);
        }
        static bool Wanted(object opaque, object target)
        {
            try
            {
                var owner = Capture(opaque);
                return owner != null && target is ItemDrop item && Target(item) && PickupEngine.Wanted(owner.Player, item) && owner.Current() && Target(item);
            }
            catch { return false; }
        }
        static bool TryTake(object opaque, object target)
        {
            try
            {
                var owner = Capture(opaque);
                return owner != null && target is ItemDrop item && Target(item) &&
                    PickupEngine.TryTakeExternal(owner.Player, item, () => owner.Current() && Target(item));
            }
            catch { return false; }
        }
    }
}
