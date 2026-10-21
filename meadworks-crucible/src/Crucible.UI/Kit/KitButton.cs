using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// Kit buttons, PackPanel's way: a copy of a game button (<see cref="GameButtons"/>: the inventory's take-all button
    /// in a world, the character screen's Start button on the main menu), so sprite, font, sound and hover follow the
    /// game in every theme. Only when no game button exists is one drawn from the kit's own button art (PackPanel ships
    /// it but its 0.12.0 panels never needed it). A blocked option (C03) disables the button and its tooltip says why.
    /// </summary>
    public static class KitButton
    {
        /// <summary>A game button to copy: the inventory's, else the main menu's character Start button.</summary>
        public static Button Source
        {
            get
            {
                Button inventory = GameButtons.Source;
                if (inventory != null)
                    return inventory;
                FejdStartup menu = FejdStartup.instance;
                return menu != null && menu.m_csStartButton != null ? menu.m_csStartButton : null;
            }
        }

        /// <summary>
        /// A button under <paramref name="parent"/>, top-left at <paramref name="at"/>; null headless. A live child of the
        /// same name is reused; one retired with <see cref="KitUnity.Retire"/> this frame never is, so destroy kit buttons
        /// with <see cref="KitUnity.Retire"/> (a plain <c>Object.Destroy</c> leaves the child findable until the frame ends).
        /// </summary>
        public static Button Create(RectTransform parent, string name, string caption, Action click, Vector2 at, Vector2 size)
        {
            if (!CrucibleUiPlugin.Active || parent == null)
                return null;
            Transform existing = KitUnity.FindLive(parent, name);
            Button button = existing != null ? existing.GetComponent<Button>() : null;
            if (button == null)
            {
                Button source = Source;
                button = source != null ? GameButtons.Make(source, parent, name, caption, click) : Drawn(parent, name, caption, click);
            }
            GameButtons.Set(button, at, size.x, size.y, true, true);
            return button;
        }

        /// <summary>Shows a decision: clickable or not, dimmed or lit, and the reason tooltip (empty clears it).</summary>
        public static void Apply(Button button, KitControlDecision decision)
        {
            if (button == null)
                return;
            button.interactable = decision.Interactable;
            GameButtons.Tint(button, !decision.Dimmed);
            if (decision.Tooltip.Length > 0)
                KitTooltip.Set(button.gameObject, decision.TooltipTopic, decision.Tooltip);
            else if (button.GetComponent<UITooltip>() is UITooltip tip)
                tip.Set("", "");
        }

        /// <summary>The button for a product option: usable unless the option is blocked (C03), with the reason as tooltip.</summary>
        public static KitControlDecision Block(Button button, BlockedOption option, bool usable = true)
        {
            KitControlDecision decision = KitControls.Decide(usable, option, CrucibleUiPlugin.Language);
            Apply(button, decision);
            return decision;
        }

        /// <summary>A button drawn from the kit's art when the game has none to copy (Brown: sprite swap; Timber: tint).</summary>
        private static Button Drawn(RectTransform parent, string name, string caption, Action click)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = KitArt.Button;
            image.type = Image.Type.Sliced;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (!KitArt.Timber && KitArt.ButtonHover != null && KitArt.ButtonPressed != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState { highlightedSprite = KitArt.ButtonHover, selectedSprite = KitArt.ButtonHover, pressedSprite = KitArt.ButtonPressed };
            }
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                try { click?.Invoke(); }
                catch (Exception e) { CrucibleUiPlugin.Log?.Error($"Crucible.UI: button {name} failed: {e}"); }
            });
            TMP_Text text = KitCanvas.Text(go.transform, "Text", "", KitButtonText.MaxSize, Color.white, TextAlignmentOptions.Center, false);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            GameButtons.Caption(go, caption);
            return button;
        }
    }

    /// <summary>One tab of a <see cref="KitTabs"/> strip.</summary>
    public sealed class KitTab
    {
        public string Id;
        public string Caption;
        public Action Click;
        public bool Usable = true;
        /// <summary>When blocked (C03), the tab is dimmed, unclickable and its tooltip gives the reason.</summary>
        public BlockedOption Blocked;
    }

    /// <summary>
    /// A strip of tab buttons across a panel's top (PackPanel's Gear and Consumables tabs, for any number of tabs):
    /// copies of a game button, <see cref="KitTabLayout.Height"/> high, equal widths <see cref="KitTabLayout.Between"/>
    /// apart; the shown tab lit, the others dimmed.
    /// </summary>
    public static partial class KitTabs
    {
        public static List<Button> Place(RectTransform panel, IList<KitTab> tabs, string shown, float left, float top, float width)
        {
            List<Button> buttons = new List<Button>();
            if (!CrucibleUiPlugin.Active || panel == null || tabs == null)
                return buttons;
            IReadOnlyList<KitTabPlace> places = KitTabLayout.Place(width, tabs.Count, left);
            for (int i = 0; i < tabs.Count; i++)
            {
                KitTab tab = tabs[i];
                Button button = KitButton.Create(panel, "Crucible_tab_" + tab.Id, tab.Caption, tab.Click,
                    new Vector2(places[i].X, -top), new Vector2(places[i].Width, KitTabLayout.Height));
                if (button == null)
                    continue;
                KitControlDecision decision = KitControls.Decide(tab.Usable, tab.Blocked, CrucibleUiPlugin.Language);
                KitButton.Apply(button, decision);
                bool lit = KitControls.Tab(tab.Id == shown, decision.Interactable) == KitTabVisual.Lit;
                GameButtons.Tint(button, lit);
                buttons.Add(button);
            }
            return buttons;
        }
    }
}
