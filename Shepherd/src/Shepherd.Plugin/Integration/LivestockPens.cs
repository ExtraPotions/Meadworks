// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Product-owned live pen registry; uses Crucible's existing versioned capability registration.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using ExtraPotions.Shepherd.Runtime.Breeding;
using UnityEngine;
using PensContract = System.Tuple<System.Func<ZNetView, UnityEngine.Bounds, int, System.IDisposable>,
    System.Func<UnityEngine.Vector3, System.Tuple<ZNetView, int, bool>>>;

namespace ExtraPotions.Shepherd.Runtime.Integration
{
    public static class LivestockPens
    {
        public const string Id = "meadworks.livestock.pens";
        static readonly List<Pen> pens = new List<Pen>();
        static readonly int thread = Thread.CurrentThread.ManagedThreadId;
        [ThreadStatic] static Procreation? counting;
        public static void Register(ProductRegistration registration) => registration.AddCapability<PensContract>(Id,
            new System.Version(1, 0), Tuple.Create<Func<ZNetView,Bounds,int,IDisposable>,Func<Vector3,Tuple<ZNetView,int,bool>>>(RegisterPen, PenOf));
        static void MainThread() { if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Pens require the native main thread."); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static void Prune() => pens.RemoveAll(p => p.Disposed || !p.Valid);
        public static IDisposable RegisterPen(ZNetView view, Bounds box, int herdLimit)
        {
            MainThread(); Prune();
            if (!Plugin.Ready || view == null || !view.IsValid() || ZNet.instance == null || !Finite(box.center) || !Finite(box.size)
                || box.size.x <= 0 || box.size.y <= 0 || box.size.z <= 0 || box.size.sqrMagnitude > 16384
                || herdLimit < 0 || herdLimit > 1000 || pens.Count >= 256 || pens.Any(p => p.View == view))
                throw new ArgumentException("Invalid or duplicate pen registration.");
            var pen = new Pen(view, box, herdLimit, ZNet.instance); pens.Add(pen); return pen;
        }
        static Pen? Find(Vector3 position)
        {
            MainThread(); Prune();
            if (!Plugin.Ready || !NativeBreeding.HerdOn || !Finite(position)) return null;
            return pens.Where(p => p.Contains(position)).OrderBy(p => p.Box.size.x*p.Box.size.y*p.Box.size.z)
                .ThenBy(p => p.View.GetZDO().m_uid.ToString(), StringComparer.Ordinal).FirstOrDefault();
        }
        public static Tuple<ZNetView,int,bool> PenOf(Vector3 position)
        {
            var pen = Find(position); return pen == null ? null! : Tuple.Create(pen.View, pen.Limit, pen.Moving);
        }
        internal static int Maximum(Procreation parent, int fallback) => ShepherdSettings.PenMode.Value
            ? ExtraPotions.Shepherd.Pens.PenCapacity.Maximum(fallback, Find(parent.transform.position)?.Limit) : fallback;
        internal static bool HoldsMoving(Procreation parent) => NativeBreeding.HerdOn && ShepherdSettings.PauseMovingShips.Value
            && Find(parent.transform.position)?.Moving == true;
        internal static int? Count(GameObject prefab, Vector3 position)
        {
            if (!ShepherdSettings.PenMode.Value || prefab == null) return null;
            var pen = Find(position); if (pen == null) return null;
            var name = global::Utils.GetPrefabName(prefab);
            if (prefab.GetComponent<BaseAI>() != null)
                return BaseAI.BaseAIInstances.Count(ai => ai != null && global::Utils.GetPrefabName(ai.gameObject) == name && pen.Contains(ai.transform.position));
            return GameObject.FindGameObjectsWithTag("spawned").Count(item => item != null && global::Utils.GetPrefabName(item) == name && pen.Contains(item.transform.position));
        }
        internal static IDisposable EnterCount(Procreation parent) => new CountScope(parent);
        internal static bool TryNativeCount(GameObject prefab, Vector3 position, float range, bool eventOnly, bool procreationOnly, out int count)
        {
            count = 0; var parent = counting;
            if (parent == null || !NativeBreeding.HerdOn || eventOnly || procreationOnly || range != parent.m_totalCheckRange
                || (position-parent.transform.position).sqrMagnitude > .0001f
                || (prefab != parent.m_myPrefab && prefab != parent.m_offspringPrefab)) return false;
            var result = Count(prefab, position); if (!result.HasValue) return false; count=result.Value; return true;
        }
        public static void Clear() { foreach (var pen in pens) pen.Disposed=true; pens.Clear(); counting=null; }
        sealed class CountScope : IDisposable
        {
            readonly Procreation parent; readonly Procreation? previous; bool disposed;
            internal CountScope(Procreation parent) { this.parent=parent;previous=counting;counting=parent; }
            public void Dispose() { if (disposed) return;disposed=true;if (counting==parent) counting=previous; }
        }
        sealed class Pen : IDisposable
        {
            internal readonly ZNetView View; internal readonly Bounds Box; internal readonly int Limit; readonly ZNet network;
            internal bool Disposed;
            internal Pen(ZNetView view,Bounds box,int limit,ZNet network) { View=view;Box=box;Limit=limit;this.network=network; }
            internal bool Valid => View != null && View.IsValid() && ReferenceEquals(ZNet.instance,network);
            internal bool Contains(Vector3 position) => Valid && Box.Contains(View.transform.InverseTransformPoint(position));
            internal bool Moving { get { var ship=View.GetComponent<Ship>(); return ship != null && ship.m_body != null && ship.m_body.linearVelocity.sqrMagnitude > 1f; } }
            public void Dispose() { MainThread(); if (Disposed)return;Disposed=true;pens.Remove(this); }
        }
    }
}
