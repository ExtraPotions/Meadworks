using HarmonyLib;
using ExtraPotions.Mantle.Embedded.PlateColumn;
using Mantle.Core;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The weight beside the minimap (the user's request): a box in PlateColumn's HUD column showing what the local
    /// player carries out of the most they can carry, with the weight icon, written as the inventory's weight box writes
    /// it (red flashing while over). The game writes its own only while the inventory is open, so a <c>Hud.Update</c>
    /// postfix writes this one every frame, touching the text only when it changes. At the game's weight rank, so
    /// it comes before Elite Creatures Reborn's world tier as in the inventory's stat column. Weight Under Minimap (per player) and the
    /// section's master switch show it. Local only; nothing is sent.
    /// </summary>
    [HarmonyPatch(typeof(Hud), nameof(Hud.Update))]
    public static class HudWeight
    {
        private const string Id = "mantle_weight";
        private const int Rank = Column.WeightRank;

        /// <summary>Seconds between tries while the box cannot be made (no minimap yet, the inventory's plates missing).</summary>
        private const float RetrySeconds = 1f;

        private static Plate box;
        private static string shown;
        private static float nextTry;

        [HarmonyPostfix]
        public static void Postfix()
        {
            Player player = Player.m_localPlayer;
            bool show = !InventoryGui.IsVisible() && player != null && InventorySettings.Enabled.Value && InventorySettings.WeightUnderMinimap.Value;
            HudStats.Refresh(show);
            Plate plate = Current(show);
            if (plate == null)
                return;
            if (plate.Rect.gameObject.activeSelf != show)
                plate.Rect.gameObject.SetActive(show);
            if (show)
                Write(plate, player);
        }

        /// <summary>The box already made while it lives, else a new one when it is to be shown; null otherwise.</summary>
        private static Plate Current(bool show)
        {
            if (box != null && box.Rect != null)
                return box;
            InventoryGui gui = InventoryGui.instance;
            if (!show || gui == null || Time.time < nextTry)
                return null;
            nextTry = Time.time + RetrySeconds;
            box = HudRow.Add(gui, new PlateSpec(Id, Rank, HudRow.WeightIcon(gui), withText: true, "", ""));
            shown = null;
            return box;
        }

        /// <summary>The game's weight text (<c>InventoryGui.UpdateInventoryWeight</c>): whole numbers, red flashing when over.</summary>
        private static void Write(Plate plate, Player player)
        {
            string text = WeightDisplay.Format(player);
            if (plate.Text == null || text == shown)
                return;
            plate.Text.text = text;
            shown = text;
        }
    }
}
