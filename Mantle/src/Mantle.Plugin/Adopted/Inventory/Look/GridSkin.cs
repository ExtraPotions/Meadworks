using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using Mantle.Core;
using Mantle.Panels;
using UnityEngine.UI;
using UnityEngine;

namespace Mantle.Look
{
    /// <summary>
    /// Mantle's inventory styling, on the Crucible.UI kit's skin (<see cref="KitSkin"/>: Timber wallpaper and bevel, recessed
    /// cells, night shade; the kit also dresses the game's own wood panels when <see cref="KitLook.RequestGamePanels"/> is
    /// asked, see <see cref="LookRequest"/>). What stays here is what is Mantle's: the Panel Look switch (<see cref="On"/>) and
    /// which panels of the inventory screen are dressed. The game's panel backgrounds reach 10 units past their panel; the
    /// kit's reach 12, a little room between the frame and the cells, and the container panel sits 4 units lower so the two
    /// frames keep the game's gap. Off, the game's sprites, materials and sizes come back.
    /// </summary>
    public static class GridSkin
    {
        private const float GameOverhang = KitSkin.GameOverhang;
        private static readonly HashSet<Image> panels = new HashSet<Image>();
        // The inventory screen's panels, which match the game's own panels (KitSkin.GamePanel: the forged set in Timber).
        private static readonly HashSet<Image> gamePanels = new HashSet<Image>();
        private static readonly Dictionary<Image, CompanionLook> companions = new Dictionary<Image, CompanionLook>();
        private static readonly HashSet<Image> declinedCompanions = new HashSet<Image>();

        /// <summary>The Meadworks look is part of the new inventory: the master switch off brings the game's panels back too, and so does the kit's Styled Panels.</summary>
        public static bool On => InventorySettings.Enabled.Value && InventorySettings.BrownStyle.Value && KitSkin.On;

        /// <summary>Extra room above the first row with the frame on, so the cells clear it.</summary>
        public static float TopPad => On ? 4f : 0f;

        /// <summary>How far the background reaches past a panel's rect: the game's 10, or 12 with the kit's frame.</summary>
        public static float PanelOverhang => KitSkin.OverhangFor(On);

        public static void Panels(InventoryGui gui)
        {
            Background(Background(gui.m_player));
            Background(Background(gui.m_container));
            CompanionPanel(Background(gui.m_info));
            CompanionPanel(Background(gui.m_crafting));
            ContainerDrop.Apply(gui.m_container, PanelOverhang - GameOverhang);
        }

        /// <summary>The character header and crafting panel share the theme (KitSkin.GamePanel), retaining their size and controls.</summary>
        private static void CompanionPanel(Image image)
        {
            if (image == null || declinedCompanions.Contains(image)) return;
            if (!companions.TryGetValue(image, out var look))
            {
                if (KitSkin.Owns(image) || KitNativeMenus.Owns(image) || !GamePanelTheme.TryRelease(image))
                { declinedCompanions.Add(image); return; }
                look = new CompanionLook(image); companions.Add(image, look); Track(image);
            }
            look.Apply(On);
        }

        /// <summary>A stretched panel background (the game's Bkg): the skin, and how far it reaches past the panel.</summary>
        public static void Background(Image image)
        {
            Track(image);
            if (image != null) gamePanels.Add(image);
            KitSkin.GameBackground(image, On);
        }

        /// <summary>A Mantle panel beside the inventory panel (gear slots, stats): dressed like the inventory panel itself.</summary>
        public static void GamePanel(Image image)
        {
            Track(image);
            if (image != null) gamePanels.Add(image);
            KitSkin.GamePanel(image, On);
        }

        public static void Panel(Image image)
        {
            Track(image);
            KitSkin.Panel(image, On);
        }

        private static void Track(Image image)
        {
            if (image != null) panels.Add(image);
        }

        internal static void RefreshLook()
        {
            panels.RemoveWhere(image => image == null);
            gamePanels.RemoveWhere(image => image == null);
            // Shared theme changes repaint existing owned art without changing inventory layout.
            foreach (Image image in panels)
                if (companions.TryGetValue(image, out var look)) look.Apply(On, true);
                else if (gamePanels.Contains(image)) KitSkin.GamePanel(image, On);
                else KitSkin.Panel(image, On);
            foreach (var image in new List<Image>(companions.Keys)) if (image == null) companions.Remove(image);
            declinedCompanions.RemoveWhere(image => image == null);
        }

        internal static void ClearTrackedPanels()
        {
            foreach (var look in companions.Values) look.Apply(false, true);
            companions.Clear(); declinedCompanions.Clear(); panels.Clear(); gamePanels.Clear();
        }

        sealed class CompanionLook
        {
            readonly Image image;
            readonly CompanionArt original;
            CompanionArt assigned;
            Transform wood;
            bool painted, yielded, wasOn, wasTimber, woodActive;
            Sprite panel;
            internal CompanionLook(Image image)
            {
                this.image = image; original = new CompanionArt(image);
                wood = image.transform.Find(KitThemeRules.WoodMarker);
            }
            internal void Apply(bool on, bool force = false)
            {
                if (image == null || yielded) return;
                var currentWood = image.transform.Find(KitThemeRules.WoodMarker);
                if (painted && (!assigned.Matches(image) || currentWood != wood || wood != null && wood.gameObject.activeSelf != woodActive))
                {
                    // Retire our wallpaper without writing over the foreign image state.
                    if (wood != null && currentWood == wood && woodActive)
                    {
                        var foreign = new CompanionArt(image); TimberBackground.Apply(image, false); foreign.Restore(image);
                    }
                    yielded = true; return;
                }
                if (!force && painted && on == wasOn && KitArt.Timber == wasTimber && panel == KitArt.Panel) return;
                // The game's own panels wear the forged set (calm wood and the iron frame) like the inventory panel beside
                // them, whichever look reached them first (playtest 2026-10-20: Panel left the crafting panel without its frame).
                KitSkin.GamePanel(image, on);
                if (!on) original.Restore(image);
                assigned = new CompanionArt(image); painted = true; wasOn = on; wasTimber = KitArt.Timber; panel = KitArt.Panel;
                wood = image.transform.Find(KitThemeRules.WoodMarker); woodActive = wood != null && wood.gameObject.activeSelf;
            }
        }
        readonly struct CompanionArt
        {
            readonly Sprite sprite;
            readonly Material material;
            readonly Image.Type type;
            readonly bool fill;
            readonly Color color;
            readonly float ppu;
            internal CompanionArt(Image image)
            { sprite = image.sprite; material = image.material; type = image.type; fill = image.fillCenter; color = image.color; ppu = image.pixelsPerUnitMultiplier; }
            internal bool Matches(Image image) => image.sprite == sprite && image.material == material && image.type == type
                && image.fillCenter == fill && image.color == color && Mathf.Approximately(image.pixelsPerUnitMultiplier, ppu);
            internal void Restore(Image image)
            { image.sprite = sprite; image.material = material; image.type = type; image.fillCenter = fill; image.color = color; image.pixelsPerUnitMultiplier = ppu; }
        }

        public static void Cell(InventoryElement element)
        {
            Image image = element != null ? element.GetComponent<Image>() : null;
            if (image != null)
                KitSkin.Cell(image, On);
        }

        /// <summary>The game's own sprite and material of a panel background, whatever it shows now.</summary>
        public static Sprite GameSprite(Image image) => KitSkin.GameSprite(image);

        public static Material GameMaterial(Image image) => KitSkin.GameMaterial(image);

        /// <summary>A panel's wood: its direct child named Bkg, as the game's scene names it.</summary>
        public static Image Background(RectTransform panel) => KitSkin.Background(panel);
    }

    /// <summary>
    /// Asks the kit to dress the game's own wood panels (crafting, character dialogs, settings) while Mantle's look is on, and
    /// stops asking when it goes off. One owner, so the kit's Theme Game Panels = Auto follows Mantle.
    /// </summary>
    public static class LookRequest
    {
        private static bool asked;

        public static void Refresh()
        {
            bool want = GridSkin.On;
            if (want == asked)
                return;
            asked = want;
            if (want)
                KitLook.RequestGamePanels(typeof(LookRequest));
            else
                KitLook.ReleaseGamePanels(typeof(LookRequest));
        }

        public static void Release()
        {
            if (asked)
                KitLook.ReleaseGamePanels(typeof(LookRequest));
            asked = false;
        }
    }
}
