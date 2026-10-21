// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// A Stormbound storm as one client lives it. It watches the boss's ZDO for a new storm; when one arrives it draws
    /// every circle (<see cref="StormCircle"/>) for the time left before the lightning, and when that moment comes on
    /// the shared clock it strikes them all, thunders once and judges its own player (<see cref="StormHit"/>). A client
    /// that meets the boss mid-storm - a player arriving, or the boss coming into range - takes the storm in the same
    /// way and sees the rest of it; one with less than <see cref="MinWarning"/> left sits it out, so no one is struck
    /// by a circle they had no time to see. If the boss is dead when the moment comes the storm breaks and nothing
    /// falls; if this client lets the boss go, its circles go with it. Built only where there is a screen: never on a
    /// dedicated server.
    /// </summary>
    internal sealed class StormView
    {
        /// <summary>The least warning a client must have had to draw a storm and be struck by it.</summary>
        private const float MinWarning = 0.3f;

        private readonly ZNetView _view;
        private readonly Character _boss;
        private readonly List<StormCircle> _circles = new List<StormCircle>();
        private readonly List<Vector3> _centers = new List<Vector3>();
        private long _seen;
        private float _strikeTime;
        private float _radius;
        private float _damage;
        private bool _pending;

        public StormView(ZNetView view, Character boss)
        {
            _view = view;
            _boss = boss;
        }

        public void Tick()
        {
            if (_view == null || !_view.IsValid())
            {
                return;
            }
            long strikeAt = StormStore.StrikeAt(_view.GetZDO());
            if (strikeAt != _seen)
            {
                Take(strikeAt);
            }
            if (_pending && Time.time >= _strikeTime)
            {
                Strike();
            }
        }

        // A new storm: drawn for whatever is left of its warning, or sat out when too little is.
        private void Take(long strikeAt)
        {
            _seen = strikeAt;
            float left = -NetTime.SecondsSince(strikeAt);
            if (strikeAt <= 0L || left < MinWarning)
            {
                return;
            }
            Dispose();
            _centers.AddRange(StormStore.Read(_view.GetZDO(), out _radius, out _damage)); // the owner's numbers
            if (_radius <= 0f)
            {
                _centers.Clear(); // an unreadable storm: nothing drawn, nothing judged
            }
            foreach (Vector3 center in _centers)
            {
                _circles.Add(StormCircle.Draw(center, _radius, left));
            }
            _strikeTime = Time.time + left;
            _pending = _centers.Count > 0;
        }

        private void Strike()
        {
            _pending = false;
            // Only the owner marks a character dead; a client sees it as the replicated health reaching zero.
            if (_boss == null || _boss.IsDead() || _boss.GetHealth() <= 0f)
            {
                Dispose(); // the boss fell before its lightning did: the storm breaks
                return;
            }
            foreach (StormCircle circle in _circles)
            {
                if (circle != null)
                {
                    circle.Strike();
                }
            }
            _circles.Clear();
            StormEffects.Thunder(Nearest());
            StormHit.Judge(_boss, _centers, _radius, _damage);
            _centers.Clear();
        }

        /// <summary>The thunder rolls from the circle nearest this player (the first, with none).</summary>
        private Vector3 Nearest()
        {
            Player local = Player.m_localPlayer;
            Vector3 best = _centers[0];
            foreach (Vector3 center in _centers)
            {
                if (local != null && (center - local.transform.position).sqrMagnitude
                    < (best - local.transform.position).sqrMagnitude)
                {
                    best = center;
                }
            }
            return best;
        }

        /// <summary>Any circles still up go at once, and nothing is pending.</summary>
        public void Dispose()
        {
            foreach (StormCircle circle in _circles)
            {
                if (circle != null)
                {
                    circle.Dismiss();
                }
            }
            _circles.Clear();
            _centers.Clear();
            _pending = false;
        }
    }
}
