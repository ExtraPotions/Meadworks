// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGateSurface.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The client-side portal surface between the pillars of every gate loaded on this machine. Visual only:
    /// no collider, no ZDO, nothing networked; ship detection is geometric, in <see cref="ShipJump"/>. Each gate gets
    /// one <see cref="SeaGateSurfaceView"/>, built once when the gate appears in <see cref="SeaGateRegistry.Gates"/>
    /// (re-read a few times a second), faded in to full brightness (every gate reaches every other), faded out and
    /// destroyed when the gate is gone or a pillar unloads. Every frame only the fades and the sheet's
    /// shimmer move.</summary>
    public static class SeaGateSurface
    {
        private const float ScanSeconds = 0.25f;

        private static readonly Dictionary<long, SeaGateSurfaceView> live = new Dictionary<long, SeaGateSurfaceView>();
        private static readonly List<SeaGateSurfaceView> all = new List<SeaGateSurfaceView>();   // live and fading out
        private static readonly HashSet<long> seen = new HashSet<long>();
        private static readonly HashSet<long> broken = new HashSet<long>();   // gates whose surface failed to build
        private static readonly List<long> gone = new List<long>();
        private static float nextScan;

        /// <summary>Every frame on every machine with a local player (called by <see cref="SeaGates"/>).</summary>
        public static void Tick()
        {
            float time = Time.time;
            if (time >= nextScan)
            {
                nextScan = time + ScanSeconds;
                Scan();
            }
            Animate(Time.deltaTime, time);
        }

        /// <summary>Destroys every surface and the shared resources; on world unload.</summary>
        public static void Clear()
        {
            foreach (SeaGateSurfaceView view in all)
                view.Destroy();
            all.Clear();
            live.Clear();
            broken.Clear();
            nextScan = 0f;
            SeaGateSurfaceLook.Forget();
        }

        private static void Scan()
        {
            seen.Clear();
            foreach (LoadedGate gate in SeaGateRegistry.Gates)
            {
                if (gate.IsAlive && seen.Add(gate.Id))
                    Keep(gate);
            }
            gone.Clear();
            foreach (KeyValuePair<long, SeaGateSurfaceView> pair in live)
            {
                if (!seen.Contains(pair.Key))
                    gone.Add(pair.Key);
            }
            foreach (long id in gone)
                Retire(id);
        }

        /// <summary>Makes sure the gate has its surface, built for its current pillars, aiming at the right level.</summary>
        private static void Keep(LoadedGate gate)
        {
            if (live.TryGetValue(gate.Id, out SeaGateSurfaceView view) && !view.Matches(gate))
            {
                Retire(gate.Id);
                view = null;
            }
            if (view == null && !broken.Contains(gate.Id))
                view = Create(gate);
            view?.SetTarget(1f);
        }

        private static SeaGateSurfaceView Create(LoadedGate gate)
        {
            try
            {
                SeaGateSurfaceView view = SeaGateSurfaceView.Create(gate);
                live[gate.Id] = view;
                all.Add(view);
                return view;
            }
            catch (Exception e)
            {
                broken.Add(gate.Id);
                SeaGateLog.LogWarning($"Sea gate surface: could not build the surface of gate {gate.Id}: {e}");
                return null;
            }
        }

        /// <summary>Lets a surface fade out; it stays in <see cref="all"/> until it has finished.</summary>
        private static void Retire(long gateId)
        {
            if (!live.TryGetValue(gateId, out SeaGateSurfaceView view))
                return;
            view.SetTarget(0f);
            live.Remove(gateId);
        }

        private static void Animate(float deltaTime, float time)
        {
            for (int i = all.Count - 1; i >= 0; i--)
            {
                SeaGateSurfaceView view = all[i];
                view.Animate(deltaTime, time);
                if (!view.Finished)
                    continue;
                view.Destroy();
                all.RemoveAt(i);
            }
        }
    }

    /// <summary>A world unload (disconnect, quit to menu) destroys every surface and forgets the copied effect, so
    /// nothing from one world is drawn or reused in the next. Runs whatever the settings say: clearing is always safe.</summary>
    [HarmonyPatch(typeof(Game), "OnDestroy")]
    public static class SeaGateSurfaceTeardownPatch
    {
        [HarmonyPostfix]
        public static void Postfix() => SeaGateSurface.Clear();
    }
}
