// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// A Portalbound throw as one client sees it. It watches the boss's ZDO (<see cref="PortalStore"/>): when a far
    /// portal opens it draws it where the owner put it, facing the target, with the portal-opening sound; when the
    /// throw is let go it opens the second portal on the hand the boss throws with (<see cref="PortalAttacks"/>) and
    /// turns the far one to where the throw is aimed; when the closing time comes on the shared clock - or the boss
    /// falls - both close. A client that meets the boss mid-throw draws the rest of it the same way; one that meets it
    /// after the closing time draws nothing. The throw itself (vines, slime) is the game's own networked projectiles, seen by
    /// everyone as they are. Built only where there is a screen: never on a dedicated server.
    /// </summary>
    internal sealed class PortalView
    {
        /// <summary>The far portal's size against the wooden portal's own swirl (2.6 m across): about 2.3 m.</summary>
        private const float FarSize = 0.9f;

        /// <summary>The portal at the hand, a little smaller: about 1.8 m.</summary>
        private const float HandSize = 0.7f;

        private readonly ZNetView _view;
        private readonly Character _boss;
        private readonly string _prefab;
        private PortalRift? _far;
        private PortalRift? _hand;
        private long _seenAt;
        private long _seenFire;

        public PortalView(ZNetView view, Character boss, string prefab)
        {
            _view = view;
            _boss = boss;
            _prefab = prefab;
        }

        public void Tick()
        {
            if (_view == null || !_view.IsValid())
            {
                return;
            }
            ZDO zdo = _view.GetZDO();
            long openedAt = PortalStore.OpenedAt(zdo);
            if (openedAt != _seenAt)
            {
                Take(zdo, openedAt);
            }
            if (_far != null)
            {
                Follow(zdo, openedAt);
            }
        }

        // An open throw: closed when its time comes, and the hand's portal opened when the throw is let go.
        private void Follow(ZDO zdo, long openedAt)
        {
            if (Closing(zdo))
            {
                Dispose();
                return;
            }
            long firedAt = PortalStore.FiredAt(zdo);
            if (firedAt > 0L && firedAt >= openedAt && firedAt != _seenFire)
            {
                Release(zdo, firedAt);
            }
        }

        // A new far portal: drawn unless its throw is already over here.
        private void Take(ZDO zdo, long openedAt)
        {
            _seenAt = openedAt;
            _seenFire = 0L;
            Dispose();
            if (openedAt <= 0L || Closing(zdo))
            {
                return;
            }
            Vector3 spot = PortalStore.Spot(zdo);
            _far = PortalRift.Open(spot, PortalStore.Aim(zdo) - spot, FarSize);
            PortalEffects.Opened(spot);
        }

        // The throw is let go: the far portal turns to it and the portal at the hand opens.
        private void Release(ZDO zdo, long firedAt)
        {
            _seenFire = firedAt;
            if (_far != null)
            {
                _far.Face(PortalStore.Aim(zdo) - PortalStore.Spot(zdo));
            }
            Transform? hand = PortalAttacks.ThrowingHand(_boss, _prefab);
            if (hand == null)
            {
                return; // a boss with no hand to open on: the far portal alone
            }
            _hand = PortalRift.OnHand(hand, _boss.transform, HandSize);
            PortalEffects.Opened(hand.position);
        }

        // The owner's closing time has come, or the boss has fallen (a client sees that as its health reaching zero).
        private bool Closing(ZDO zdo) =>
            NetTime.NowMs() >= PortalStore.ShutAt(zdo) || _boss == null || _boss.IsDead() || _boss.GetHealth() <= 0f;

        /// <summary>Any portals still open close now.</summary>
        public void Dispose()
        {
            if (_far != null)
            {
                _far.Close();
            }
            if (_hand != null)
            {
                _hand.Close();
            }
            _far = null;
            _hand = null;
        }
    }
}
