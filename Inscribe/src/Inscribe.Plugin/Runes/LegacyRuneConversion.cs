// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Awakening and Tempering Runes folded into Ascension and Honing (user decision 2026-10-19). Their
// prefabs stay registered so saved stacks load; every stack becomes the same number of the successor as it loads.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// Converts legacy rune stacks (<see cref="global::ExtraPotions.Inscribe.Runes.LegacyRunes"/>) to their successor, one for one:
    /// <list type="bullet">
    /// <item>an inventory, chest or tombstone as it loads (<see cref="LoadPatch"/>): the stack is created as the successor,
    /// same count, slot and data, and the next save writes the new name; an owned container saves it at once
    /// (<see cref="ContainerLoadPatch"/>);</item>
    /// <item>a stack lying in the world or dropped from a stand as it wakes (<see cref="StonePrefabs.LinkShared"/>): it shows
    /// and picks up as the successor;</item>
    /// <item>a Rune Table's stored counts on the table's owner (<c>TableStore.MigrateLegacy</c>).</item>
    /// </list>
    /// Nothing is removed or added, so a stack can neither vanish nor double; converting twice changes nothing.
    /// </summary>
    internal static class LegacyRuneConversion
    {
        private static readonly Dictionary<int, int> SuccessorHash = new Dictionary<int, int>();

        /// <summary>Stacks converted since the game started (TestBridge and the log read it).</summary>
        public static int Converted { get; private set; }

        /// <summary>The successor's prefab for a legacy rune prefab, or null.</summary>
        public static GameObject? Successor(GameObject? prefab) =>
            prefab != null ? StonePrefabs.GetByPrefabName(global::ExtraPotions.Inscribe.Runes.LegacyRunes.SuccessorPrefab(prefab.name)) : null;

        /// <summary>Turns one live legacy stack into its successor in place (same stack, slot and data); false for anything else.</summary>
        public static bool Convert(ItemDrop.ItemData? item)
        {
            GameObject? successor = Successor(item?.m_dropPrefab);
            ItemDrop? drop = successor != null ? successor.GetComponent<ItemDrop>() : null;
            if (item == null || drop == null)
            {
                return false;
            }
            item.m_dropPrefab = successor;
            item.m_shared = drop.m_itemData.m_shared;
            Count(item.m_stack);
            return true;
        }

        /// <summary>The successor's prefab hash for a legacy rune's hash; the same hash for anything else.</summary>
        public static int SuccessorOf(int prefabHash)
        {
            if (ObjectDB.instance == null)
            {
                return prefabHash;
            }
            if (!SuccessorHash.TryGetValue(prefabHash, out int next))
            {
                GameObject? successor = Successor(ObjectDB.instance.GetItemPrefab(prefabHash));
                next = successor != null ? successor.name.GetStableHashCode() : prefabHash;
                if (StonePrefabs.IsBuilt)
                {
                    SuccessorHash[prefabHash] = next;
                }
            }
            return next;
        }

        private static void Count(int stack)
        {
            Converted++;
            Log.Info($"a legacy rune stack ({stack}) became its successor rune (Awakening to Ascension, Tempering to Honing)");
        }

        /// <summary>A live legacy stack (a world drop picked up, a stack moved in from elsewhere) converts as it enters an inventory.</summary>
        [HarmonyPatch]
        internal static class AddPatch
        {
            private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
            {
                // The positional overload gained a trailing bool (skipValidPositionCheck); the older four-argument form is
                // kept for game builds without it. Looked up directly so the overload a build lacks logs no HarmonyX warning.
                const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                foreach (System.Type[] args in new[]
                {
                    new[] { typeof(ItemDrop.ItemData) },
                    new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) },
                    new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int) },
                })
                {
                    System.Reflection.MethodBase? method = typeof(Inventory).GetMethod(nameof(Inventory.AddItem), Flags, null, args, null);
                    if (method != null)
                    {
                        yield return method;
                    }
                }
            }

            [HarmonyPriority(Priority.First)]
            private static void Prefix(object[] __args) => Convert(__args.Length > 0 ? __args[0] as ItemDrop.ItemData : null);
        }

        /// <summary>
        /// The private load-time <c>Inventory.AddItem</c> (every <c>Inventory.Load</c>: players, chests, tombstones) gets the
        /// successor's hash for a legacy rune, so the stack is built as the successor. Runs before <c>StoneStackGuard</c>.
        /// </summary>
        [HarmonyPatch(typeof(Inventory), "AddItem", new[]
        {
            typeof(int), typeof(int), typeof(float), typeof(Vector2i), typeof(bool), typeof(int), typeof(int), typeof(long),
            typeof(string), typeof(Dictionary<string, string>), typeof(int), typeof(bool), typeof(bool), typeof(bool),
        })]
        [HarmonyPriority(Priority.First)]
        internal static class LoadPatch
        {
            private static void Prefix(ref int prefabHash, int stack)
            {
                int next = SuccessorOf(prefabHash);
                if (next != prefabHash)
                {
                    prefabHash = next;
                    Count(stack);
                }
            }
        }

        /// <summary>Containers that converted legacy runes while loading on a machine that did not own them; saved when opened by an owner.</summary>
        private static readonly HashSet<int> Unsaved = new HashSet<int>();

        /// <summary>True once for a container whose converted runes still wait to be written to its ZDO.</summary>
        public static bool TakeUnsaved(Container container) => container != null && Unsaved.Remove(container.GetInstanceID());

        /// <summary>
        /// <c>Container.Load</c> (chests, carts, tombstones) runs with <c>m_loading</c> set, so the game never saves what
        /// <see cref="LoadPatch"/> converted: the ZDO would keep the legacy names until some later change. The owner writes the
        /// converted inventory back at once (it is exactly what the ZDO just held, renamed); a non-owner remembers it for the
        /// next owned opening. The legacy prefabs stay registered, so an unsaved chest still loads with every rune.
        /// </summary>
        [HarmonyPatch(typeof(Container), nameof(Container.Load))]
        internal static class ContainerLoadPatch
        {
            private static void Prefix(out int __state) => __state = Converted;

            private static void Postfix(Container __instance, bool __result, int __state)
            {
                if (!__result || Converted == __state || __instance == null)
                {
                    return;
                }
                ZNetView? view = __instance.m_nview;
                if (view != null && view.IsValid() && view.IsOwner())
                {
                    Unsaved.Remove(__instance.GetInstanceID());
                    __instance.Save();
                }
                else
                {
                    Unsaved.Add(__instance.GetInstanceID());
                }
            }
        }
    }
}
