// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; the inscription picker.
using ExtraPotions.Inscribe.Adopted.Items;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// The gesture (applying-stones.md section 1): carry a stone stack, click it onto an item. The game routes
    /// click-then-click, drag-and-release, touch and gamepad through <c>InventoryGui.OnSelectedItem</c> for both grids
    /// (game notes Q5, verified against the decompile), so one prefix covers them all.
    /// <para>
    /// The prefix takes the click over only when a stone of ours is carried, the clicked slot holds another item, that
    /// item is not one of our stones (stone onto stone stays a vanilla merge or swap, RC-2) and the player is not
    /// teleporting, and the click would be a use (<c>WouldBeAUse</c>, IMP-55). Then the vanilla swap never happens, whether the stone applies or is refused. Everything else
    /// (dropping a stone on an empty slot, any other drag) runs vanilla. Local player only: this is UI.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
    internal static class StoneClickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(InventoryGui __instance, ItemDrop.ItemData item, InventoryGrid.Modifier mod)
        {
            if (!TakesOver(__instance, item))
            {
                return true;
            }
            Apply(__instance.m_dragItem, item, mod == InventoryGrid.Modifier.Split);
            return false;
        }

        private static bool TakesOver(InventoryGui gui, ItemDrop.ItemData? item)
        {
            Player player = Player.m_localPlayer;
            return player != null && gui.m_dragGo != null && gui.m_dragItem != null && item != null
                && item != gui.m_dragItem && IsStone(gui.m_dragItem) && !ItemClasses.IsStone(item)
                && !player.IsTeleporting() && WouldBeAUse(player, item);
        }

        // A rune prefab (a disabled one, or one the YAML no longer defines, still refuses as stone_disabled).
        private static bool IsStone(ItemDrop.ItemData carried) => StonePrefabs.IsStonePrefab(ItemTier.PrefabName(carried));

        // IMP-55: a click is a stone use (applied or refused) when the target is gear a stone could change, wherever
        // it lies (a chest target is refused with not_own_inventory, APP-2), or any item in the player's own inventory
        // (a non-magic one is refused with not_magic_base, APP-1). A stone onto a plain item in a container stays a
        // vanilla swap, so a stone can still be put into an occupied chest slot. The stone itself may be carried from
        // the player's inventory or from the open chest (APP-2, 2026-10-05).
        private static bool WouldBeAUse(Player player, ItemDrop.ItemData item)
        {
            return ItemClasses.IsRuneBase(item) || player.GetInventory().ContainsItem(item);
        }

        // Steps 1-12, then the gate, which commits. A refusal is shown and nothing changes.
        private static void Apply(ItemDrop.ItemData stone, ItemDrop.ItemData target, bool shiftHeld)
        {
            StoneJob job = StoneJob.Create(Player.m_localPlayer, stone, target);
            StoneResult result = StonePipeline.Evaluate(job);
            if (result.Refused)
            {
                StoneFeedback.Show(job.Player, result.Refusal!);
                return;
            }
            if (result.NeedsSocket)
            {
                GemChooser.Ask(job);
                return;
            }
            if (result.NeedsInscription)
            {
                InscriptionChooser.Ask(job);
                return;
            }
            ConfirmGate.Pass(job, result, shiftHeld);
        }
    }
}
