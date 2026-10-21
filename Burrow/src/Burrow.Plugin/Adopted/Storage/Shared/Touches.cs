using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Burrow.Shared
{
    /// <summary>
    /// The feedback of SPEC 9.2. In Full mode, mouse-down on a stack of the open chest's grid sends
    /// <c>Burrow_Touch</c> (slot, player name, on) to the chest's owner, which forwards it to everybody; the
    /// touch ends when the button is up with nothing dragged, when the panel leaves the chest, or after
    /// <c>Touch Seconds</c>. Other clients tint that slot (<c>Touch Colour</c>, <c>Show Touches</c>) and add
    /// "&lt;name&gt; is moving this" to its tooltip until the touch ends or <c>Touch Seconds</c> pass. Feedback
    /// only; correctness comes from the requests.
    /// </summary>
    public static class Touches
    {
        private sealed class Touch
        {
            public Container Container;
            public Vector2i Slot;
            public string Name;
            public float Until;
        }

        private static readonly List<Touch> touches = new List<Touch>();
        private static Container sentContainer;
        private static Vector2i sentSlot;
        private static float sentAt;

        /// <summary>Burrow_Touch: the owner forwards a fresh touch to everybody; a forwarded one is shown.</summary>
        public static void Receive(Container container, long sender, ZPackage pkg)
        {
            Vector2i slot = pkg.ReadVector2i();
            string name = pkg.ReadString();
            bool on = pkg.ReadBool();
            bool forwarded = pkg.ReadBool();
            if (!forwarded)
            {
                if (container.m_nview.IsOwner())
                    container.m_nview.InvokeRPC(ZNetView.Everybody, ChestRequests.TouchRpc, Packet(slot, name, on, true));
                return;
            }
            if (Player.m_localPlayer != null && name == Player.m_localPlayer.GetPlayerName())
                return;
            touches.RemoveAll(touch => touch.Container == container && touch.Slot == slot);
            if (on)
                touches.Add(new Touch { Container = container, Slot = slot, Name = name, Until = Time.time + SharedSettings.TouchSeconds.Value });
        }

        private static ZPackage Packet(Vector2i slot, string name, bool on, bool forwarded)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(slot);
            pkg.Write(name);
            pkg.Write(on);
            pkg.Write(forwarded);
            return pkg;
        }

        private static void Send(Container container, Vector2i slot, bool on)
        {
            if (container != null && container.m_nview != null && container.m_nview.IsValid() && Player.m_localPlayer != null)
                container.m_nview.InvokeRPC(ChestRequests.TouchRpc, Packet(slot, Player.m_localPlayer.GetPlayerName(), on, false));
            sentContainer = on ? container : null;
            sentSlot = slot;
            sentAt = Time.time;
        }

        /// <summary>The touch this client sent ends when the button is up with nothing dragged, the chest left the panel, or the time passed.</summary>
        private static void EndSentTouch(InventoryGui gui)
        {
            if (sentContainer == null)
                return;
            bool released = gui.m_dragGo == null && !ZInput.GetMouseButton(0);
            bool gone = gui.m_currentContainer != sentContainer || Time.time - sentAt > SharedSettings.TouchSeconds.Value;
            if (released || gone)
                Send(sentContainer, sentSlot, false);
        }

        private static void Expire()
        {
            if (touches.Count > 0)
                touches.RemoveAll(touch => touch.Container == null || touch.Until < Time.time);
        }

        /// <summary>The element whose tooltip the grid shows: the gamepad or touch selection, else the one under the pointer.</summary>
        private static InventoryElement Hovered(InventoryGrid grid)
        {
            bool selection = grid.m_uiGroup != null && grid.m_uiGroup.IsActive && (ZInput.IsExclusiveGamepadActive() || ZInput.IsTouchActive());
            return selection ? grid.GetElement(grid.m_selected.x, grid.m_selected.y, grid.m_width) : grid.GetHoveredElement();
        }

        private static void Paint(InventoryGrid grid, Container container)
        {
            Color colour = SharedSettings.Colour();
            InventoryElement hovered = Hovered(grid);
            foreach (Touch touch in touches)
            {
                if (touch.Container != container)
                    continue;
                InventoryElement element = grid.GetElement(touch.Slot.x, touch.Slot.y, grid.m_width);
                if (element == null || !element.m_used)
                    continue;
                element.m_icon.color = colour;
                if (element == hovered && element.m_tooltip != null)
                    element.m_tooltip.Set(element.m_tooltip.m_topic, element.m_tooltip.m_text + "\n" + touch.Name + " " + SharedWords.Moving, grid.m_tooltipAnchor);
            }
        }

        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.OnLeftDown))]
        private static class LeftDownPatch
        {
            [HarmonyPostfix]
            private static void Postfix(InventoryGrid __instance, UIInputHandler clickHandler)
            {
                InventoryGui gui = InventoryGui.instance;
                if (gui == null || __instance != gui.m_containerGrid || !SharedState.FullMode)
                    return;
                Container container = gui.m_currentContainer;
                if (container == null || container.GetInventory() == null)
                    return;
                Vector2i pos = __instance.GetButtonPos(clickHandler.gameObject);
                if (pos.x < 0 || container.GetInventory().GetItemAt(pos.x, pos.y) == null)
                    return;
                Send(container, pos, true);
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
        private static class UpdatePatch
        {
            [HarmonyPostfix]
            private static void Postfix(InventoryGui __instance)
            {
                EndSentTouch(__instance);
                Expire();
            }
        }

        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
        private static class PaintPatch
        {
            [HarmonyPostfix]
            private static void Postfix(InventoryGrid __instance)
            {
                InventoryGui gui = InventoryGui.instance;
                if (gui == null || __instance != gui.m_containerGrid || touches.Count == 0 || !SharedSettings.ShowTouches.Value)
                    return;
                Container container = gui.m_currentContainer;
                if (container != null)
                    Paint(__instance, container);
            }
        }
    }
}
