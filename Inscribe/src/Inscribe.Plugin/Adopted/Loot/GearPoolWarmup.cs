// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Builds the gear pool before a kill needs it (<see cref="GearPool"/>): once a second in a world, a pool found out
    /// of date (world loaded, rules changed, items or recipes registered late by other mods) whose inputs held still
    /// since the last check is collected again, <see cref="PerFrame"/> item prefabs a frame, and then adopted. A slice
    /// whose inputs changed under it starts over at the next check; a draw that came first built the pool itself.
    /// Every peer (a creature's or container's owner rolls the loot, a dedicated server included); nothing is sent.
    /// </summary>
    internal sealed class GearPoolWarmup : MonoBehaviour
    {
        private const float CheckSeconds = 1f;
        private const int PerFrame = 64;

        private float _nextCheck;
        private GearPoolKey? _seen;
        private GearPoolKey? _failed;
        private GearCollector? _job;
        private GearPoolKey _jobKey;
        private int _index;

        /// <summary>Loot area Init: one warm-up for the process.</summary>
        public static void Create()
        {
            GameObject host = new GameObject("ecf_gear_pool_warmup");
            global::ExtraPotions.Inscribe.Runtime.NativeGameplay.Own(host);
            DontDestroyOnLoad(host);
            host.AddComponent<GearPoolWarmup>();
        }

        // A failure is logged once per set of inputs and left to the draw's own build, never retried every second.
        private void Update()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                _job = null;
                _failed = GearPoolKey.Now();
                Log.Warn($"the gear drop pool could not be prepared ahead, the next drop builds it: {e}");
            }
        }

        private void Tick()
        {
            if (_job != null)
            {
                Step(_job);
                return;
            }
            if (Time.unscaledTime < _nextCheck)
            {
                return;
            }
            _nextCheck = Time.unscaledTime + CheckSeconds;
            Check();
        }

        // Out of date at two checks running with the same inputs (late registrations come in bursts): start collecting.
        private void Check()
        {
            GearPoolKey key = GearPoolKey.Now();
            bool settled = _seen.HasValue && _seen.Value.Equals(key);
            _seen = key;
            if (!settled || key.Db == null || ZNetScene.instance == null || GearPool.IsFresh(key)
                || (_failed.HasValue && _failed.Value.Equals(key)))
            {
                return;
            }
            _job = new GearCollector(ActiveRules.Current);
            _jobKey = key;
            _index = 0;
        }

        private void Step(GearCollector job)
        {
            GearPoolKey key = GearPoolKey.Now();
            if (!key.Equals(_jobKey) || GearPool.IsFresh(key))
            {
                _job = null;
                return;
            }
            List<GameObject> items = key.Db!.m_items;
            int end = Math.Min(items.Count, _index + PerFrame);
            for (; _index < end; _index++)
            {
                job.Add(items[_index]);
            }
            if (_index >= items.Count)
            {
                _job = null;
                GearPool.Adopt(key, job.Finish(), job.Rules);
            }
        }
    }
}
