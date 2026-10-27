using Burrow.Resources;

namespace Burrow.Core
{
    /// <summary>OpenKeep owner-readiness adoption: existing owners hand over; free objects are claimed normally.</summary>
    internal static class ContainerClaim
    {
        internal static bool Blocked(Container container) => container == null || NativeInventoryGuards.Held(container.GetInventory()) || NativeInventoryGuards.ForeignHold(container.m_nview);
        public static bool Claim(Container container, float askEvery)
        {
            ZNetView view = container != null ? container.m_nview : null;
            if (view == null || !view.IsValid() || container.m_inventory == null || Blocked(container) || !ContainerScan.IsUsable(container, ContainerUse.Stow)) return false;
            if (!view.IsOwner() && !TakeFree(container, view, askEvery)) return false;
            container.Load();
            return true;
        }
        private static bool TakeFree(Container container, ZNetView view, float askEvery)
        {
            if (IsVehicle(container)) return false;
            if (view.GetZDO().HasOwner()) { HandOver.Ask(container, askEvery); return false; }
            view.ClaimOwnership();
            return view.IsOwner();
        }
        /// <summary>
        /// Recovery of a payment already decided: the chest is requested through the game's normal ownership path (an idle
        /// unowned chest is claimed, an owned one is asked to hand over its latest data) only while nobody has it open, it is
        /// not another client's cart or ship, and no hold is on it other than this payment's own. Usage rules are not asked
        /// again: the payment was permitted when it was decided. True once the local client owns the chest.
        /// </summary>
        internal static bool ClaimForRecovery(Container container, string ownHold)
        {
            ZNetView view = container != null ? container.m_nview : null;
            if (view == null || !view.IsValid() || container.m_inventory == null) return false;
            if (view.IsOwner()) return true;
            if (ContainerScan.InUseByAnother(container) || IsVehicle(container)) return false;
            if (NativeInventoryGuards.ForeignInventoryHeld(container.GetInventory())) return false;
            if (NativeInventoryGuards.ForeignHold(view) && view.GetZDO().GetString(NativeBundleStore.HoldKey) != ownHold) return false;
            if (!TakeFree(container, view, HandOver.ActionSeconds)) return false;
            container.Load();
            return true;
        }
        public static bool CanClaimNow(Container container)
        {
            ZNetView view = container != null ? container.m_nview : null;
            if (view == null || !view.IsValid() || container.m_inventory == null || Blocked(container) || !ContainerScan.IsUsable(container, ContainerUse.Stow)) return false;
            return view.IsOwner() || !view.GetZDO().HasOwner() && !IsVehicle(container) && !ContainerScan.InUseByAnother(container);
        }
        public static bool OwnedElsewhere(Container container)
        {
            ZNetView view = container != null ? container.m_nview : null;
            return view != null && view.IsValid() && view.GetZDO().HasOwner() && !view.IsOwner();
        }
        public static bool VehicleOfAnother(Container container)
        {
            ZNetView view = container != null ? container.m_nview : null;
            return view != null && view.IsValid() && !view.IsOwner() && IsVehicle(container);
        }
        private static bool IsVehicle(Container container) => ContainerScan.IsShip(container) || ContainerScan.IsCart(container);
    }
}
