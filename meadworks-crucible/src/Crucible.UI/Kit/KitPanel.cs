using System.Collections.Generic;
using Jotunn.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>Where kit windows live and which font they write with.</summary>
    public static class KitCanvas
    {
        private static TMP_FontAsset font;

        /// <summary>
        /// A free-standing window's parent: Jotunn's front GUI layer, which follows the game's interface scale on the
        /// main menu and in a world; null headless or before the GUI exists. Panels that belong to a game panel (the
        /// inventory) are made under that panel instead, as PackPanel does.
        /// </summary>
        public static Transform Front => CrucibleUiPlugin.Active && GUIManager.CustomGUIFront != null ? GUIManager.CustomGUIFront.transform : null;

        /// <summary>
        /// The game's reading font (Averia Serif, with the game's fallbacks for other scripts) in real letter case. Until it
        /// is loaded, the game's first text font stands in and is not kept, so a later call still finds Averia.
        /// </summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (font != null)
                    return font;
                foreach (TMP_FontAsset asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                    if (asset != null && KitFontRules.IsReading(asset.name))
                        return font = asset;
                return Heading;
            }
        }

        /// <summary>The game's heading font: the first text under the inventory or main menu (the capitals-only Norse face).</summary>
        public static TMP_FontAsset Heading
        {
            get
            {
                if (heading != null)
                    return heading;
                heading = FontOf(InventoryGui.instance != null ? InventoryGui.instance.gameObject : null);
                if (heading == null)
                    heading = FontOf(FejdStartup.instance != null ? FejdStartup.instance.gameObject : null);
                return heading;
            }
        }

        private static TMP_FontAsset heading;

        private static TMP_FontAsset FontOf(GameObject root)
        {
            if (root == null)
                return null;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.font != null)
                    return text.font;
            return null;
        }

        /// <summary>A text child with the kit's restyling (PackPanel StatRows: font set before the text wakes).</summary>
        public static TMP_Text Text(Transform parent, string name, string words, float size, Color colour, TextAlignmentOptions alignment, bool wraps)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.SetActive(false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset asset = Font;
            if (asset != null)
                text.font = asset;
            go.SetActive(true);
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.color = colour;
            text.alignment = alignment;
            text.textWrappingMode = wraps ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = true;
            text.raycastTarget = false;
            text.text = words ?? "";
            return text;
        }

        /// <summary>
        /// Opt in to the button caption fitting for a single-line label: the text shrinks from its current size down to
        /// <see cref="KitButtonText.MinSize"/> (never above its own size) to fit its rect, then is cut at the edge (the game's
        /// fonts have no ellipsis glyph, and TextMeshPro warns on every Ellipsis text that lacks one).
        /// Call it after the label's rect has its final size; <see cref="Text"/> leaves fitting off so existing layouts
        /// that overflow on purpose keep their look.
        /// </summary>
        public static TMP_Text Fit(TMP_Text text)
        {
            if (text == null)
                return null;
            float size = text.fontSize;
            text.fontSizeMax = size;
            text.fontSizeMin = Mathf.Min(size, KitButtonText.MinSize);
            text.enableAutoSizing = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }
    }

    /// <summary>
    /// A kit panel: a rect holding the product's controls with a <c>Bkg</c> child reaching past it as the game's panels
    /// do (<see cref="KitSkin.Background(Image)"/>), drawn in the kit look (Brown or Timber, night shade) and the game's
    /// wood with the look off. Panels are re-skinned when a look setting changes.
    /// </summary>
    public static class KitPanel
    {
        private static readonly List<Image> panels = new List<Image>();
        private static readonly HashSet<Image> round = new HashSet<Image>();
        private static Sprite gameWood;
        private static bool hooked;

        /// <summary>A panel of <paramref name="size"/> under <paramref name="parent"/>, centered, optionally round; null headless.</summary>
        public static RectTransform Create(Transform parent, string name, Vector2 size, bool circle = false)
        {
            if (!CrucibleUiPlugin.Active || parent == null)
                return null;
            Hook();
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform panel = (RectTransform)go.transform;
            panel.SetParent(parent, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = size;
            GameObject back = new GameObject("Bkg", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)back.transform;
            rect.SetParent(panel, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            Image image = back.GetComponent<Image>();
            image.sprite = GameWood();
            image.type = Image.Type.Sliced;
            if (circle)
            {
                TimberBackground.RegisterRound(image);
                round.Add(image);
            }
            Skin(image);
            panels.Add(image);
            return panel;
        }

        /// <summary>The panel's background image (its <c>Bkg</c>).</summary>
        public static Image Background(RectTransform panel) => KitSkin.Background(panel);

        /// <summary>Applies the current look again (after a theme change, or after the product resized the panel).</summary>
        public static void Refresh(RectTransform panel)
        {
            Image image = KitSkin.Background(panel);
            if (image != null)
                Skin(image);
        }

        /// <summary>The kit skin; a round panel shows PackPanel's ring disc in the Brown look (its key ring pop-up).</summary>
        private static void Skin(Image image)
        {
            KitSkin.Background(image);
            KitForged.SkinSidePlate(image);
            if (round.Contains(image) && KitLook.On && !KitLook.Timber && KitArt.RingPanel != null)
            {
                image.sprite = KitArt.RingPanel;
                image.type = Image.Type.Simple;
            }
        }

        private static void RefreshAll()
        {
            for (int i = panels.Count - 1; i >= 0; i--)
            {
                if (panels[i] == null)
                {
                    panels.RemoveAt(i);
                    continue;
                }
                Skin(panels[i]);
            }
        }

        private static void Hook()
        {
            if (hooked)
                return;
            hooked = true;
            KitLook.Changed += RefreshAll;
        }

        /// <summary>The game's wood panel sprite for the look-off state: the inventory's own, else one found once.</summary>
        private static Sprite GameWood()
        {
            if (gameWood != null)
                return gameWood;
            Image inventory = InventoryGui.instance != null ? KitSkin.Background(InventoryGui.instance.m_player) : null;
            Sprite sprite = inventory != null ? KitSkin.GameSprite(inventory) : null;
            if (sprite != null && KitThemeRules.IsWoodSprite(sprite.name))
                return gameWood = sprite;
            foreach (Sprite candidate in Resources.FindObjectsOfTypeAll<Sprite>())
                if (candidate != null && KitThemeRules.IsWoodSprite(candidate.name))
                    return gameWood = candidate;
            return null;
        }
    }
}
