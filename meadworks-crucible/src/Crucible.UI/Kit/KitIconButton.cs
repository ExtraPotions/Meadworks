using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// KX02: a square <see cref="KitButton"/> (a copy of a game button, so sprite, sound and hover follow the game in
    /// every look) with a centered glyph instead of a caption, inset <see cref="KitIconLayout.Inset"/> and drawn in the
    /// button's text color. A click arrives as plain, Shift or Ctrl (<see cref="KitClicks"/>, modifiers through the C07
    /// input service); the wheel over the button can step too. An on/off button (favorites only; Almanac's toggle AX05)
    /// shows on as lit and off as dimmed. Any size. Null on a headless process (C10).
    /// </summary>
    public sealed class KitIconButton
    {
        private bool on = true;
        private bool usable = true;

        public Button Button { get; }
        public Image Glyph { get; }
        public KitGlyph Shape { get; private set; }
        public bool On => on;

        /// <summary>A click with its modifier.</summary>
        public event Action<KitClickKind> Clicked;

        private KitIconButton(Button button, Image glyph, KitGlyph shape)
        {
            Button = button;
            Glyph = glyph;
            Shape = shape;
        }

        /// <summary>A <paramref name="size"/> square icon button under <paramref name="parent"/>, top-left at <paramref name="at"/>; null headless.</summary>
        public static KitIconButton Create(RectTransform parent, string name, KitGlyph glyph, Vector2 at, float size, Action<KitClickKind> click = null)
        {
            KitIconButton made = null;
            Button button = KitButton.Create(parent, name, "", () => made?.Click(), at, new Vector2(size, size));
            if (button == null)
                return null;
            Image image = GlyphImage(button.transform, size);
            made = new KitIconButton(button, image, glyph);
            if (click != null)
                made.Clicked += click;
            made.SetGlyph(glyph);
            made.Show();
            return made;
        }

        public void SetGlyph(KitGlyph glyph)
        {
            Shape = glyph;
            if (Glyph != null)
                Glyph.sprite = KitGlyphSprites.Of(glyph);
        }

        /// <summary>Shows a game sprite instead of a kit glyph (the build menu's favorite star; the kit star is the fallback).</summary>
        public void SetSprite(Sprite sprite, bool tinted = true)
        {
            if (Glyph == null)
                return;
            Glyph.sprite = sprite != null ? sprite : KitGlyphSprites.Of(Shape);
            Glyph.preserveAspect = true;
            if (!tinted)
                Glyph.color = Color.white;
        }

        /// <summary>On (lit) or off (dimmed), for toggles.</summary>
        public void SetOn(bool value)
        {
            on = value;
            Show();
        }

        public void SetUsable(bool value)
        {
            usable = value;
            if (Button != null)
                Button.interactable = value;
            Show();
        }

        public void Tooltip(string topic, string text) => KitTooltip.Set(Button != null ? Button.gameObject : null, topic, text);

        /// <summary>A blocked option (C03): unclickable, dimmed, the reason as tooltip.</summary>
        public KitControlDecision Block(BlockedOption option, bool usable = true)
        {
            KitControlDecision decision = KitControls.Decide(usable, option, CrucibleUiPlugin.Language);
            KitButton.Apply(Button, decision);
            this.usable = decision.Interactable;
            Show();
            return decision;
        }

        /// <summary>The wheel over the button: +1 up, -1 down (<see cref="KitClicks.Wheel"/>).</summary>
        public void Wheel(Action<int> step) => KitWheel.Add(Button != null ? Button.gameObject : null, step);

        private void Click()
        {
            Clicked?.Invoke(KitClicks.Of(MeadworksInput.ShiftHeld, MeadworksInput.CtrlHeld));
        }

        private void Show()
        {
            if (Button == null)
                return;
            KitTabVisual visual = KitClicks.Toggle(on, usable);
            GameButtons.Tint(Button, visual == KitTabVisual.Lit);
            if (Glyph != null)
                Glyph.color = KitUnity.Colour(KitControls.TextColour(visual));
        }

        private static Image GlyphImage(Transform button, float size)
        {
            Transform existing = KitUnity.FindLive(button, "Crucible_glyph");
            if (existing != null && existing.GetComponent<Image>() is Image kept)
                return kept;
            GameObject go = new GameObject("Crucible_glyph", typeof(RectTransform), typeof(Image));
            go.layer = button.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(button, false);
            KitRect place = KitIconLayout.Glyph(new KitVector(size, size));
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(place.Width, place.Height);
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }
    }

    /// <summary>The kit glyphs as sprites, painted once from the Core pixel rules (<see cref="KitGlyphs"/>).</summary>
    public static class KitGlyphSprites
    {
        private static readonly Dictionary<KitGlyph, Sprite> sprites = new Dictionary<KitGlyph, Sprite>();

        public static Sprite Of(KitGlyph glyph)
        {
            if (sprites.TryGetValue(glyph, out Sprite made) && made != null)
                return made;
            string name = KitGlyphs.SpriteName(glyph);
            Texture2D texture = new Texture2D(KitGlyphs.Size, KitGlyphs.Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            texture.SetPixels32(KitPaintUnity.White(KitGlyphs.Alpha(glyph)));
            texture.Apply(false, true);
            made = Sprite.Create(texture, new Rect(0f, 0f, KitGlyphs.Size, KitGlyphs.Size), new Vector2(0.5f, 0.5f), 100f);
            made.name = name;
            made.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprites[glyph] = made;
        }
    }

    /// <summary>
    /// The mouse wheel over one kit part: +1 up, -1 down. On each part rather than a row, since a field passes the wheel
    /// to its parent and a handler on the row would then step twice.
    /// </summary>
    public sealed class KitWheel : MonoBehaviour, IScrollHandler
    {
        private Action<int> step;

        public static KitWheel Add(GameObject part, Action<int> step)
        {
            if (part == null || step == null)
                return null;
            KitWheel wheel = part.GetComponent<KitWheel>();
            if (wheel == null)
                wheel = part.AddComponent<KitWheel>();
            wheel.step = step;
            return wheel;
        }

        public void OnScroll(PointerEventData data)
        {
            int direction = KitClicks.Wheel(data.scrollDelta.y);
            if (direction == 0 || step == null)
                return;
            try
            {
                step(direction);
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Error($"Crucible.UI: wheel on {name} failed: {e}");
            }
        }
    }
}
