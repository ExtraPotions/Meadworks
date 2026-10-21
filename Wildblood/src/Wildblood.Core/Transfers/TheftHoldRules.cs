// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// MeadWorks-original: what an inventory write does while Wildblood holds the inventory (playtest 2026-10-20, item safety).
// Same rules as Burrow's InventoryHoldRules, so a write gets one answer whichever product holds the inventory.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Wildblood.Transfers;

/// <summary>
/// What the guard does with one <c>Inventory</c> write:
/// <list type="bullet">
/// <item><see cref="Run"/>: not held, or <c>Load</c> (the authoritative state from a character load or the chest owner): runs.
/// That includes the <c>AddItem</c> calls the game makes inside <c>Inventory.Load</c> after it clears the list: refusing them
/// emptied a held inventory in memory (hotfix 2026-10-20, the same defect Burrow fixed for its Shared Materials holds).</item>
/// <item><see cref="Refuse"/>: methods that answer (bool or the added item) are skipped and answer false or null; nothing changed.</item>
/// <item><see cref="Defer"/>: void removals (<c>RemoveItem(name, ...)</c>, <c>RemoveAll</c>) cannot answer, so they are kept and
/// applied once the hold ends: the caller's payment still happens.</item>
/// <item><see cref="Neutral"/>: void moves (<c>MoveAll</c>, <c>MoveItemToThis</c> without an amount) are skipped on both sides together.</item>
/// <item><see cref="Skipped"/>: an earlier prefix (Burrow's guard applies the same rules) already refused, kept or skipped this
/// call. Wildblood changes nothing and keeps nothing, so a removal kept by Burrow is never applied twice.</item>
/// </list>
/// </summary>
public enum HeldWrite { Run, Refuse, Defer, Neutral, Skipped }

public static class TheftHoldRules
{
    /// <summary>The Inventory methods the guard patches. <c>Load</c> is deliberately absent: loading is never blocked.</summary>
    public static readonly string[] Guarded = { "AddItem", "RemoveItem", "RemoveAll", "RemoveOneItem", "RemoveItemByName", "MoveItemToThis", "MoveAll" };

    /// <summary>Most removals kept at once. Beyond it a void removal runs at once (logged once): the hold's commit then sees a
    /// changed inventory and waits for recovery, which never deducts twice or overwrites the change.</summary>
    public const int MaxDeferred = 256;

    public static HeldWrite Classify(string method, bool returnsVoid) => Classify(method, returnsVoid, false);

    /// <param name="insideLoad">The write is made by <c>Inventory.Load</c> of the same inventory.</param>
    public static HeldWrite Classify(string method, bool returnsVoid, bool insideLoad)
    {
        if (insideLoad || Array.IndexOf(Guarded, method) < 0)
            return HeldWrite.Run;
        if (!returnsVoid)
            return HeldWrite.Refuse;
        return method == "MoveAll" || method == "MoveItemToThis" ? HeldWrite.Neutral : HeldWrite.Defer;
    }

    /// <param name="runOriginal">Harmony's <c>__runOriginal</c>: false when an earlier prefix already skipped the call.</param>
    public static HeldWrite Decide(string method, bool returnsVoid, bool held, bool runOriginal) => Decide(method, returnsVoid, held, runOriginal, false);

    /// <param name="insideLoad">The write is made by <c>Inventory.Load</c> of the same inventory: it always runs.</param>
    public static HeldWrite Decide(string method, bool returnsVoid, bool held, bool runOriginal, bool insideLoad)
    {
        if (!runOriginal)
            return HeldWrite.Skipped;
        return held ? Classify(method, returnsVoid, insideLoad) : HeldWrite.Run;
    }

    /// <summary>
    /// Whether opening the theft journal must hold the player inventory before reconciliation. Only an intent that is not yet
    /// settled with a confirmed character receipt needs it; settled history alone (anyone who ever stole) holds nothing.
    /// </summary>
    public static bool HoldOnOpen(IEnumerable<bool> settledAndConfirmed) => settledAndConfirmed.Any(settled => !settled);
}

/// <summary>Void removals kept while their target is held, applied in call order per target once it is free.</summary>
public sealed class DeferredWrites<TTarget, TCall> where TTarget : class
{
    readonly List<(TTarget Target, TCall Call)> entries = new();
    readonly int limit;

    public DeferredWrites(int limit = TheftHoldRules.MaxDeferred) => this.limit = limit;

    public int Count => entries.Count;

    /// <summary>False when full: the caller then runs the write at once rather than lose it.</summary>
    public bool TryAdd(TTarget target, TCall call)
    {
        if (entries.Count >= limit)
            return false;
        entries.Add((target, call));
        return true;
    }

    /// <summary>
    /// Applies kept writes whose target is free, oldest first. Once a target is found held, its later writes wait too, so one
    /// target's writes never run out of order. Each entry is removed before it is applied, so it runs at most once even if
    /// applying it throws or keeps a new write.
    /// </summary>
    public int Drain(Func<TTarget, bool> held, Action<TTarget, TCall> apply)
    {
        if (entries.Count == 0)
            return 0;
        int applied = 0;
        var waiting = new HashSet<TTarget>(ReferenceComparer.Instance);
        for (int i = 0; i < entries.Count;)
        {
            var (target, call) = entries[i];
            if (waiting.Contains(target) || held(target))
            {
                waiting.Add(target);
                i++;
                continue;
            }
            entries.RemoveAt(i);
            applied++;
            apply(target, call);
        }
        return applied;
    }

    sealed class ReferenceComparer : IEqualityComparer<TTarget>
    {
        internal static readonly ReferenceComparer Instance = new();
        public bool Equals(TTarget? x, TTarget? y) => ReferenceEquals(x, y);
        public int GetHashCode(TTarget obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
