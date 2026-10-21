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
    /// A Nightfall boss's tornadoes as one client lives them. It watches the boss's ZDO for a new wave; when one arrives
    /// it raises every tornado (<see cref="TornadoVisual"/>) where the owner put it, and each frame tells each one where
    /// it should be: exactly where this machine is moving it, if it owns the boss (<see cref="TornadoHunt"/>), otherwise
    /// the owner's latest track carried forward (<see cref="TornadoTrack"/>). Once they have formed it judges its own
    /// player against them as drawn here, four times a second (<see cref="TornadoHit"/>). A client that meets the boss
    /// mid-wave raises the wave's tornadoes where they are by then, already formed if their forming is over. When the
    /// wave's life ends on the shared clock, or the boss falls, every tornado breaks up; if this client lets the boss
    /// go, they break up with it. Built only where there is a screen: never on a dedicated server.
    /// </summary>
    internal sealed class TornadoView
    {
        private readonly ZNetView _view;
        private readonly Character _boss;
        private readonly TornadoHunt _hunt;
        private readonly List<Raised> _raised = new List<Raised>();
        private readonly List<Vector3> _feet = new List<Vector3>();
        private TornadoWave? _wave;
        private TornadoTrack? _track;
        private byte[]? _trackBytes;
        private long _seen;
        private float _nextTick;

        public TornadoView(ZNetView view, Character boss, TornadoHunt hunt)
        {
            _view = view;
            _boss = boss;
            _hunt = hunt;
        }

        public void Tick()
        {
            if (_view == null || !_view.IsValid())
            {
                return;
            }
            ZDO zdo = _view.GetZDO();
            long at = TornadoStore.At(zdo);
            if (at != _seen)
            {
                Take(zdo, at);
            }
            long now = NetTime.NowMs();
            if (_wave != null && (now >= _wave.EndsAt || BossDown()))
            {
                Dispose(); // their time is up, or the boss fell: the storm breaks
            }
            if (_wave != null)
            {
                Follow(zdo, _wave, now);
                Judge(_wave, now);
            }
        }

        // A new wave: its tornadoes raised where they are by now, unless it is over or its boss already down.
        private void Take(ZDO zdo, long at)
        {
            _seen = at;
            Dispose();
            TornadoWave? wave = TornadoStore.Read(zdo);
            long now = NetTime.NowMs();
            if (wave == null || now >= wave.EndsAt || BossDown())
            {
                return;
            }
            _wave = wave;
            Refresh(zdo);
            for (int i = 0; i < wave.Count; i++)
            {
                Vector3 start = Where(wave, i, now);
                start.y = wave.Spawns[i].y; // the ground is found from here, or kept here until it is
                TornadoVisual tornado = TornadoPool.Raise(start, wave.Shape, wave.Form, wave.Age(now));
                _raised.Add(new Raised(tornado, tornado.Lease));
            }
        }

        private void Follow(ZDO zdo, TornadoWave wave, long now)
        {
            Refresh(zdo);
            float age = wave.Age(now);
            for (int i = 0; i < _raised.Count; i++)
            {
                if (_raised[i].Tornado != null)
                {
                    _raised[i].Tornado.Follow(_raised[i].Lease, Where(wave, i, now), age);
                }
            }
        }

        // The owner's latest track, read again only when a new one has arrived.
        private void Refresh(ZDO zdo)
        {
            byte[]? bytes = TornadoStore.TrackBytes(zdo);
            if (!ReferenceEquals(bytes, _trackBytes))
            {
                _trackBytes = bytes;
                _track = TornadoStore.ReadTrack(bytes);
            }
        }

        private Vector3 Where(TornadoWave wave, int index, long now)
        {
            if (_hunt.TryGet(wave.At, index, out Vector3 at))
            {
                return at; // this machine moves it: exactly where it is
            }
            if (_track != null && _track.WaveAt == wave.At && _track.TryPredict(index, now, wave.EndsAt, out at))
            {
                return at;
            }
            return wave.Spawns[index]; // no word yet: still where it rose
        }

        // Four times a second once formed: is this player inside one of the funnels as drawn here?
        private void Judge(TornadoWave wave, long now)
        {
            if (now < wave.FormedAt || Time.time < _nextTick)
            {
                return;
            }
            _nextTick = Time.time + TornadoHit.TickSeconds;
            _feet.Clear();
            foreach (Raised raised in _raised)
            {
                if (raised.Tornado != null && raised.Tornado.Lease == raised.Lease)
                {
                    _feet.Add(raised.Tornado.Foot);
                }
            }
            TornadoHit.Judge(_boss, _feet, wave.Shape, wave.Damage * TornadoHit.TickSeconds);
        }

        // Only the owner marks a character dead; a client sees it as the replicated health reaching zero.
        private bool BossDown() => _boss == null || _boss.IsDead() || _boss.GetHealth() <= 0f;

        /// <summary>Every tornado still up breaks up, and nothing more is judged.</summary>
        public void Dispose()
        {
            foreach (Raised raised in _raised)
            {
                if (raised.Tornado != null)
                {
                    raised.Tornado.Dissipate(raised.Lease);
                }
            }
            _raised.Clear();
            _wave = null;
            _track = null;
            _trackBytes = null;
        }

        /// <summary>A tornado this view raised, with the lease it raised it under.</summary>
        private readonly struct Raised
        {
            public readonly TornadoVisual Tornado;
            public readonly int Lease;

            public Raised(TornadoVisual tornado, int lease)
            {
                Tornado = tornado;
                Lease = lease;
            }
        }
    }
}
