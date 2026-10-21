using HarmonyLib;
using UnityEngine;

namespace Burrow.Shared
{
    /// <summary>
    /// Keeps the chest panel alive for a viewer. <c>InventoryGui.UpdateContainer(Player)</c> hides the chest
    /// panel when the local client does not own <c>m_currentContainer</c>; for a viewed container this prefix does
    /// the owner's work instead: shows the panel, refreshes the grid from the container's inventory (which the
    /// game reloads from the ZDO once a second for non-owners), sets the title, applies the auto-close distance,
    /// and asks for the game's own open when the chest is no longer in use so the panel turns live without
    /// closing. The open request is repeated every few seconds while viewing because a stale in-use flag would
    /// otherwise never clear; a refusal is silent (<see cref="ViewOpenResponsePatch"/>). The Take all and Stack
    /// all buttons are disabled in View mode. Hide and CloseContainer end the viewer state. The game calls
    /// UpdateContainer only while the panel is visible, so no visibility check is repeated here.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer))]
    public static class ViewerPanel
    {
        private const float RetrySeconds = 5f;
        private static float lastOpenRequest = -100f;

        [HarmonyPrefix]
        public static bool Prefix(InventoryGui __instance, Player player)
        {
            Container container = __instance.m_currentContainer;
            if (!SharedState.IsViewing(container))
            {
                LeaveViewer(__instance);
                return true;
            }
            ShowViewed(__instance, container);
            if (Vector3.Distance(container.transform.position, player.transform.position) > __instance.m_autoCloseDistance)
            {
                __instance.CloseContainer();
                return false;
            }
            RequestOpenWhenFree(container, player);
            return false;
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
        private static class HidePatch
        {
            [HarmonyPostfix]
            private static void Postfix(InventoryGui __instance) => LeaveViewer(__instance);
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.CloseContainer))]
        private static class ClosePatch
        {
            [HarmonyPostfix]
            private static void Postfix(InventoryGui __instance) => LeaveViewer(__instance);
        }

        /// <summary>Records that an open request was just sent (the read-only open sends none; the first retry follows soon).</summary>
        internal static void OpenRequested(Container container)
        {
            lastOpenRequest = Time.time - RetrySeconds + 1f;
        }

        private static void ShowViewed(InventoryGui gui, Container container)
        {
            gui.m_container.gameObject.SetActive(true);
            gui.m_containerGrid.UpdateInventory(container.GetInventory(), null, gui.m_dragItem);
            gui.m_containerName.text = SharedState.Title(container);
            if (gui.m_firstContainerUpdate)
            {
                gui.m_containerGrid.ResetView();
                gui.m_firstContainerUpdate = false;
                gui.m_containerHoldTime = 0f;
                gui.m_containerHoldState = 0;
            }
            bool full = SharedState.FullMode;
            if (!full && gui.m_dragInventory != null && gui.m_dragInventory == container.GetInventory())
                gui.SetupDragItem(null, null, 1);
            SetButtons(gui, full);
        }

        /// <summary>
        /// When the in-use flag clears, and every few seconds anyway, the game's own open request goes to the
        /// owner: granted, it hands the chest over and calls Show, which makes the panel a normal one.
        /// </summary>
        private static void RequestOpenWhenFree(Container container, Player player)
        {
            bool free = !Core.ContainerScan.InUseByAnother(container);
            if (!free && Time.time - lastOpenRequest < RetrySeconds)
                return;
            if (free && Time.time - lastOpenRequest < 1f)
                return;
            lastOpenRequest = Time.time;
            Plugin.Log.LogDebug($"Burrow: viewer asks to open {Core.ContainerScan.PrefabName(container)} (free: {free})");
            container.Interact(player, false, false);
        }

        private static void LeaveViewer(InventoryGui gui)
        {
            if (SharedState.ViewedContainer == null)
                return;
            SetButtons(gui, true);
            SharedState.EndView();
        }

        private static void SetButtons(InventoryGui gui, bool interactable)
        {
            if (gui.m_takeAllButton != null && gui.m_takeAllButton.interactable != interactable)
                gui.m_takeAllButton.interactable = interactable;
            if (gui.m_stackAllButton != null && gui.m_stackAllButton.interactable != interactable)
                gui.m_stackAllButton.interactable = interactable;
        }
    }
}
