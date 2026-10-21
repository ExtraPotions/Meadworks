// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; receipted per-entry drop decisions.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Transfers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Gives a pouch back: every item it holds drops at a position, exactly as it was taken - never re-made, never
    /// multiplied. Shared by every removal path the spec requires it for: a normal death (<see cref="Patches.DeathPatch"/>),
    /// `wildblood purge` (the one documented exception to "purge drops nothing"), and a game-driven despawn
    /// (<see cref="Patches.ThievingDespawnPatch"/>).
    /// <para>Each entry is decided once through <see cref="PouchDropOwner"/>: a durable mark before the native drop is
    /// created and the created identity after. A repeated death/despawn, an owner hand-over (the creature's replicated
    /// drop marker) or a crash between the two steps never creates a second copy; such an entry stays UNKNOWN, with its
    /// exact item bytes retained in the ledger.</para>
    /// </summary>
    public static class PouchDrop
    {
        // The same spread idiom Splitter.cs already uses for placing a splinter copy near its parent - one horizontal
        // jitter constant for "scatter near a point" in this codebase, rather than a second one invented here.
        private const float Spread = 0.5f;

        /// <summary>Drops every undecided entry. <paramref name="live"/> is the thief's still-valid owned ZDO (despawn,
        /// purge) or null once the death has reset it. <paramref name="lastChance"/>: the creature is gone whatever happens
        /// (death), so an entry that cannot be durably marked still drops once, unreceipted, rather than vanish. Returns
        /// false when an entry was neither created nor durably decided - the caller must then keep the creature.</summary>
        public static bool DropAll(List<PouchStore.Entry> entries, Vector3 pos, ZDOID source, ZDO? live, bool lastChance)
        {
            if (entries.Count == 0)
            {
                return true;
            }
            PouchDropLedger? ledger = PouchDropOwner.Ledger();
            var keys = entries.Select(e => PouchDropKeys.Entry(source.UserID, source.ID, e.StealerId, e.StealCounter, e.Root ?? "", PouchStore.ItemBytes(e))).ToList();
            Func<string, bool> marked = ReadMarker(live);
            if (ledger == null)
            {
                return Unreceipted(entries, keys, marked, pos, lastChance);
            }
            bool resolved = true;
            var decisions = new List<PouchDropDecision>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                decisions.Add(ledger.Begin(keys[i], marked(keys[i]), PouchStore.ItemBytes(entries[i])));
            }
            // Replicate the decided keys before any native creation, so a later owner of a surviving creature refuses them.
            WriteMarker(live, keys.Where((k, i) => decisions[i] == PouchDropDecision.Create || marked(k) || ledger.State(k) != PouchDropState.None));
            for (int i = 0; i < entries.Count; i++)
            {
                switch (decisions[i])
                {
                    case PouchDropDecision.Create:
                        Create(ledger, keys[i], entries[i], pos);
                        break;
                    case PouchDropDecision.Unknown:
                        PouchDropOwner.Warn("Wildblood pouch drop UNKNOWN retained; no second copy is created.");
                        break;
                    case PouchDropDecision.Unavailable:
                        if (lastChance && !marked(keys[i])) Drop(entries[i], pos);
                        else resolved = false;
                        PouchDropOwner.Warn("Wildblood pouch drop ledger full; " + (lastChance ? "dropped unreceipted at death." : "creature kept until it can be recorded."));
                        break;
                }
            }
            return resolved;
        }

        private static void Create(PouchDropLedger ledger, string key, PouchStore.Entry entry, Vector3 pos)
        {
            try
            {
                ItemDrop? drop = Drop(entry, pos);
                ZDO? zdo = drop != null && drop.m_nview != null && drop.m_nview.IsValid() ? drop.m_nview.GetZDO() : null;
                if (zdo == null || !ledger.Complete(key, zdo.m_uid.ToString()))
                {
                    PouchDropOwner.Warn("Wildblood pouch drop identity not confirmed; entry retained as UNKNOWN.");
                }
            }
            catch (Exception error)
            {
                // A native on-drop callback can throw after the world object exists: never create it again.
                PouchDropOwner.Warn("Wildblood pouch drop UNKNOWN after " + error.GetType().Name + "; retained, not recreated.");
            }
        }

        // Ledger unavailable: a death still drops each entry once (the creature is destroyed regardless); a despawn or purge
        // keeps the creature and its pouch instead of risking a second copy.
        private static bool Unreceipted(List<PouchStore.Entry> entries, List<string> keys, Func<string, bool> marked, Vector3 pos, bool lastChance)
        {
            if (!lastChance)
            {
                PouchDropOwner.Warn("Wildblood pouch drop ledger unavailable; creature and pouch kept.");
                return false;
            }
            for (int i = 0; i < entries.Count; i++)
            {
                if (!marked(keys[i])) Drop(entries[i], pos);
            }
            PouchDropOwner.Warn("Wildblood pouch drop ledger unavailable; death drops created unreceipted.");
            return true;
        }

        private static ItemDrop? Drop(PouchStore.Entry entry, Vector3 pos)
        {
            Vector3 dropPos = pos + Random.insideUnitSphere * Spread;
            dropPos.y = pos.y;
            return ItemDrop.DropItem(entry.Item, entry.Item.m_stack, dropPos, Quaternion.identity);
        }

        // An unreadable marker cannot prove an entry undecided, so it answers "decided" for every key.
        private static Func<string, bool> ReadMarker(ZDO? live)
        {
            if (live == null) return _ => false;
            try
            {
                var keys = new HashSet<string>(PouchDropMarker.Decode(live.GetByteArray(TraitKeys.PouchDrops.GetStableHashCode())), StringComparer.Ordinal);
                return keys.Contains;
            }
            catch (System.IO.InvalidDataException)
            {
                Log.Warn("Thieving drop marker unreadable; every entry treated as already decided.");
                return _ => true;
            }
        }

        private static void WriteMarker(ZDO? live, IEnumerable<string> keys)
        {
            if (live == null || !live.IsOwner()) return;
            var list = keys.Distinct().Take(PouchDropMarker.MaxKeys).ToList();
            live.Set(TraitKeys.PouchDrops, PouchDropMarker.Encode(list));
        }
    }
}
