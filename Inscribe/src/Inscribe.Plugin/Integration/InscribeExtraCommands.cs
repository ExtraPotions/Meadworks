// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Inscribe.Adopted.Commands;
using ExtraPotions.Inscribe.Adopted.Api;
using ExtraPotions.Inscribe.Integration;
namespace ExtraPotions.Inscribe.Runtime
{
    internal static class InscribeExtraCommands
    {
        /// <summary>Names carry no badge (user decision 2026-10-19: the game's tooltip already shows quality); the rarity name stays.</summary>
        internal static string? DecorateName(ItemDrop.ItemData item, string? original) => original;
        private static ItemDrop.ItemData? Owned(CommandCall call)
        {
            var item = ItemTargets.Resolve("", false, out _);
            if (item == null || Player.m_localPlayer == null || !Player.m_localPlayer.GetInventory().ContainsItem(item)) { call.Reply("Select or hover an item in your own inventory."); return null; }
            return item;
        }
        internal static void Roll(CommandCall call)
        {
            var item = Owned(call); if (item == null) return;
            call.Reply(ApiItems.RollMagic(item, call.Lower(0)) ? "rolled " + call.Lower(0) : "roll refused");
            Player.m_localPlayer.GetInventory().Changed();
        }
        internal static void Cleanse(CommandCall call)
        {
            var item = Owned(call); if (item == null) return;
            call.Reply(ApiItems.Cleanse(item) ? "cleansed" : "cleanse refused");
            Player.m_localPlayer.GetInventory().Changed();
        }
        /// <summary>The removed enhancement scrolls are no longer given; every other give goes to the rune command.</summary>
        internal static void Give(CommandCall call) => GiveCommand.Run(call);
    }
}
