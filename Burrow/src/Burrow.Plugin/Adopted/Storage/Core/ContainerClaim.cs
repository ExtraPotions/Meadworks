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
