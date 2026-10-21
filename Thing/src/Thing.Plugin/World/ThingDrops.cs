// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, behaviour only: loaded loose-stack cleanup; no third-party source adopted.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using ExtraPotions.Thing.WorldAdmin;
using UnityEngine;

namespace ExtraPotions.Thing.Runtime
{
    public static class ThingDrops
    {
        public readonly struct Target
        {
            public Target(string id, long owner, string fingerprint) { Id = id; Owner = owner; Fingerprint = fingerprint; }
            public string Id { get; }
            public long Owner { get; }
            public string Fingerprint { get; }
            public string Approval => Id + "|" + Fingerprint;
        }

        public static IReadOnlyList<Target> Loaded(Vector3 center, int radius)
        {
            var targets = new List<Target>();
            foreach (var item in ItemDrop.s_instances.ToArray())
            {
                if (!Eligible(item, center, radius)) continue;
                var view = item.GetComponent<ZNetView>();
                if (!view || !view.IsValid() || view.GetZDO().GetOwner() == 0) continue;
                if (TryFingerprint(item, out var fingerprint)) targets.Add(new Target(view.GetZDO().m_uid.ToString(), view.GetZDO().GetOwner(), fingerprint));
            }
            return targets;
        }

        /// <summary>Only approved IDs still loose and in-range can be removed. Items added after preview are untouched.</summary>
        public static bool RemoveOwned(Vector3 center, int radius, IReadOnlyList<string> approved, out int removed)
        {
            removed = 0;
            var fingerprints = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var approval in approved)
            {
                var split = approval.LastIndexOf('|');
                if (split <= 0) return false;
                var fingerprint = approval.Substring(split + 1);
                if (fingerprint.Length != 64 || fingerprint.Any(c => !((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))) return false;
                var id = approval.Substring(0, split);
                if (fingerprints.ContainsKey(id)) return false;
                fingerprints.Add(id, fingerprint);
            }
            var loaded = new Dictionary<string, ItemDrop>(StringComparer.Ordinal);
            foreach (var item in ItemDrop.s_instances.ToArray())
            {
                if (!Eligible(item, center, radius)) continue;
                var view = item.GetComponent<ZNetView>();
                if (!view || !view.IsValid()) continue;
                var id = view.GetZDO().m_uid.ToString();
                if (fingerprints.TryGetValue(id, out var fingerprint) && TryFingerprint(item, out var current) && current == fingerprint) loaded[id] = item;
            }
            // All-owner preflight before the first removal. Vanished, collected, placed or moved items stay untouched.
            foreach (var item in loaded.Values) if (!item.GetComponent<ZNetView>().IsOwner()) return false;
            foreach (var item in loaded.Values)
            {
                if (!Eligible(item, center, radius)) continue;
                var view = item.GetComponent<ZNetView>();
                if (!view || !view.IsValid() || !view.IsOwner()) return false;
                if (!TryFingerprint(item, out var fingerprint) || fingerprint != fingerprints[view.GetZDO().m_uid.ToString()]) continue;
                view.Destroy();
                removed++;
            }
            return true;
        }

        static bool TryFingerprint(ItemDrop item, out string fingerprint)
        {
            fingerprint = "";
            try
            {
                // Native item persistence captures stack/content changes, while world physics motion is excluded.
                var package = new ZPackage();
                item.m_itemData.Save(package);
                using (var hash = SHA256.Create()) fingerprint = BitConverter.ToString(hash.ComputeHash(package.GetArray())).Replace("-", "");
                return true;
            }
            catch { return false; } // Unknown or unserializable contents are preserved.
        }

        static bool Eligible(ItemDrop item, Vector3 center, int radius)
        {
            if (!item || item.m_itemData == null || item.m_itemData.m_pickedUp || item.m_pickupRequester != null || item.transform.parent != null) return false;
            var kind = Kind(item);
            return DropRules.Removes(kind, Vector3.Distance(center, item.transform.position), radius);
        }

        static DropKind Kind(ItemDrop item)
        {
            if (item.GetComponentInParent<TombStone>()) return DropKind.Tombstone;
            if (item.GetComponentInParent<Container>()) return DropKind.Container;
            if (item.GetComponentInParent<Fish>()) return DropKind.SwimmingFish;
            if (item.GetComponentInParent<ItemStand>() || item.IsPiece()) return DropKind.PlacedItem;
            // Native loose stacks have a dynamic body; display/placed/preview objects are excluded conservatively.
            var body = item.GetComponent<Rigidbody>();
            if (!body || body.isKinematic) return DropKind.PlacedItem;
            return DropKind.LooseItem;
        }
    }
}
