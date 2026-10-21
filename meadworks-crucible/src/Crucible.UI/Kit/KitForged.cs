// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. The forged set (user decisions 2026-10-13 09:49 and 09:52): MeadWorks' own CC0-built images
// (Art/source/build_panel_r5.py), the same wood, forged iron, orange rim and rivets as Cairn's compass plank (r4).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>The forged set's images. Window pieces and buttons are drawn at <see cref="WindowPixelsPerUnit"/> source pixels per UI unit.</summary>
    public static class KitForgedArt
    {
        /// <summary>Source pixels per UI unit of the window frame, inset, buttons and side plate (a 96-pixel bracket border is 48 units).</summary>
        public const float WindowPixelsPerUnit = KitAppearanceRules.ForgedPixelsPerUnit;

        /// <summary>Calm boards for kit windows (1024 x 1024, canvas-aligned like the Timber wallpaper, unsliced).</summary>
        public static Sprite WindowWood => KitHudArt.Read("window_wood", Vector4.zero);

        /// <summary>A window's thin forged-iron edge with riveted corner brackets; transparent center (320 x 320, 96-pixel border).</summary>
        public static Sprite WindowFrame => KitHudArt.Read("window_frame", new Vector4(96f, 96f, 96f, 96f));

        /// <summary>Dark-stained, sunk wood behind a reading area (512 x 384, 24-pixel border).</summary>
        public static Sprite WindowInset => KitHudArt.Read("window_inset", new Vector4(24f, 24f, 24f, 24f));

        /// <summary>Carved wood in an iron trim with a rivet at each end (256 x 80; 32-pixel ends, 20-pixel top and bottom).</summary>
        public static Sprite Button => KitHudArt.Read("button_wood", ButtonBorder);

        /// <summary>The button under the pointer or gamepad focus: the warm orange rim and a gold line inside the trim.</summary>
        public static Sprite ButtonHover => KitHudArt.Read("button_wood_hover", ButtonBorder);

        /// <summary>The pressed button: darker and sunk.</summary>
        public static Sprite ButtonPressed => KitHudArt.Read("button_wood_pressed", ButtonBorder);

        /// <summary>A button that cannot be used: faded, grayed wood and iron with no rim color.</summary>
        public static Sprite ButtonDisabled => KitHudArt.Read("button_wood_disabled", ButtonBorder);

        /// <summary>A small plate for HUD and map side panels: dark wood, a thin iron edge, riveted iron corners (192 x 192, 40-pixel border).</summary>
        public static Sprite SidePlate => KitHudArt.Read("side_plate", new Vector4(40f, 40f, 40f, 40f));

        /// <summary>A round wooden dial face in a riveted forged-iron ring (512 x 512, unsliced; the ring is 40 pixels, 7.8% of the width).</summary>
        public static Sprite DialFrame => KitHudArt.Read("dial_frame", Vector4.zero);

        /// <summary>A painted boar for an empty animal list (384 x 256, unsliced).</summary>
        public static Sprite EmptyBoar => KitHudArt.Read("empty_boar", Vector4.zero);

        static readonly Vector4 ButtonBorder = new Vector4(32f, 20f, 32f, 20f);
    }

    /// <summary>
    /// The forged look on kit surfaces: windows (calm wood, iron frame), the dark inset behind reading areas, wood buttons,
    /// an empty-state picture, side plates and a dial frame. Each follows <see cref="KitLook"/>: with the look off the game's
    /// own look comes back. Kit windows, kit buttons, <see cref="KitStatSheet"/> and <see cref="KitVirtualList"/> take it on
    /// their own; products call the rest.
    /// </summary>
    public static class KitForged
    {
        /// <summary>Inset padding round a reading area, in UI units.</summary>
        public const float InsetPad = 6f;

        const string FrameName = "Crucible_ForgedFrame", InsetName = "Crucible_ForgedInset", WindowPrefix = "Crucible_window_";
        static readonly Dictionary<Image, FrameLook> frames = new Dictionary<Image, FrameLook>();
        static readonly List<Image> insets = new List<Image>();
        static readonly Dictionary<Image, (Sprite Sprite, Material Material, Color Color, Image.Type Type, float Ppu)> sheetFills = new Dictionary<Image, (Sprite, Material, Color, Image.Type, float)>();
        static readonly Dictionary<Button, GameLook> buttons = new Dictionary<Button, GameLook>();
        static readonly HashSet<RectTransform> forgedWindows = new HashSet<RectTransform>();
        static bool hooked;

        sealed class GameLook
        {
            public Sprite Sprite; public Material Material; public Image.Type Type; public float Ppu;
            public Selectable.Transition Transition; public SpriteState State;
            public TMP_Text Caption; public TMP_FontAsset Font; public Material FontMaterial;
        }

        /// <summary>The look is on: kit windows and buttons wear the forged set.</summary>
        public static bool On => KitLook.On;

        /// <summary>Kit windows call this once: calm wood behind a straight edge, and the iron frame over it. Round windows keep the Timber disc.</summary>
        internal static void Window(RectTransform panel)
        {
            Image back = KitPanel.Background(panel);
            if (panel == null || back == null)
                return;
            Hook();
            forgedWindows.Add(panel);
            TimberBackground.RegisterForged(back);
            KitPanel.Refresh(panel);
            DressFrame(back, On, false);
        }

        /// <summary>
        /// A game panel themed by <see cref="GamePanelTheme"/> (inventory, chests, stations; user decision 2026-10-13 11:09) wears the
        /// forged set too: calm wood inside a straight edge and the iron frame, laid under the panel's own slots and text. Off restores.
        /// </summary>
        internal static void GamePanel(Image image, bool on)
        {
            if (!on) { TryReleaseGamePanel(image); return; }
            DressFrame(image, true, true);
        }

        internal static bool TryReleaseGamePanel(Image image)
        {
            if (image == null) return true;
            if (!frames.TryGetValue(image, out var look)) return image.transform.Find(FrameName) == null;
            if (look.Image == null) { frames.Remove(image); return image.transform.Find(FrameName) == null; }
            if (!look.Current) return false;
            Show(look.Image, false); look.Remember(); return true;
        }

        sealed class FrameLook
        {
            internal readonly Image Image;
            readonly Transform parent;
            Sprite sprite; Material material; Color color; Image.Type type; float ppu; bool visible;
            internal FrameLook(Image image) { Image = image; parent = image.transform.parent; Remember(); }
            internal bool Current => Image != null && Image.transform.parent == parent && Image.sprite == sprite && Image.material == material
                && Image.color == color && Image.type == type && Mathf.Approximately(Image.pixelsPerUnitMultiplier, ppu)
                && Image.gameObject.activeSelf == visible && !Image.raycastTarget;
            internal void Remember()
            { sprite = Image.sprite; material = Image.material; color = Image.color; type = Image.type; ppu = Image.pixelsPerUnitMultiplier; visible = Image.gameObject.activeSelf; }
        }

        static void DressFrame(Image back, bool on, bool fitToPanel)
        {
            if (back == null) return;
            bool known = frames.TryGetValue(back, out var look);
            if (known && !look.Current) { frames.Remove(back); return; }
            Sprite sprite = on ? KitAppearance.Sprite(KitSurfaceRole.Frame) : null;
            if (sprite == null)
            {
                if (known) { Show(look.Image, false); look.Remember(); }
                return;
            }
            Image frame;
            if (known) frame = look.Image;
            else
            {
                // A matching name alone is not ownership of another surface's overlay.
                if (back.transform.Find(FrameName) != null) return;
                frame = Overlay(back.rectTransform, FrameName, KitSurfaceRole.Frame);
                if (frame == null) return;
                look = new FrameLook(frame); frames.Add(back, look);
            }
            frame.sprite = sprite; frame.material = null; frame.type = Image.Type.Sliced; frame.color = Color.white;
            float ppu = KitAppearance.PixelsPerUnit(KitSurfaceRole.Frame);
            if (fitToPanel)
            {
                Vector2 size = back.rectTransform.rect.size;
                float side = Mathf.Min(size.x, size.y);
                Vector4 border = sprite.border;
                if (side > 0f) ppu = Mathf.Max(ppu, Mathf.Max(border.x + border.z, border.y + border.w) / side);
                frame.rectTransform.SetAsFirstSibling();
                Transform wood = back.transform.Find(KitThemeRules.WoodMarker);
                if (wood != null) wood.SetAsFirstSibling();
            }
            Fit(frame, ppu); Show(frame, true); look.Remember();
        }

        /// <summary>True when <paramref name="node"/> sits in a forged kit window (its stat sheets and lists then get the dark inset).</summary>
        public static bool InWindow(Transform node)
        {
            for (Transform at = node; at != null; at = at.parent)
                if (at is RectTransform rect && at.name.StartsWith(WindowPrefix) && forgedWindows.Contains(rect))
                    return true;
            return false;
        }

        /// <summary>
        /// The dark inset behind a reading area: a child of <paramref name="area"/> drawn first, reaching <paramref name="pad"/>
        /// units past it. Call once per area (a second call reuses the inset). Null headless; off or missing art hides the owned overlay.
        /// </summary>
        public static Image Inset(RectTransform area, float pad = InsetPad)
        {
            if (area == null || !CrucibleUiPlugin.Active)
                return null;
            Hook();
            Transform existing = area.Find(InsetName);
            if (existing != null)
                return existing.GetComponent<Image>();
            Image inset = Overlay(area, InsetName, KitSurfaceRole.Inset, true);
            if (inset == null)
                return null;
            inset.rectTransform.SetAsFirstSibling();
            inset.rectTransform.offsetMin = new Vector2(-pad, -pad);
            inset.rectTransform.offsetMax = new Vector2(pad, pad);
            insets.Add(inset);
            DressOverlay(inset, KitSurfaceRole.Inset);
            return inset;
        }

        /// <summary>A stat sheet's own dark fill becomes the inset (its rounded fill comes back with the look off).</summary>
        internal static void SheetFill(Image fill)
        {
            if (fill == null)
                return;
            Hook();
            if (!sheetFills.ContainsKey(fill))
                sheetFills[fill] = (fill.sprite, fill.material, fill.color, fill.type, fill.pixelsPerUnitMultiplier);
            DressSheet(fill, On);
        }

        /// <summary>
        /// A kit button in carved wood with an iron trim: hover (and gamepad focus) shows the warm rim and gold line, pressing sinks it.
        /// Dimming (<see cref="GameButtons.Tint"/>) still darkens it. Called again on a reused button it only refits the borders to its size.
        /// </summary>
        public static void Button(Button button)
        {
            if (button == null)
                return;
            Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image == null)
                return;
            Hook();
            if (!buttons.ContainsKey(button))
            {
                TMP_Text caption = button.GetComponentInChildren<TMP_Text>(true);
                buttons[button] = new GameLook { Sprite = image.sprite, Material = image.material, Type = image.type, Ppu = image.pixelsPerUnitMultiplier,
                    Transition = button.transition, State = button.spriteState, Caption = caption, Font = caption != null ? caption.font : null,
                    FontMaterial = caption != null ? caption.fontSharedMaterial : null };
            }
            DressButton(button, image, On);
        }

        /// <summary>
        /// An empty-state picture with a headline and a hint line, centered in <paramref name="area"/> (the reading area, usually with
        /// its inset); hidden until <see cref="KitEmptyState.Show"/>. Null headless.
        /// </summary>
        public static KitEmptyState EmptyState(RectTransform area, string name, Sprite picture, string headline, string hint, float pictureWidth = 200f)
        {
            if (area == null || !CrucibleUiPlugin.Active)
                return null;
            return new KitEmptyState(area, name, picture, headline, hint, pictureWidth);
        }

        static readonly HashSet<Image> sidePlates = new HashSet<Image>();
        static readonly Dictionary<Image, (float Original, float Assigned)> sidePlatePpus = new Dictionary<Image, (float, float)>();
        static readonly Dictionary<Image, (Sprite Sprite, Material Material, Color Color, Image.Type Type, float Ppu)> directPlates = new Dictionary<Image, (Sprite, Material, Color, Image.Type, float)>();

        /// <summary>
        /// A <see cref="KitPanel"/> for a HUD or map side panel uses the current Card role after every re-skin;
        /// the game's wood comes back with the look off.
        /// </summary>
        public static void SidePlate(RectTransform panel)
        {
            Image back = KitPanel.Background(panel);
            if (back == null)
                return;
            Hook();
            sidePlates.RemoveWhere(known => known == null);
            sidePlates.Add(back);
            if (!sidePlatePpus.ContainsKey(back)) sidePlatePpus.Add(back, (back.pixelsPerUnitMultiplier, back.pixelsPerUnitMultiplier));
            KitPanel.Refresh(panel);
        }

        /// <summary>KitPanel's skin step: a registered side plate is dressed while the look is on.</summary>
        internal static void SkinSidePlate(Image back)
        {
            if (back == null || !sidePlates.Contains(back) || !sidePlatePpus.TryGetValue(back, out var ppu)) return;
            if (back.pixelsPerUnitMultiplier != ppu.Assigned) return;
            if (!On) { back.pixelsPerUnitMultiplier = ppu.Original; sidePlatePpus[back] = (ppu.Original, ppu.Original); return; }
            TimberBackground.Apply(back, false);
            if (DressPlate(back)) sidePlatePpus[back] = (ppu.Original, back.pixelsPerUnitMultiplier);
            else { KitSkin.Panel(back, false); back.pixelsPerUnitMultiplier = ppu.Original; sidePlatePpus[back] = (ppu.Original, ppu.Original); }
        }

        /// <summary>Dresses an owned image with the current Card role; off or missing art restores its original presentation.</summary>
        public static bool SidePlate(Image image)
        {
            if (image == null) return false;
            Hook();
            if (!directPlates.ContainsKey(image)) directPlates[image] = (image.sprite, image.material, image.color, image.type, image.pixelsPerUnitMultiplier);
            if (DressPlate(image)) return true;
            var game = directPlates[image];
            image.sprite = game.Sprite; image.material = game.Material; image.color = game.Color; image.type = game.Type; image.pixelsPerUnitMultiplier = game.Ppu;
            return false;
        }

        static bool DressPlate(Image image)
        {
            Sprite plate = KitAppearance.Sprite(KitSurfaceRole.Card);
            if (!On || plate == null) return false;
            image.sprite = plate; image.material = null; image.type = Image.Type.Sliced; image.color = Color.white;
            Fit(image, KitAppearance.PixelsPerUnit(KitSurfaceRole.Card)); NightShade.Track(image);
            return true;
        }

        /// <summary>Dresses a round dial's background as the iron-rimmed wooden dial (aspect kept). False when the image is missing.</summary>
        public static bool DialFrame(Image image)
        {
            Sprite dial = KitForgedArt.DialFrame;
            if (image == null || dial == null)
                return false;
            image.sprite = dial;
            image.material = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            NightShade.Track(image);
            return true;
        }

        /// <summary>The share of a dial frame's width taken by its iron ring on each side (draw the needle or rose inside it).</summary>
        public const float DialRing = 42f / 512f;

        static Image Overlay(RectTransform parent, string name, KitSurfaceRole role, bool createWithoutArt = false)
        {
            Sprite sprite = KitAppearance.Sprite(role);
            if (parent == null || sprite == null && !createWithoutArt)
                return null;
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            image.color = Color.white;
            Fit(image, KitAppearance.PixelsPerUnit(role));
            NightShade.Track(image);
            return image;
        }

        /// <summary>Source pixels per UI unit, whatever the canvas's reference and the sprite's own scale.</summary>
        static void Fit(Image image, float pixelsPerUnit) => KitAppearance.Fit(image, pixelsPerUnit);

        static void Show(Image image, bool on)
        {
            if (image != null && image.gameObject.activeSelf != on)
                image.gameObject.SetActive(on);
        }

        static void DressOverlay(Image image, KitSurfaceRole role)
        {
            Sprite sprite = KitAppearance.Sprite(role);
            if (On && sprite != null)
            {
                image.sprite = sprite; image.type = Image.Type.Sliced; image.color = Color.white;
                Fit(image, KitAppearance.PixelsPerUnit(role));
            }
            Show(image, On && sprite != null);
        }

        static void DressSheet(Image fill, bool on)
        {
            if (!sheetFills.TryGetValue(fill, out var game)) return;
            Sprite inset = on ? KitAppearance.Sprite(KitSurfaceRole.Inset) : null;
            if (inset != null)
            {
                fill.sprite = inset; fill.material = null; fill.type = Image.Type.Sliced; fill.color = Color.white;
                Fit(fill, KitAppearance.PixelsPerUnit(KitSurfaceRole.Inset)); NightShade.Track(fill);
            }
            else
            {
                fill.sprite = game.Sprite; fill.material = game.Material; fill.type = game.Type; fill.color = game.Color; fill.pixelsPerUnitMultiplier = game.Ppu;
            }
        }

        static void DressButton(Button button, Image image, bool on)
        {
            GameLook game = buttons[button];
            Sprite normal = on ? KitAppearance.Sprite(KitSurfaceRole.Button) : null;
            Sprite hover = on ? KitAppearance.Sprite(KitSurfaceRole.ButtonHover) : null;
            Sprite pressed = on ? KitAppearance.Sprite(KitSurfaceRole.ButtonPressed) : null;
            Sprite disabled = on ? KitAppearance.Sprite(KitSurfaceRole.ButtonDisabled) : null;
            on = on && normal != null && hover != null && pressed != null && disabled != null;
            if (game.Caption != null)
            {
                game.Caption.font = on ? KitAppearance.Font(KitFontRole.Body) ?? game.Font : game.Font;
                if (!on) game.Caption.fontSharedMaterial = game.FontMaterial;
            }
            if (!on)
            {
                image.sprite = game.Sprite;
                image.material = game.Material;
                image.type = game.Type;
                image.pixelsPerUnitMultiplier = game.Ppu;
                button.transition = game.Transition;
                button.spriteState = game.State;
                return;
            }
            image.sprite = normal;
            image.material = null;
            image.type = Image.Type.Sliced;
            // Small buttons (the steppers' 28-unit squares) take finer source pixels so both rivet ends still fit.
            Vector2 size = ((RectTransform)button.transform).rect.size;
            float ppu = ButtonPpu(normal, KitSurfaceRole.Button, size);
            ppu = Mathf.Max(ppu, ButtonPpu(hover, KitSurfaceRole.ButtonHover, size));
            ppu = Mathf.Max(ppu, ButtonPpu(pressed, KitSurfaceRole.ButtonPressed, size));
            ppu = Mathf.Max(ppu, ButtonPpu(disabled, KitSurfaceRole.ButtonDisabled, size));
            Fit(image, ppu);
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = hover,
                selectedSprite = hover,
                pressedSprite = pressed,
                disabledSprite = disabled
            };
        }

        static float ButtonPpu(Sprite sprite, KitSurfaceRole role, Vector2 size)
        {
            float ppu = KitAppearance.PixelsPerUnit(role);
            Vector4 border = sprite.border;
            if (size.x > 0f) ppu = Mathf.Max(ppu, (border.x + border.z) / Mathf.Max(8f, size.x - 4f));
            if (size.y > 0f) ppu = Mathf.Max(ppu, (border.y + border.w) / Mathf.Max(8f, size.y - 2f));
            return ppu;
        }

        internal static void Prune()
        {
            var goneFrames = new List<Image>();
            foreach (var pair in frames) if (pair.Key == null || pair.Value.Image == null) goneFrames.Add(pair.Key);
            foreach (Image image in goneFrames) frames.Remove(image);
            sidePlates.RemoveWhere(image => image == null);
            var goneSidePpus = new List<Image>();
            foreach (Image image in sidePlatePpus.Keys) if (image == null) goneSidePpus.Add(image);
            foreach (Image image in goneSidePpus) sidePlatePpus.Remove(image);
            var gonePlates = new List<Image>();
            foreach (Image image in directPlates.Keys) if (image == null) gonePlates.Add(image);
            foreach (Image image in gonePlates) directPlates.Remove(image);
            insets.RemoveAll(image => image == null);
            forgedWindows.RemoveWhere(panel => panel == null);
            var goneFills = new List<Image>();
            foreach (Image fill in sheetFills.Keys) if (fill == null) goneFills.Add(fill);
            foreach (Image fill in goneFills) sheetFills.Remove(fill);
            var goneButtons = new List<Button>();
            foreach (Button button in buttons.Keys) if (button == null) goneButtons.Add(button);
            foreach (Button button in goneButtons) buttons.Remove(button);
        }

        static void RefreshAll()
        {
            Prune();
            bool on = On;
            foreach (RectTransform panel in forgedWindows) DressFrame(KitPanel.Background(panel), on, false);
            foreach (Image image in insets) DressOverlay(image, KitSurfaceRole.Inset);
            foreach (Image image in new List<Image>(directPlates.Keys)) SidePlate(image);
            foreach (Image fill in new List<Image>(sheetFills.Keys)) DressSheet(fill, on);
            foreach (Button button in new List<Button>(buttons.Keys))
            {
                Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
                if (image != null) DressButton(button, image, on);
            }
        }

        static void Hook()
        {
            if (hooked)
                return;
            hooked = true;
            KitLook.Changed += RefreshAll;
        }
    }

    /// <summary>
    /// A painted picture with a headline and a hint, centered in a reading area, for a list with nothing to show
    /// (<see cref="KitForged.EmptyState"/>). Averia text; the picture is hidden with the look off, the words stay.
    /// </summary>
    public sealed class KitEmptyState
    {
        public RectTransform Root { get; }
        public Image Picture { get; }
        public TMP_Text Headline { get; }
        public TMP_Text Hint { get; }

        internal KitEmptyState(RectTransform area, string name, Sprite picture, string headline, string hint, float pictureWidth)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = area.gameObject.layer;
            Root = (RectTransform)go.transform;
            Root.SetParent(area, false);
            Root.anchorMin = Vector2.zero;
            Root.anchorMax = Vector2.one;
            Root.offsetMin = Root.offsetMax = Vector2.zero;
            float aspect = picture != null && picture.rect.width > 0f ? picture.rect.height / picture.rect.width : 2f / 3f;
            float pictureHeight = picture != null ? pictureWidth * aspect : 0f;
            const float gap = 10f, headSize = 18f, hintSize = 14f;
            float block = pictureHeight + (pictureHeight > 0f ? gap : 0f) + headSize + 6f + hintSize * 2.4f;
            float top = -block / 2f;
            if (picture != null)
            {
                var pic = new GameObject("Picture", typeof(RectTransform), typeof(Image));
                pic.layer = go.layer;
                pic.transform.SetParent(Root, false);
                Picture = pic.GetComponent<Image>();
                Picture.sprite = picture;
                Picture.preserveAspect = true;
                Picture.raycastTarget = false;
                Place(Picture.rectTransform, top, pictureWidth, pictureHeight);
                NightShade.Track(Picture);
                top += pictureHeight + gap;
            }
            Headline = KitCanvas.Text(Root, "Headline", headline, headSize, KitUnity.Colour(KitPalette.TabLitText), TextAlignmentOptions.Center, false);
            Place(Headline.rectTransform, top, 0f, headSize + 6f);
            top += headSize + 6f;
            Hint = KitCanvas.Text(Root, "Hint", hint, hintSize, new Color(0.86f, 0.80f, 0.68f, 0.85f), TextAlignmentOptions.Top, true);
            Place(Hint.rectTransform, top, 0f, hintSize * 2.4f, 24f);
            go.SetActive(false);
        }

        /// <summary>Shows or hides the empty state (the product shows it while its list is empty).</summary>
        public void Show(bool shown)
        {
            if (Root == null)
                return;
            if (Root.gameObject.activeSelf != shown)
                Root.gameObject.SetActive(shown);
            if (Picture != null)
                Picture.enabled = KitLook.On;
        }

        /// <summary>New words for the headline and hint (for a product whose wording depends on a setting, such as the search radius).</summary>
        public void Words(string headline, string hint)
        {
            if (Headline != null) Headline.text = headline ?? "";
            if (Hint != null) Hint.text = hint ?? "";
        }

        // Centered horizontally (width 0: the whole area less the margin), top measured from the area's middle downwards.
        static void Place(RectTransform rect, float top, float width, float height, float margin = 12f)
        {
            rect.anchorMin = new Vector2(width > 0f ? 0.5f : 0f, 0.5f);
            rect.anchorMax = new Vector2(width > 0f ? 0.5f : 1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(width > 0f ? width : -margin * 2f, height);
        }
    }
}
