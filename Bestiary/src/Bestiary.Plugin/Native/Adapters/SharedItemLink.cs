// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExtraPotions.Bestiary.Catalog;
using ExtraPotions.Bestiary.Fixes;
using HarmonyLib;

namespace ExtraPotions.Bestiary.Runtime.Native
{
    /// <summary>
    /// Inventory loads and crafts instantiate the item prefab, and Unity's Instantiate gives every item its own copy of
    /// the shared data, so a settings change applied to the prefab never reached items already made. Each Bestiary item
    /// keeps that copy and receives the prefab's live numbers when it wakes and when settings change. The attack objects
    /// stay with the item: one greataxe's swing timer and arc are not the attack every greataxe uses. Other mods' items
    /// are left alone.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Awake))]
    internal static class SharedItemLink
    {
        private static readonly HashSet<string> Owned = new HashSet<string>(ItemCatalog.All.Select(i => i.Prefab), StringComparer.Ordinal);
        private static readonly List<Link> Links = new List<Link>();
        private static readonly FieldInfo[] SharedFields = typeof(ItemDrop.ItemData.SharedData)
            .GetFields(BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo[] AttackFields = typeof(Attack)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(field => !SharedAttackRules.IsRuntime(field.Name))
            .ToArray();

        private sealed class Link
        {
            public Link(ItemDrop.ItemData.SharedData own, string prefab)
            {
                Own = new WeakReference<ItemDrop.ItemData.SharedData>(own);
                Prefab = prefab;
            }

            public WeakReference<ItemDrop.ItemData.SharedData> Own { get; }
            public string Prefab { get; }
        }

        private static void Postfix(ItemDrop __instance)
        {
            var item = __instance.m_itemData;
            var prefab = item?.m_dropPrefab;
            if (prefab == null || prefab == __instance.gameObject || !Owned.Contains(prefab.name)) return;
            var source = prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            if (source == null || item == null) return;
            ItemDrop.ItemData.SharedData own = item.m_shared;
            if (own == null || ReferenceEquals(own, source))
            {
                own = new ItemDrop.ItemData.SharedData();
                item.m_shared = own;
            }
            CopyLive(source, own);
            Track(own, prefab.name);
        }

        /// <summary>After a settings change has updated the prefabs, copy those numbers onto items already made.</summary>
        internal static void Refresh()
        {
            ObjectDB? db = ObjectDB.instance;
            if (db == null) return;
            Compact();
            for (int i = 0; i < Links.Count; i++)
            {
                if (!Links[i].Own.TryGetTarget(out ItemDrop.ItemData.SharedData? own) || own == null) continue;
                var source = db.GetItemPrefab(Links[i].Prefab)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                if (source == null || ReferenceEquals(source, own)) continue;
                CopyLive(source, own);
            }
        }

        private static void CopyLive(ItemDrop.ItemData.SharedData from, ItemDrop.ItemData.SharedData to)
        {
            for (int i = 0; i < SharedFields.Length; i++)
            {
                FieldInfo field = SharedFields[i];
                if (field.Name == "m_attack" || field.Name == "m_secondaryAttack") continue;
                field.SetValue(to, field.GetValue(from));
            }
            to.m_attack = Separate(to.m_attack, from.m_attack);
            to.m_secondaryAttack = Separate(to.m_secondaryAttack, from.m_secondaryAttack);
            CopyAttack(from.m_attack, to.m_attack);
            CopyAttack(from.m_secondaryAttack, to.m_secondaryAttack);
        }

        private static Attack Separate(Attack? own, Attack? source)
        {
            if (own != null && !ReferenceEquals(own, source)) return own;
            return new Attack();
        }

        private static void CopyAttack(Attack? from, Attack? to)
        {
            if (from == null || to == null || ReferenceEquals(from, to)) return;
            for (int i = 0; i < AttackFields.Length; i++)
            {
                FieldInfo field = AttackFields[i];
                field.SetValue(to, field.GetValue(from));
            }
        }

        private static void Track(ItemDrop.ItemData.SharedData own, string prefab)
        {
            if ((Links.Count & 31) == 0) Compact();
            for (int i = 0; i < Links.Count; i++)
            {
                if (Links[i].Own.TryGetTarget(out ItemDrop.ItemData.SharedData? current) && ReferenceEquals(current, own))
                    return;
            }
            Links.Add(new Link(own, prefab));
        }

        private static void Compact()
        {
            int write = 0;
            for (int i = 0; i < Links.Count; i++)
            {
                if (Links[i].Own.TryGetTarget(out ItemDrop.ItemData.SharedData? target) && target != null)
                    Links[write++] = Links[i];
            }
            if (write < Links.Count) Links.RemoveRange(write, Links.Count - write);
        }
    }
}
