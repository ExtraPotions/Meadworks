using System.Collections.Generic;
using Burrow.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Burrow.Stow
{
    /// <summary>
    /// With a container open, the cycle keys and the mouse wheel over a non-scrolling container grid close it and open the
    /// previous or next container, ordered by angle around the player. Only containers the game would keep open
    /// take part: the panel closes any container farther than its auto close distance, so the ring is limited to
    /// that distance (and Nearby Range). Opening goes through the game's own path, <c>Container.Interact</c>,
    /// which requests ownership by RPC and shows the panel when granted; with <c>Shared Chests</c> on (View or
    /// Full) the Shared module turns that call into a read-only open for a chest another player is using, so such
    /// chests are part of the ring when the player could view them.
    /// </summary>
    public static class Cycling
    {
        private const float WheelCooldown = 0.25f;
        private static float lastWheel;

        public static void Poll(InventoryGui gui)
        {
            if (gui.m_currentContainer == null)
                return;
            if (Keys.Pressed(StowSettings.CyclePreviousKey))
                Cycle(-1);
            else if (Keys.Pressed(StowSettings.CycleNextKey))
                Cycle(1);
            else if (StowSettings.CycleWithWheel.Value)
                PollWheel(gui);
        }

        private static void PollWheel(InventoryGui gui)
        {
            float wheel = ZInput.GetMouseScrollWheel();
            if (wheel == 0f || Time.time - lastWheel < WheelCooldown || Keys.TextInputActive || gui.m_containerGrid == null
                || GridScrolls(gui.m_containerGrid) || TooltipScrolls())
                return;
            RectTransform rect = gui.m_containerGrid.transform as RectTransform;
            if (rect == null || !rect.rect.Contains((Vector2)rect.InverseTransformPoint(ZInput.pointerPosition)))
                return;
            lastWheel = Time.time;
            Cycle(wheel > 0f ? -1 : 1);
        }

        private static bool GridScrolls(InventoryGrid grid)
        {
            Scrollbar bar = grid.m_scrollbar;
            return bar != null && bar.gameObject.activeInHierarchy && bar.size < 0.999f;
        }

        private static bool TooltipScrolls()
        {
            GameObject tooltip = UITooltip.m_tooltip;
            if (tooltip == null || !tooltip.activeInHierarchy)
                return false;
            foreach (ScrollRect scroll in tooltip.GetComponentsInChildren<ScrollRect>())
            {
                RectTransform viewport = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
                if (scroll.isActiveAndEnabled && scroll.content != null && viewport != null
                    && (scroll.vertical && scroll.content.rect.height > viewport.rect.height + 0.5f
                        || scroll.horizontal && scroll.content.rect.width > viewport.rect.width + 0.5f))
                    return true;
            }
            Transform bar = Utils.FindChild(tooltip.transform, "inscribe_tooltip_bar");
            if (bar != null && bar.gameObject.activeInHierarchy)
                return true;
            bar = Utils.FindChild(tooltip.transform, "ecf_tooltip_bar");
            return bar != null && bar.gameObject.activeInHierarchy;
        }

        public static void Cycle(int direction)
        {
            if (!StowActions.Ready(out Player player))
                return;
            InventoryGui gui = InventoryGui.instance;
            Container current = gui != null ? gui.m_currentContainer : null;
            if (current == null)
                return;
            List<Container> ring = Ring(player, gui, current);
            if (ring.Count < 2)
            {
                Messages.Center(StowWords.NoCycle);
                return;
            }
            int index = ring.IndexOf(current);
            Container next = ring[(index + direction + ring.Count) % ring.Count];
            gui.CloseContainer();
            next.Interact(player, false, false);
        }

        private static List<Container> Ring(Player player, InventoryGui gui, Container current)
        {
            float range = Mathf.Min(StowSettings.NearbyRange.Value, gui.m_autoCloseDistance);
            Vector3 origin = player.transform.position;
            List<Container> ring = ContainerScan.Nearby(origin, range, ContainerUse.Stow);
            foreach (Container container in ContainerScan.All())
            {
                if (!ring.Contains(container) && StowTargets.Distance(origin, container) <= range && Viewable(container))
                    ring.Add(container);
            }
            if (!ring.Contains(current))
                ring.Add(current);
            ring.Sort((a, b) => Angle(origin, a).CompareTo(Angle(origin, b)));
            return ring;
        }

        /// <summary>
        /// A chest another player uses that the Shared module would open read-only: Shared Chests is View or
        /// Full, the ward and privacy checks of Container.Interact pass, and the section 0 rules of the Stow
        /// targets (the prefab enabled, the ship, cart and private chest switches) allow it.
        /// </summary>
        private static bool Viewable(Container container)
        {
            Player player = Player.m_localPlayer;
            if (player == null || Burrow.Integration.MultiUserChestYield.ModeFor(container) == SharedMode.Off || container.GetInventory() == null)
                return false;
            if (!ContainerScan.InUseByAnother(container) || !ContainerRules.IsEnabled(ContainerScan.PrefabName(container)) || !SwitchAllows(container))
                return false;
            if (container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position, 0f, false))
                return false;
            if (container.m_privacy == Container.PrivacySetting.Private && container.m_piece == null)
                return false;
            return container.CheckAccess(player.GetPlayerID());
        }

        private static bool SwitchAllows(Container container)
        {
            if (ContainerScan.IsShip(container))
                return CoreSettings.Ships.Value;
            if (ContainerScan.IsCart(container))
                return CoreSettings.Carts.Value;
            return !ContainerScan.IsPrivateChest(container) || CoreSettings.PlayerChests.Value;
        }

        private static float Angle(Vector3 origin, Container container)
        {
            Vector3 delta = container.transform.position - origin;
            return Mathf.Atan2(delta.z, delta.x);
        }
    }
}
