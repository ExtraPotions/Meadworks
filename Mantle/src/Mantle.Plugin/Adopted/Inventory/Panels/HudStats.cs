using ExtraPotions.Mantle.Embedded.PlateColumn;
using TMPro;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// Armor, weight and world level in inventory order, with matching squares in a column right of the minimap: full
    /// size (48 units, the user found them tiny at 65%), the map moved over to make room (<see cref="HudRoom"/>).
    /// </summary>
    public static class HudStats
    {
        /// <summary>The HUD column's scale: its squares as big as the stats panel's.</summary>
        private const float Scale = 1f;

        private static Plate armor;
        private static float nextTry;
        private static int armorShown;
        private static TMP_Text armorText;
        private static Plate globe;
        private static float nextGlobeTry, nextLook;
        private static string globeShown;
        private static System.Func<System.Tuple<string, string>> wildblood;

        public static void Refresh(bool show)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || gui.m_takeAllButton == null) return;
            Armor(gui, show);
            WorldLevel(gui, show);
            RectTransform column = HudRow.Container();
            if (column != null)
            {
                if (column.localScale.x != Scale) column.localScale = Vector3.one * Scale;
                for (int i = 0; i < column.childCount; i++)
                {
                    RectTransform box = column.GetChild(i) as RectTransform;
                    if (box != null && StatIconFrames.IsHudStat(box)) StatIconFrames.Dress(gui, box, true, false);
                }
            }
            HudRoom.Fit(column);
        }

        /// <summary>
        /// The third square, Solo's globe with the world level (user, 2026-10-12 14:46), below armour and carry. The level comes from
        /// Wildblood's optional HUD status capability (meadworks.wildblood.hud); without Wildblood, or while it shows no tier, the square is hidden.
        /// </summary>
        private static void WorldLevel(InventoryGui gui, bool show)
        {
            if (wildblood == null && Time.unscaledTime >= nextLook)
            {
                nextLook = Time.unscaledTime + 5f;
                try { ExtraPotions.Meadworks.Crucible.Plugin.Services.Capabilities.TryResolve("meadworks.wildblood.hud", new System.Version(1, 0), out wildblood); }
                catch { wildblood = null; }
            }
            string level = null;
            if (show && wildblood != null)
            {
                try { level = Mantle.Presentation.WildbloodPlateReading.HudTier(wildblood()); }
                catch { level = null; }
            }
            show = level != null;
            if ((globe == null || globe.Rect == null) && show && Time.time >= nextGlobeTry && Mantle.Look.SkinArt.Globe != null)
            {
                nextGlobeTry = Time.time + 1f;
                // Named *_world_tier so StatIconFrames dresses it like Solo's square; ranked after the weight box.
                globe = HudRow.Add(gui, new PlateSpec("mantle_world_tier", Column.WeightRank + 100, Mantle.Look.SkinArt.Globe, true, "", ""));
                globeShown = null;
            }
            if (globe == null || globe.Rect == null) return;
            if (globe.Rect.gameObject.activeSelf != show) globe.Rect.gameObject.SetActive(show);
            if (show && globe.Text != null && level != globeShown)
            {
                globe.Text.text = level;
                globeShown = level;
            }
        }

        private static void Armor(InventoryGui gui, bool show)
        {
            if ((armor == null || armor.Rect == null) && show && Time.time >= nextTry)
            {
                nextTry = Time.time + 1f;
                // HudRow copies the game's armor plate; a null sprite preserves its shield icon.
                armor = HudRow.Add(gui, new PlateSpec("mantle_armor", Column.ArmorRank, null, true, "", ""));
            }
            if (armor == null || armor.Rect == null) return;
            if (armor.Rect.gameObject.activeSelf != show) armor.Rect.gameObject.SetActive(show);
            if (show && armor.Text != null)
            {
                // The number is turned into text only when it or the box changes.
                int value = Mathf.CeilToInt(Player.m_localPlayer.GetBodyArmor());
                if (value == armorShown && armor.Text == armorText) return;
                armor.Text.text = value.ToString();
                armorShown = value;
                armorText = armor.Text;
            }
        }
    }
}
