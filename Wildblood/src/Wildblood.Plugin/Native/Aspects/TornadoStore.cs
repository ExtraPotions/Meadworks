// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// A Nightfall boss's latest wave of tornadoes, in its ZDO, in two parts. The wave itself - when it rose, on the
    /// shared clock, the numbers it was raised with, the delay to the next and each tornado's player and spawn point -
    /// is written once, all at once, by the owner; every client takes it in when its time changes, a client that
    /// meets the boss mid-wave reads it the same way, and a new owner reads both when the last wave rose and how long
    /// it meant to wait. The track - where each tornado is and its velocity, stamped on the shared clock and tagged
    /// with its wave - is rewritten by the owner a few times a second while they hunt (<see cref="TornadoTrack"/>), so
    /// every other machine draws them where the owner moves them and a new owner carries the hunt on from there. The
    /// game replicates both with the boss, so the tornadoes need no message of their own. Only the codec lives here.
    /// </summary>
    internal static class TornadoStore
    {
        private static readonly int TornadoAtHash = TraitKeys.TornadoAt.GetStableHashCode();
        private static readonly int TornadoTrackHash = TraitKeys.TornadoTrack.GetStableHashCode();
        private static readonly int TornadoWaveHash = TraitKeys.TornadoWave.GetStableHashCode();

        /// <summary>A sanity cap on the tornadoes one wave carries, whatever the player count.</summary>
        private const int MaxTornadoes = 32;

        /// <summary>Eight numbers and the tornado count: nine 4-byte values.</summary>
        private const int Header = 36;

        /// <summary>Each tornado: its player (a long and a uint) and its spawn point (three floats).</summary>
        private const int PerTornado = 24;

        /// <summary>The track's wave and stamp (two longs) and its count.</summary>
        private const int TrackHeader = 20;

        /// <summary>Each tornado on the track: x, z and the velocity along each.</summary>
        private const int PerTrack = 16;

        /// <summary>When the latest wave rose, in shared-clock ms; 0 before the first.</summary>
        public static long At(ZDO zdo) => zdo.GetLong(TornadoAtHash);

        /// <summary>The latest track's packed bytes, so a reader can tell a fresh one from one it has read.</summary>
        public static byte[]? TrackBytes(ZDO zdo) => zdo.GetByteArray(TornadoTrackHash);

        /// <summary>Owner only: the wave, then the time every machine watches for a change.</summary>
        public static void Write(ZDO zdo, TornadoWave wave)
        {
            ZPackage pkg = new ZPackage();
            int count = Mathf.Min(wave.Count, MaxTornadoes);
            WriteNumbers(pkg, wave);
            pkg.Write(count);
            for (int i = 0; i < count; i++)
            {
                pkg.Write(wave.Targets[i]);
                pkg.Write(wave.Spawns[i]);
            }
            zdo.Set(TraitKeys.TornadoWave, pkg.GetArray());
            zdo.Set(TraitKeys.TornadoAt, wave.At);
        }

        private static void WriteNumbers(ZPackage pkg, TornadoWave wave)
        {
            pkg.Write(wave.Form);
            pkg.Write(wave.Life);
            pkg.Write(wave.Speed);
            pkg.Write(wave.Damage);
            pkg.Write(wave.Next);
            pkg.Write(wave.Shape.BaseRadius * 2f);
            pkg.Write(wave.Shape.TopRadius * 2f);
            pkg.Write(wave.Shape.Height);
        }

        /// <summary>The latest wave, with the moment it rose; null when there is none or the blob is unreadable.</summary>
        public static TornadoWave? Read(ZDO zdo)
        {
            byte[]? bytes = zdo.GetByteArray(TornadoWaveHash);
            long at = At(zdo);
            if (bytes == null || bytes.Length < Header || at <= 0L)
            {
                return null;
            }
            ZPackage pkg = new ZPackage(bytes);
            TornadoWave wave = ReadNumbers(pkg);
            wave.At = at;
            int count = Mathf.Clamp(pkg.ReadInt(), 0, Mathf.Min(MaxTornadoes, (bytes.Length - Header) / PerTornado));
            for (int i = 0; i < count; i++)
            {
                ZDOID target = pkg.ReadZDOID();
                wave.Add(target, pkg.ReadVector3());
            }
            return wave.Life > 0f ? wave : null;
        }

        private static TornadoWave ReadNumbers(ZPackage pkg)
        {
            TornadoWave wave = new TornadoWave
            {
                Form = pkg.ReadSingle(),
                Life = pkg.ReadSingle(),
                Speed = pkg.ReadSingle(),
                Damage = pkg.ReadSingle(),
                Next = pkg.ReadSingle(),
            };
            float baseWidth = pkg.ReadSingle();
            float topWidth = pkg.ReadSingle();
            wave.Shape = new TornadoShape(baseWidth, topWidth, pkg.ReadSingle());
            return wave;
        }

        /// <summary>Owner only: where the wave's tornadoes are now and how fast each moves which way.</summary>
        public static void WriteTrack(ZDO zdo, long waveAt, long stampMs, List<TornadoPath> paths, float speed)
        {
            ZPackage pkg = new ZPackage();
            int count = Mathf.Min(paths.Count, MaxTornadoes);
            pkg.Write(waveAt);
            pkg.Write(stampMs);
            pkg.Write(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = paths[i].Velocity(speed);
                pkg.Write(paths[i].Position.x);
                pkg.Write(paths[i].Position.z);
                pkg.Write(velocity.x);
                pkg.Write(velocity.z);
            }
            zdo.Set(TraitKeys.TornadoTrack, pkg.GetArray());
        }

        /// <summary>A packed track; null when there is none or it is unreadable.</summary>
        public static TornadoTrack? ReadTrack(byte[]? bytes)
        {
            if (bytes == null || bytes.Length < TrackHeader)
            {
                return null;
            }
            ZPackage pkg = new ZPackage(bytes);
            TornadoTrack track = new TornadoTrack { WaveAt = pkg.ReadLong(), Stamp = pkg.ReadLong() };
            int count = Mathf.Clamp(pkg.ReadInt(), 0, Mathf.Min(MaxTornadoes, (bytes.Length - TrackHeader) / PerTrack));
            for (int i = 0; i < count; i++)
            {
                Vector3 position = new Vector3(pkg.ReadSingle(), 0f, pkg.ReadSingle());
                track.Add(position, new Vector3(pkg.ReadSingle(), 0f, pkg.ReadSingle()));
            }
            return track;
        }
    }
}
