// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Shepherd.Runtime.Breeding;
using ExtraPotions.Shepherd.Runtime.Integration;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Shepherd.Runtime.Pens
{
    internal sealed class LandPenRegistration : MonoBehaviour
    {
        static readonly HashSet<LandPenRegistration> instances = new HashSet<LandPenRegistration>();
        [SerializeField] internal Bounds LocalBox;
        ZNetView? view;
        IDisposable? lease;
        ZNet? network;
        long generation = -1;
        int capacity = -1;
        Vector3 scale;
        double next;
        bool warned;
        void Awake() { view = GetComponent<ZNetView>(); instances.Add(this); }
        void Update()
        {
            if (!LandPenPieces.Active || !NativeBreeding.HerdOn || view == null || !view.IsValid()) { Release(); return; }
            if (Time.realtimeSinceStartupAsDouble < next) return;
            next = Time.realtimeSinceStartupAsDouble + 0.5;
            int current = LandPenPieces.Capacity;
            long session = CruciblePlugin.Session.Generation;
            if (lease != null && capacity == current && generation == session && ReferenceEquals(network, ZNet.instance) && scale == transform.lossyScale) return;
            Release();
            try
            {
                lease = LivestockPens.RegisterPen(view, LocalBox, current);
                capacity = current; generation = session; network = ZNet.instance; scale = transform.lossyScale;
            }
            catch (Exception error) { if (!warned) LandPenPieces.Warn("land pen registration", error); warned = true; }
        }
        void Release()
        {
            try { lease?.Dispose(); } catch (Exception error) { if (!warned) LandPenPieces.Warn("land pen disposal", error); warned = true; }
            lease = null; network = null;
        }
        void OnDisable() => Release();
        void OnDestroy() { Release(); instances.Remove(this); }
        internal static void Clear() { foreach (var pen in new List<LandPenRegistration>(instances)) if (pen != null) pen.Release(); }
    }
}
