// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Echoing's recording of its boss, on the boss's owner: where it stood, how it faced and looked, what it aimed at and
    /// how its animator stood, one sample every physics step, and what it did - the attacks it started and the animation
    /// cues it was sent - as timed events. It keeps a little more than the delay, the oldest sample overwritten first, so
    /// the echo can be shown the boss of exactly `delay` seconds ago (<see cref="EchoDriver"/>). It lives only in the
    /// owner's memory: a new owner starts a fresh tape, and the echo waits until that covers the delay again.
    /// </summary>
    internal sealed class EchoTape
    {
        /// <summary>The shortest and longest `delay` a rule file can set, in seconds.</summary>
        private const float MinDelay = 1f;
        private const float MaxDelay = 60f;

        /// <summary>Seconds of samples kept beyond the delay, so a slow physics step never leaves the replay without one.</summary>
        private const float Slack = 1f;

        private readonly float[] _times;
        private readonly EchoPose[] _poses;
        private readonly float[][] _params;
        private readonly Queue<EchoEvent> _events = new Queue<EchoEvent>();
        private int _head;
        private int _count;

        public EchoTape(int capacity, int paramCount)
        {
            _times = new float[capacity];
            _poses = new EchoPose[capacity];
            _params = new float[capacity][];
            for (int i = 0; i < capacity; i++)
            {
                _params[i] = new float[paramCount];
            }
        }

        /// <summary>How far behind its boss the echo runs, from the rules, held to a sane range.</summary>
        public static float Delay() => Mathf.Clamp(AspectMath.Power(Aspect.Echoing, Fields.Delay), MinDelay, MaxDelay);

        /// <summary>Samples needed to hold <paramref name="delay"/> seconds at the physics rate, with the slack.</summary>
        public static int CapacityFor(float delay) =>
            Mathf.CeilToInt((delay + Slack) / Mathf.Max(Time.fixedDeltaTime, 0.005f)) + 1;

        public int Capacity => _times.Length;

        /// <summary>True once the tape reaches back to <paramref name="time"/>.</summary>
        public bool Covers(float time) => _count > 0 && _times[At(0)] <= time;

        /// <summary>The slot the next sample's animator values are read into, before <see cref="Commit"/>.</summary>
        public float[] NextParams => _params[_head];

        public void Commit(float time, EchoPose pose)
        {
            _times[_head] = time;
            _poses[_head] = pose;
            _head = (_head + 1) % _times.Length;
            if (_count < _times.Length)
            {
                _count++;
            }
            TrimEvents();
        }

        public void Add(EchoEvent echoEvent) => _events.Enqueue(echoEvent);

        /// <summary>The next event due by <paramref name="time"/>, oldest first; false when none is.</summary>
        public bool TryTake(float time, out EchoEvent echoEvent)
        {
            if (_events.Count > 0 && _events.Peek().Time <= time)
            {
                echoEvent = _events.Dequeue();
                return true;
            }
            echoEvent = null!;
            return false;
        }

        /// <summary>
        /// The boss as it was at <paramref name="time"/>: its pose blended between the two samples around it, and the
        /// animator values of the one before. False when the tape does not reach back that far.
        /// </summary>
        public bool TryRead(float time, out EchoPose pose, out float[] values)
        {
            int k = LatestAtOrBefore(time);
            if (k < 0)
            {
                pose = default;
                values = null!;
                return false;
            }
            int a = At(k);
            int b = k + 1 < _count ? At(k + 1) : a;
            float span = _times[b] - _times[a];
            float share = span > 0f ? Mathf.Clamp01((time - _times[a]) / span) : 0f;
            pose = EchoPose.Blend(_poses[a], _poses[b], share);
            values = _params[a];
            return true;
        }

        /// <summary>The ring slot of the k-th sample, oldest first.</summary>
        private int At(int k) => (_head - _count + k + _times.Length * 2) % _times.Length;

        /// <summary>The index (oldest first) of the last sample taken at or before <paramref name="time"/>; -1 for none.</summary>
        private int LatestAtOrBefore(float time)
        {
            int low = 0;
            int high = _count - 1;
            int found = -1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (_times[At(mid)] <= time)
                {
                    found = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }
            return found;
        }

        /// <summary>
        /// Events older than every sample are past replaying: dropped, so a tape whose echo never comes (a prefab another
        /// mod removed) never grows without end.
        /// </summary>
        private void TrimEvents()
        {
            float oldest = _times[At(0)];
            while (_events.Count > 0 && _events.Peek().Time < oldest - Slack)
            {
                _events.Dequeue();
            }
        }
    }
}
