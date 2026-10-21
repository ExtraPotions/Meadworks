// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Product ownership of components created by Edge's adopted native systems. Main thread only.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace ExtraPotions.Edge.Runtime.Secondaries.Native;

internal interface IEdgeNativeCancellation { void CancelForEdge(); }
// Only still-pending work owned by this exact attack is canceled. Released projectiles and applied hits survive.
internal interface IEdgeAttackCancellation { void CancelAttack(Attack attack); }

internal static class EdgeNativeOwnership
{
    private static readonly List<WeakReference<Component>> Components = new();
    internal static T AddOwnedEdgeComponent<T>(this GameObject target) where T : Component
    {
        var component = target.AddComponent<T>();
        if (typeof(T).Assembly == typeof(Plugin).Assembly)
        {
            Components.RemoveAll(reference => !reference.TryGetTarget(out var live) || live == null);
            Components.Add(new WeakReference<Component>(component));
        }
        return component;
    }

    internal static void CancelAttack(Attack attack)
    {
        foreach (var reference in Components.ToArray())
            if (reference.TryGetTarget(out var component) && component != null && component is IEdgeAttackCancellation pending)
                pending.CancelAttack(attack);
    }

    internal static void Dispose()
    {
        var owned = Components.ToArray();
        Components.Clear();
        foreach (var reference in owned)
        {
            if (!reference.TryGetTarget(out var component) || component == null) continue;
            if (component is Behaviour behaviour) behaviour.enabled = false;
            if (component is IEdgeNativeCancellation cancellation) cancellation.CancelForEdge();
            if (component == null) continue;
            // Standalone source scheduler roots contain no native actor. Actor components leave their actor intact.
            var root = component.gameObject;
            bool scheduler = root.name.StartsWith("SecondaryAttacks_", StringComparison.Ordinal) &&
                root.GetComponent<Character>() == null && root.GetComponent<Projectile>() == null &&
                root.GetComponent<ZNetView>() == null;
            UnityEngine.Object.Destroy(scheduler ? (UnityEngine.Object)root : component);
        }
    }
}
