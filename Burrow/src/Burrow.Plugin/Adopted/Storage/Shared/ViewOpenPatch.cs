using HarmonyLib;
using Burrow.Core;

namespace Burrow.Shared
{
    /// <summary>
    /// The read-only open of SPEC 9.1. <c>Container.Interact(Humanoid, bool, bool)</c> sends the game's open
    /// request, which the owner refuses while the chest is in use. With <c>View</c> or <c>Full</c>, when the chest
    /// is in use by another player and the ward and privacy checks pass, the panel is shown with the chest instead
    /// (<c>InventoryGui.Show(container)</c>, the call the game makes when an open is granted) and the game's own
    /// request is skipped; <see cref="ViewerPanel"/> keeps that panel alive. Everything else stays the game's:
    /// the owner, a chest nobody uses, a ward or privacy refusal, and the chest already being viewed, whose
    /// viewer re-sends the game's request through this method to turn the panel live.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    public static class ViewOpenPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(Container __instance, Humanoid character, bool hold, ref bool __result, bool __runOriginal = true)
        {
            if (!__runOriginal)
                return true;
            if (Burrow.Stow.InteractQuickStack.TryRun(__instance, character, hold))
            {
                __result = true;
                return false;
            }
            if (hold || Burrow.Integration.MultiUserChestYield.ModeFor(__instance) == SharedMode.Off || SharedState.ViewedContainer == __instance)
                return true;
            if (!CanView(__instance, character))
                return true;
            SharedState.BeginView(__instance);
            ViewerPanel.OpenRequested(__instance);
            InventoryGui.instance.Show(__instance);
            __result = true;
            return false;
        }

        /// <summary>In use by another player, the ward and privacy checks of Container.Interact pass, the panel exists.</summary>
        private static bool CanView(Container container, Humanoid character)
        {
            if (InventoryGui.instance == null || character == null || !(character is Player) || container.m_inventory == null)
                return false;
            if (!ContainerScan.InUseByAnother(container))
                return false;
            if (container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position, 0f, false))
                return false;
            if (container.m_privacy == Container.PrivacySetting.Private && container.m_piece == null)
                return false;
            return container.CheckAccess(Game.instance.GetPlayerProfile().GetPlayerID());
        }
    }

    /// <summary>
    /// While a chest is viewed its viewer re-sends the game's open request now and then (see
    /// <see cref="ViewerPanel"/>); a refusal must not print the game's "in use" message every time, a grant shows
    /// the panel as usual.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.RPC_OpenResponse))]
    public static class ViewOpenResponsePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Container __instance, bool granted)
        {
            return granted || !SharedState.IsViewing(__instance);
        }
    }
}
