// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Runtime.CompilerServices;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Text;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// Item stand and armor stand hover text (display.md section 2): a magic item on a stand shows its rarity-colored
    /// name and its affix block, under the stand's first line and above the key hints. Plain items, empty stands,
    /// guardian stones and the no-access text stay vanilla. The game rebuilds the hover text every frame, so each
    /// surface remembers its last input and output (<see cref="HoverMemo"/>): a repeat is one content compare and no
    /// allocation. Viewing client only; nothing is sent.
    /// </summary>
    internal static class StandHover
    {
        private static readonly HoverMemo ItemStandMemo = new HoverMemo();
        private static readonly HoverMemo ArmorStandMemo = new HoverMemo();

        [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.GetHoverText))]
        private static class ItemStandPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ItemStand __instance, ref string __result)
            {
                if (string.IsNullOrEmpty(__result) || __instance.m_currentItemName.Length == 0)
                {
                    return;
                }
                ZNetView nview = __instance.m_nview;
                ZDO? zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
                ItemDrop.ItemData? item = zdo == null ? null : StandItems.Read(__instance, zdo, zdo.GetInt(ZDOVars.s_item), -1);
                ItemState state = item == null ? ItemState.Empty : ItemState.Read(item);
                if (!state.IsEmpty)
                {
                    __result = ItemStandText(__result, state, item!);
                }
            }
        }

        /// <summary>Armor stand slots hover through their <c>Switch</c>; other switches are recognised once and skipped.</summary>
        [HarmonyPatch(typeof(Switch), nameof(Switch.GetHoverText))]
        private static class ArmorStandPatch
        {
            [HarmonyPostfix]
            private static void Postfix(Switch __instance, ref string __result)
            {
                ArmorStandLinks.Link? link = string.IsNullOrEmpty(__result) ? null : ArmorStandLinks.Of(__instance);
                ZNetView? nview = link == null ? null : link.Stand.m_nview;
                if (nview == null || !nview.IsValid())
                {
                    return;
                }
                ZDO zdo = nview.GetZDO();
                ItemDrop.ItemData? item = StandItems.Read(link!.Slot, zdo, link.Stand.GetAttachedItem(link.Index), link.Index);
                ItemState state = item == null ? ItemState.Empty : ItemState.Read(item);
                if (!state.IsEmpty)
                {
                    __result = ArmorStandText(__result, state, item!, link.Stand);
                }
            }
        }

        // "Item stand ( Bronze sword )": the name inside the parentheses is colored, the block goes under that line.
        private static string ItemStandText(string vanilla, ItemState state, ItemDrop.ItemData item)
        {
            string block = DisplayCache.Block(state, item);
            if (ItemStandMemo.TryGet(vanilla, block, item.m_shared, out string output))
            {
                return output;
            }
            string name = Words.Localize(item.m_shared.m_name);
            int at = name.Length == 0 ? -1 : vanilla.IndexOf(" ( " + name + " )", StringComparison.Ordinal);
            output = at < 0 ? vanilla : UnderFirstLine(vanilla.Substring(0, at + 3) + ColoredName(state, item, name)
                + vanilla.Substring(at + 3 + name.Length), block);
            return ItemStandMemo.Store(vanilla, block, item.m_shared, output);
        }

        // The slot's hover names the slot, not the item: a colored name line and the block go under the first line.
        private static string ArmorStandText(string vanilla, ItemState state, ItemDrop.ItemData item, ArmorStand stand)
        {
            string block = DisplayCache.Block(state, item);
            if (ArmorStandMemo.TryGet(vanilla, block, item.m_shared, out string output))
            {
                return output;
            }
            output = PrivateArea.CheckAccess(stand.transform.position, 0f, flash: false)
                ? UnderFirstLine(vanilla, "\n" + ColoredName(state, item, Words.Localize(item.m_shared.m_name)) + block)
                : vanilla;
            return ArmorStandMemo.Store(vanilla, block, item.m_shared, output);
        }

        private static string ColoredName(ItemState state, ItemDrop.ItemData item, string localizedName)
        {
            string? topic = DisplayCache.Topic(state, item);
            return topic == null ? localizedName : Words.Localize(topic);
        }

        // The block starts with a blank line (it follows a tooltip); on a hover it sits directly under the first line.
        private static string UnderFirstLine(string text, string block)
        {
            string lines = block.StartsWith("\n\n", StringComparison.Ordinal) ? block.Substring(1) : block;
            int end = text.IndexOf('\n');
            return end < 0 ? text + lines : text.Substring(0, end) + lines + text.Substring(end);
        }
    }

    /// <summary>
    /// Last input and output of one hover surface. The key is the vanilla text's content plus the block and the item
    /// type by reference: the block string is rebuilt (a new reference) whenever the item, the rules, the words or a
    /// display setting change, so an equal key always means an equal output.
    /// </summary>
    internal sealed class HoverMemo
    {
        private string? _vanilla;
        private string? _block;
        private object? _shared;
        private string? _output;

        public bool TryGet(string vanilla, string block, object shared, out string output)
        {
            output = _output!;
            return _output != null && ReferenceEquals(block, _block) && ReferenceEquals(shared, _shared)
                && string.Equals(vanilla, _vanilla);
        }

        public string Store(string vanilla, string block, object shared, string output)
        {
            _vanilla = vanilla;
            _block = block;
            _shared = shared;
            _output = output;
            return output;
        }
    }

    /// <summary>
    /// Which armor stand slot a <c>Switch</c> belongs to, found once per switch (a parent lookup and a slot scan) and
    /// remembered; a switch that is not an armor stand slot is remembered as such, so fuel and lever switches cost one
    /// table lookup per hover frame.
    /// </summary>
    internal static class ArmorStandLinks
    {
        public sealed class Link
        {
            public ArmorStand Stand = null!;
            public ArmorStand.ArmorStandSlot Slot = null!;
            public int Index;
        }

        private sealed class Box
        {
            public Link? Link;
        }

        private static readonly ConditionalWeakTable<Switch, Box> Table = new ConditionalWeakTable<Switch, Box>();
        private static readonly ConditionalWeakTable<Switch, Box>.CreateValueCallback Find = Resolve;

        public static Link? Of(Switch sw) => Table.GetValue(sw, Find).Link;

        private static Box Resolve(Switch sw)
        {
            ArmorStand? stand = sw.GetComponentInParent<ArmorStand>();
            if (stand == null)
            {
                return new Box();
            }
            for (int i = 0; i < stand.m_slots.Count; i++)
            {
                if (stand.m_slots[i].m_switch == sw)
                {
                    return new Box { Link = new Link { Stand = stand, Slot = stand.m_slots[i], Index = i } };
                }
            }
            return new Box();
        }
    }
}
