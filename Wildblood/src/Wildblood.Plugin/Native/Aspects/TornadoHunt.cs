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
    /// The live wave's tornadoes as the boss's owner moves them (<see cref="TornadoPath"/>). They stand still while
    /// they form, hunt from the moment they have formed, and stop when the wave's life is over or the boss falls.
    /// <see cref="SyncEvery"/> seconds apart the owner writes where they are into the boss's ZDO
    /// (<see cref="TornadoStore"/>). A machine that takes the boss over mid-wave - or meets a wave it did not raise -
    /// picks the hunt up from the last track, carried forward to now, so the tornadoes go on from where every client
    /// already sees them. Nothing is drawn here; the owner's own view reads the exact positions through
    /// <see cref="TryGet"/>. A hunting tornado tosses the Elder's roots it passes over (<see cref="TornadoToss"/>).
    /// </summary>
    internal sealed class TornadoHunt
    {
        /// <summary>Seconds between track writes: enough for others to follow a tornado that turns slowly.</summary>
        private const float SyncEvery = 0.25f;

        private readonly List<TornadoPath> _paths = new List<TornadoPath>();
        private readonly TornadoToss _toss = new TornadoToss();
        private TornadoWave? _wave;
        private long _seen;
        private float _sync;

        /// <summary>A wave this machine has just raised: each tornado starts where it rose, facing its player.</summary>
        public void Start(TornadoWave wave)
        {
            Forget();
            _seen = wave.At;
            for (int i = 0; i < wave.Count; i++)
            {
                _paths.Add(new TornadoPath(wave.Targets[i], wave.Spawns[i], Facing(wave.Targets[i], wave.Spawns[i])));
            }
            _wave = wave;
        }

        /// <summary>Each frame on the owner: moves a live wave and writes its track; picks up a wave it did not know.</summary>
        public void Tick(ZDO zdo, bool bossAlive)
        {
            long at = TornadoStore.At(zdo);
            if (at != _seen)
            {
                Seed(zdo, at);
            }
            if (_wave == null)
            {
                return;
            }
            long now = NetTime.NowMs();
            if (!bossAlive || now >= _wave.EndsAt)
            {
                Forget(); // over: the clients break the tornadoes up on the shared clock, or as the boss falls
                return;
            }
            if (now >= _wave.FormedAt)
            {
                Move(Time.deltaTime, _wave.Speed);
                _toss.Tick(_paths, _wave.Shape);
            }
            Sync(zdo, now);
        }

        private void Move(float dt, float speed)
        {
            foreach (TornadoPath path in _paths)
            {
                path.Step(dt, speed);
            }
        }

        private void Sync(ZDO zdo, long now)
        {
            _sync -= Time.deltaTime;
            if (_sync > 0f || _wave == null)
            {
                return;
            }
            _sync = SyncEvery;
            TornadoStore.WriteTrack(zdo, _wave.At, now, _paths, _wave.Speed);
        }

        // Taken over mid-wave, or a wave this machine never raised: carried on from the last word on where each one is.
        private void Seed(ZDO zdo, long at)
        {
            Forget();
            _seen = at;
            TornadoWave? wave = TornadoStore.Read(zdo);
            if (wave == null || NetTime.NowMs() >= wave.EndsAt)
            {
                return;
            }
            TornadoTrack? track = TornadoStore.ReadTrack(TornadoStore.TrackBytes(zdo));
            for (int i = 0; i < wave.Count; i++)
            {
                _paths.Add(Resume(wave, track, i));
            }
            _wave = wave;
        }

        private static TornadoPath Resume(TornadoWave wave, TornadoTrack? track, int index)
        {
            ZDOID target = wave.Targets[index];
            if (track != null && track.WaveAt == wave.At
                && track.TryPredict(index, NetTime.NowMs(), wave.EndsAt, out Vector3 at))
            {
                Vector3 velocity = track.Velocities[index];
                return new TornadoPath(target, at, velocity != Vector3.zero ? velocity : Facing(target, at));
            }
            return new TornadoPath(target, wave.Spawns[index], Facing(target, wave.Spawns[index]));
        }

        // Toward the player, where this machine sees them; straight ahead (north) should they not be here.
        private static Vector3 Facing(ZDOID target, Vector3 from)
        {
            GameObject? body = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(target) : null;
            Vector3 toward = body != null ? StormTargets.Flat(body.transform.position - from) : Vector3.zero;
            return toward != Vector3.zero ? toward : Vector3.forward;
        }

        /// <summary>Where tornado <paramref name="index"/> of the wave that rose at <paramref name="waveAt"/> is, if this
        /// machine is moving it.</summary>
        public bool TryGet(long waveAt, int index, out Vector3 at)
        {
            bool moving = _wave != null && _wave.At == waveAt && index >= 0 && index < _paths.Count;
            at = moving ? _paths[index].Position : Vector3.zero;
            return moving;
        }

        /// <summary>No longer this machine's to move (or over); a later wave or ownership picks it up afresh.</summary>
        public void Forget()
        {
            _wave = null;
            _paths.Clear();
            _toss.Clear();
            _sync = 0f;
        }

        /// <summary>No longer the owner: the next time it is, the wave in the ZDO is read again.</summary>
        public void Release()
        {
            Forget();
            _seen = 0L;
        }
    }
}
