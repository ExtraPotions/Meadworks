// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Look/GridSkin.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: renamed GridSkin to KitSkin and generalised from the
// inventory's four panels to any panel or cell image a product hands in; the inventory layout (Panels, ContainerDrop)
// stays with the product that owns the inventory; remembered sprites are pruned on scene loads instead of on
// InventoryGui.OnDestroy; skinned images are recorded so the game-panel theme leaves them to the kit look.
// Modified by expDARE, 2026-10-20: GamePanel/GameBackground give a product's inventory-screen panels the game panels' Timber forged set.
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible.UI.PlateColumn;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// Kit panel and cell styling. Brown uses PackPanel's custom panel; Timber combines screen-aligned wallpaper with the
    /// sliced bevel. Both use the default UI material. Cells keep their recessed look; PlateColumn's plain Skin stands in
    /// for missing custom artwork. The game's panel backgrounds reach 10 units past their panel; the brown ones reach 12,
    /// a little room between the frame and the cells. Off, the game's sprites, materials and sizes come back: each
    /// panel's own are remembered the first time it is changed, the cell sprite once from the first game cell seen.
    /// </summary>
    public static class KitSkin
    {
        public const float GameOverhang = 10f;
        public const float WoodOverhang = 12f;

        private static readonly Dictionary<Image, Sprite> panelSprites = new Dictionary<Image, Sprite>();
        private static readonly Dictionary<Image, Material> panelMaterials = new Dictionary<Image, Material>();
        private static readonly HashSet<Image> owned = new HashSet<Image>();
        private static readonly List<Image> gone = new List<Image>();
        private static Sprite gameCell;
        private static bool hooked;

        /// <summary>The kit look is on (PackPanel: master switch and Brown Style).</summary>
        public static bool On => KitLook.On;

        /// <summary>Extra room above the first row with the frame on, so the cells clear it.</summary>
        public static float TopPad => On ? 4f : 0f;

        /// <summary>How far the background reaches past a panel's rect: the game's 10, or 12 with the brown frame.</summary>
        public static float PanelOverhang => OverhangFor(On);

        /// <summary>The overhang for a look that is on or off (a product with its own switch asks with its own state).</summary>
        public static float OverhangFor(bool on) => on && KitArt.Panel != null ? WoodOverhang : GameOverhang;

        /// <summary>An image the kit dresses (its look follows <see cref="KitLook"/>, not the game-panel switch).</summary>
        public static bool Owns(Image image) => image != null && owned.Contains(image);

        /// <summary>A stretched panel background (the game's Bkg): the skin, and how far it reaches past the panel.</summary>
        public static void Background(Image image) => Background(image, On);

        /// <summary>As <see cref="Background(Image)"/> for a product whose own switch decides whether the look is on.</summary>
        public static void Background(Image image, bool on)
        {
            Panel(image, on);
            if (image != null)
                image.rectTransform.sizeDelta = Vector2.one * (OverhangFor(on) * 2f);
        }

        public static void Panel(Image image) => Panel(image, On);

        /// <summary>
        /// A panel standing among the game's own panels (an inventory-screen panel a product adds or takes over). In Timber it
        /// wears the forged set the game's panels wear (<see cref="GamePanelTheme"/>: calm wood, straight edge, iron frame), so
        /// it matches them whichever look reached it first. Brown and off are as <see cref="Panel(Image, bool)"/>; off also
        /// hides the iron frame.
        /// </summary>
        public static void GamePanel(Image image, bool on)
        {
            if (image == null)
                return;
            bool forged = on && KitArt.Timber && KitArt.Panel != null;
            if (forged)
                TimberBackground.RegisterForged(image);
            Panel(image, on);
            KitForged.GamePanel(image, forged);
        }

        /// <summary>As <see cref="GamePanel"/> for a stretched panel background (the game's Bkg), with its overhang.</summary>
        public static void GameBackground(Image image, bool on)
        {
            GamePanel(image, on);
            if (image != null)
                image.rectTransform.sizeDelta = Vector2.one * (OverhangFor(on) * 2f);
        }

        public static void Panel(Image image, bool on)
        {
            if (image == null)
                return;
            Hook();
            owned.Add(image);
            TimberBackground.Apply(image, false);
            if (!panelSprites.ContainsKey(image) && image.sprite != Skin.Panel && !KitArt.IsPanel(image.sprite))
            {
                Prune();
                panelSprites[image] = image.sprite;
                panelMaterials[image] = image.material;
            }
            Sprite wood = KitArt.Panel;
            bool timber = KitArt.Timber;
            image.sprite = !on || timber ? GameSprite(image) : wood != null ? wood : Skin.Panel;
            image.material = !on || timber ? GameMaterial(image) : null;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            TimberBackground.Apply(image, on && timber && wood != null);
            NightShade.Track(image);
        }

        /// <summary>A grid cell's image: the recessed cell art, or the game's cell with the look off.</summary>
        public static void Cell(Image image) => Cell(image, On);

        public static void Cell(Image image, bool on)
        {
            if (image == null)
                return;
            if (gameCell == null && image.sprite != null && image.sprite != Skin.Cell && image.sprite != KitArt.Cell)
                gameCell = image.sprite;
            Sprite recessed = KitArt.Cell;
            image.sprite = !on ? gameCell : recessed != null ? recessed : Skin.Cell;
            image.type = on && recessed != null ? Image.Type.Simple : Image.Type.Sliced;
        }

        /// <summary>The game's own sprite and material of a panel background, whatever it shows now.</summary>
        public static Sprite GameSprite(Image image) => image != null && panelSprites.TryGetValue(image, out Sprite kept) ? kept : image?.sprite;

        public static Material GameMaterial(Image image) => image != null && panelMaterials.TryGetValue(image, out Material kept) ? kept : image?.material;

        /// <summary>A panel's wood: its direct child named Bkg, as the game's scene names it.</summary>
        public static Image Background(RectTransform panel)
        {
            Transform child = panel != null ? panel.Find("Bkg") : null;
            return child != null ? child.GetComponent<Image>() : null;
        }

        /// <summary>Destroyed panels go: their remembered sprites go with them.</summary>
        private static void Prune()
        {
            gone.Clear();
            foreach (Image image in panelSprites.Keys)
                if (image == null)
                    gone.Add(image);
            foreach (Image image in owned)
                if (image == null)
                    gone.Add(image);
            foreach (Image image in gone)
            {
                panelSprites.Remove(image);
                panelMaterials.Remove(image);
                owned.Remove(image);
            }
            gone.Clear();
        }

        private static void Hook()
        {
            if (hooked)
                return;
            hooked = true;
            SceneManager.sceneLoaded += (scene, mode) => Prune();
        }
    }
}
