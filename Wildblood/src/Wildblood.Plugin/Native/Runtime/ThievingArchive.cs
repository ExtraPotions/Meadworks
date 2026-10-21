// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the theft outcome archive outside the thief. A retiring thief's owner publishes every retained
// outcome to the world archive (TheftWorldArchive) as the native destroy begins, so death, despawn, purge, raid exit and
// any other mod's destroy are covered alike; the requester settles only from that world row or from its own replicated
// copy of the thief, never from a reply alone.
using System;
using System.IO;
using System.Linq;
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Transfers;
using HarmonyLib;
using UnityEngine;
using RootPlugin = ExtraPotions.Wildblood.Runtime.Plugin;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    internal static class ThievingArchive
    {
        private static readonly int OutcomesHash = TraitKeys.TheftOutcomes.GetStableHashCode();

        internal static byte[]? Outcomes(ZDO? zdo) => zdo?.GetByteArray(OutcomesHash);

        /// <summary>True when the thief retains at least one unacknowledged outcome (an empty archive encodes to 8 bytes).</summary>
        internal static bool Retained(ZDO? zdo) => (Outcomes(zdo)?.Length ?? 0) > 8;

        /// <summary>Publishes every retained outcome before the thief leaves. Returns false when a row could not be published
        /// and the removal is not final (no current caller; the destroy hook always publishes).</summary>
        internal static bool Retire(byte[]? outcomes, bool lastChance)
        {
            if (outcomes == null || outcomes.Length <= 8) return true;
            TheftOutcomeArchive archive;
            try { archive = TheftOutcomeArchive.Decode(outcomes); }
            catch (InvalidDataException)
            {
                Warn("Wildblood theft archive unreadable; " + (lastChance ? "nothing published." : "thief kept."));
                return lastChance;
            }
            ZoneSystem zones = ZoneSystem.instance;
            if (zones == null)
            {
                Warn("Wildblood theft world archive unavailable; " + (lastChance ? "outcomes stay UNKNOWN." : "thief kept."));
                return lastChance;
            }
            var rows = TheftWorldArchive.Rows(archive).Where(row => !Published(zones, row)).ToList();
            int existing = zones.GetGlobalKeys().Count(k => k.StartsWith(TheftWorldArchive.Prefix, StringComparison.Ordinal));
            if (!TheftWorldArchive.Admit(existing, rows.Count, lastChance))
            {
                Warn("Wildblood theft world archive full; thief kept until requesters settle.");
                return false;
            }
            if (existing + rows.Count > TheftWorldArchive.Capacity) Warn("Wildblood theft world archive above its bound; a dying thief's outcomes were still published.");
            foreach (string row in rows) zones.SetGlobalKey(row);
            return true;
        }

        private static bool Published(ZoneSystem zones, string row)
        {
            int split = row.IndexOf(' ');
            return zones.GetGlobalKey(row.Substring(0, split), out string value) && value == row.Substring(split + 1);
        }

        /// <summary>The requester's evidence: the world row, else its replicated copy of the thief's archive or exact pouch entry.</summary>
        internal static TheftStatusAnswer Proof(TheftRequest request)
        {
            try
            {
                ZoneSystem zones = ZoneSystem.instance;
                if (zones != null && zones.GetGlobalKey(TheftWorldArchive.Key(request.Root), out string value))
                {
                    var world = TheftWorldArchive.Read(value, request.Fingerprint);
                    if (world != TheftStatusAnswer.Unknown) return world;
                }
                ZDO? zdo = ZDOMan.instance?.GetZDO(new ZDOID(request.SourceUser, request.SourceId));
                if (zdo == null) return TheftStatusAnswer.Unknown;
                var archive = TheftOutcomeArchive.Decode(Outcomes(zdo));
                if (archive.Contains(request.Root)) return archive.Find(request.Root, request.Fingerprint);
                return PouchStore.HoldsExact(PouchStore.Load(zdo), request.Root, request.Fingerprint) ? TheftStatusAnswer.Approved : TheftStatusAnswer.Unknown;
            }
            catch (Exception) { return TheftStatusAnswer.Unknown; } // unreadable evidence proves nothing
        }

        /// <summary>Requester side, after its settlement is saved: removes its own world row. True when a row was removed.</summary>
        internal static bool Release(TheftRequest request)
        {
            ZoneSystem zones = ZoneSystem.instance;
            string key = TheftWorldArchive.Key(request.Root);
            if (zones == null || !zones.GetGlobalKey(key, out string value) || TheftWorldArchive.Read(value, request.Fingerprint) == TheftStatusAnswer.Unknown) return false;
            zones.RemoveGlobalKey(key + " " + value);
            return true;
        }

        private static void Warn(string message) => RootPlugin.NativeLogger?.LogWarning(message);
    }
}

namespace ExtraPotions.Wildblood.Runtime
{
    /// <summary>Every owned network destroy passes here while the ZDO is still readable: a thief with retained outcomes
    /// publishes them before its ZDO is removed. Zone unloading does not destroy the ZDO and is not affected.</summary>
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Destroy), new[] { typeof(GameObject) })]
    internal static class ThievingArchiveDestroy
    {
        private static void Prefix(GameObject go)
        {
            ZNetView? view = go ? go.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            ZDO zdo = view.GetZDO();
            if (!Adopted.Runtime.ThievingArchive.Retained(zdo)) return;
            AdoptedLibs.PatchGuard.Guard.Run("ZNetScene.Destroy theft archive", () => { Adopted.Runtime.ThievingArchive.Retire(Adopted.Runtime.ThievingArchive.Outcomes(zdo), lastChance: true); });
        }
    }
}
