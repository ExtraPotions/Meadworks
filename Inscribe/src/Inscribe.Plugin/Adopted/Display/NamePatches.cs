// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Integration;
using ExtraPotions.Inscribe.Runtime;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// Rarity-colored item names at the Phase 1 surfaces (display.md section 2). The shared <c>m_shared.m_name</c> is
    /// never touched; rarity decorates the drawn name, and pickup/hover names spell out non-base rarity.
    /// External rarity owners keep their inventory titles and pickup messages. All three run on the viewing
    /// client only; nothing is sent.
    /// </summary>
    internal static class NamePatches
    {
        internal static void Patch(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(GridTooltipPatch)).Patch();
            harmony.CreateClassProcessor(typeof(PickupMessagePatch)).Patch();
            harmony.CreateClassProcessor(typeof(HoverTextPatch)).Patch();
            harmony.CreateClassProcessor(typeof(TooltipPatch)).Patch();
        }
        /// <summary>
        /// Inventory and container grid tooltip title. Runs every frame for the hovered slot, so it takes over with a
        /// single <c>Set</c> (never a second Set in a postfix: the tooltip would rebuild every frame) using the cached
        /// colored topic. Plain items return true and vanilla runs untouched.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.CreateItemTooltip))]
        private static class GridTooltipPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(InventoryGrid __instance, ItemDrop.ItemData item, UITooltip tooltip)
            {
                if (item == null || tooltip == null || !InscribeModules.Active(InscribeModule.Display)
                    || !InscribeModules.StepAside.RarityDisplay)
                {
                    return true;
                }
                ExtraPotions.Meadworks.Crucible.UI.KitTooltip.Dress(tooltip);
                string? topic = ItemIdentification.Name(item);
                if (topic == null)
                {
                    return true;
                }
                tooltip.Set(topic, item.GetTooltip(), __instance.m_tooltipAnchor);
                return false;
            }
        }

        /// <summary>
        /// Pickup message at the top left ("Bronze sword x1"). The local player calls it when it picks an item up;
        /// the replacement shows the same message with the colored name.
        /// </summary>
        [HarmonyPatch(typeof(Character), nameof(Character.ShowPickupMessage))]
        private static class PickupMessagePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Character __instance, ItemDrop.ItemData item, int amount)
            {
                if (item == null || !InscribeModules.Active(InscribeModule.Display) || !InscribeModules.StepAside.RarityDisplay)
                {
                    return true;
                }
                string? topic = ItemIdentification.Name(item, identifyRarity: true);
                if (topic == null)
                {
                    return true;
                }
                __instance.Message(MessageHud.MessageType.TopLeft, "$msg_added " + topic, amount, item.GetIcon());
                return false;
            }
        }

        /// <summary>
        /// Dropped item hover text: the name and rarity precede an identification summary and native pickup prompt.
        /// Called every frame while hovered; <c>GetHoverText</c> has already refreshed the item data with
        /// <c>Load()</c>. See <see cref="HoverName"/>.
        /// </summary>
        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.GetHoverText))]
        private static class HoverTextPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ItemDrop __instance, ref string __result)
            {
                if (__result == null || __instance?.m_itemData == null || !InscribeModules.Active(InscribeModule.Display))
                {
                    return;
                }
                __result = HoverName.Identify(__result, __instance.m_itemData);
            }
        }
    }
}
