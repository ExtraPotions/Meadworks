using System;
using Mantle.Core;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The inventory's first opening done ahead, one piece every <see cref="Gap"/> frames while the loading screen still
    /// covers the world after the character spawns (it fades over a second), so the first Tab is like every later one.
    /// Measured 2026-10-07 with every workspace mod and Epic Loot: the first Tab after starting the game held two frames
    /// for 180 ms (the crafting list 45, the grid with Mantle's slot placement 42, the rest Unity's first build and draw
    /// of the screen), after a relog 93 ms; every later opening 25 ms. In turn: the crafting list as Show builds it; the
    /// player grid, stats and weight as an open screen's frame does; the screen drawn without the crafting panel; then
    /// with it. Drawn invisibly (a CanvasGroup at alpha 0 on the screen) by turning the root on after the game's animator
    /// has run, which turns it off again next frame while the inventory is closed; the animator's "visible" flag is never
    /// touched, so nothing counts the inventory as open. Once per inventory screen (each login). An opening, a lost
    /// player or a failure ends it at once with everything put back; a logout takes it away with the screen.
    /// </summary>
    public sealed class InventoryWarmup : MonoBehaviour
    {
        private const int Gap = 3;

        private static InventoryGui warmed;

        private InventoryGui gui;
        private CanvasGroup veil;
        private bool craftingActive;
        private int step;
        private int wait;

        /// <summary>From HudTick each frame: starts once per inventory screen, when Mantle manages the living local player.</summary>
        public static void Tick()
        {
            InventoryGui screen = InventoryGui.instance;
            if (ReferenceEquals(screen, warmed) || screen == null)
                return;
            Player player = Player.m_localPlayer;
            if (!InventoryState.Active || player == null || player.IsDead() || InventoryGui.IsVisible())
                return;
            warmed = screen;
            screen.gameObject.AddComponent<InventoryWarmup>().gui = screen;
        }

        /// <summary>After the animator: a root turned on here is drawn this frame.</summary>
        private void LateUpdate()
        {
            Player player = Player.m_localPlayer;
            if (gui == null || player == null || InventoryGui.IsVisible())
            {
                Finish();
                return;
            }
            if (--wait > 0)
                return;
            wait = Gap;
            try
            {
                Next(player);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Mantle: the inventory warm-up stopped, the first opening builds the rest: {e.Message}");
                Finish();
            }
        }

        private void Next(Player player)
        {
            switch (step++)
            {
                case 0:
                    CraftingPanel.Apply(gui);
                    gui.UpdateCraftingPanel(focusView: true);
                    return;
                case 1:
                    gui.UpdateInventory(player);
                    gui.UpdateCharacterStats(player);
                    gui.UpdateInventoryWeight(player);
                    return;
                case 2:
                    Draw(crafting: false);
                    return;
                case 3:
                    Draw(crafting: true);
                    return;
                default:
                    Finish();
                    return;
            }
        }

        /// <summary>The screen shown invisibly for this frame, with or without the crafting panel.</summary>
        private void Draw(bool crafting)
        {
            if (veil == null)
            {
                craftingActive = gui.m_crafting.gameObject.activeSelf;
                veil = gui.gameObject.AddComponent<CanvasGroup>();
                veil.alpha = 0f;
                veil.interactable = false;
                veil.blocksRaycasts = false;
            }
            gui.m_crafting.gameObject.SetActive(crafting && craftingActive);
            gui.m_inventoryRoot.gameObject.SetActive(true);
        }

        /// <summary>The crafting panel as it was, the root off unless the inventory opened meanwhile, the veil and this gone.</summary>
        private void Finish()
        {
            if (gui != null && veil != null)
            {
                if (!InventoryGui.IsVisible())
                    gui.m_inventoryRoot.gameObject.SetActive(false);
                gui.m_crafting.gameObject.SetActive(craftingActive);
                veil.alpha = 1f;
                Destroy(veil);
            }
            veil = null;
            Destroy(this);
        }
    }
}
