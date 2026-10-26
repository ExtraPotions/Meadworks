// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>Presentation for the native notification hosts; their queues, journal and animations remain native.</summary>
    public static class KitNativeNotices
    {
        static class Native
        {
            internal static readonly AccessTools.FieldRef<MessageHud, List<GameObject>> Unlocks =
                AccessTools.FieldRefAccess<MessageHud, List<GameObject>>("m_unlockMessages");
            internal static readonly AccessTools.FieldRef<MessageHud, GameObject> Biome =
                AccessTools.FieldRefAccess<MessageHud, GameObject>("m_biomeMsgInstance");
        }

        static readonly List<Surface> surfaces = new List<Surface>();
        static readonly List<NoticeWrapping> wrapping = new List<NoticeWrapping>();
        static MessageHud host;
        static bool initialized, dirty;

        public static void Initialize()
        {
            if (initialized || !MeadworksHeadless.AllowsUi) return;
            initialized = true; dirty = true;
            KitAppearance.Changed += Changed;
            SceneManager.activeSceneChanged += SceneChanged;
        }

        static void Changed() => dirty = true;
        static void SceneChanged(Scene before, Scene after) { Clear(); host = null; dirty = true; }

        /// <summary>Call after MessageHud.Update so native text and fades have advanced.</summary>
        public static void Tick()
        {
            if (!initialized || !MeadworksHeadless.AllowsUi) return;
            MessageHud next = MessageHud.instance;
            if (host != next)
            {
                Clear(); host = next;
                if (host != null)
                {
                    if (host.m_messageText != null) wrapping.Add(new NoticeWrapping(host.m_messageText, false));
                    if (host.m_messageCenterText != null) wrapping.Add(new NoticeWrapping(host.m_messageCenterText, true));
                    Add(host.m_messageText, null, null);
                    Add(host.m_messageCenterText, null, null);
                }
            }
            if (host == null) return;
            for (int i = 0; i < wrapping.Count; i++) wrapping[i].Refresh();
            List<GameObject> unlocks = Native.Unlocks(host);
            for (int i = 0; i < unlocks.Count; i++)
            {
                GameObject root = unlocks[i];
                if (root == null || Has(root)) continue;
                Transform title = root.transform.Find("UnlockMessage/UnlockTitle");
                Transform body = root.transform.Find("UnlockMessage/UnlockDescription");
                Transform box = root.transform.Find("UnlockMessage/bkg");
                if (title != null && body != null)
                    Add(title.GetComponent<TMP_Text>(), body.GetComponent<TMP_Text>(), root, box != null ? box.GetComponent<Image>() : null);
            }
            GameObject biome = Native.Biome(host);
            if (biome != null && !Has(biome))
            {
                Transform title = Utils.FindChild(biome.transform, "Title");
                if (title != null) Add(title.GetComponent<TMP_Text>(), null, biome);
            }
            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                Surface surface = surfaces[i];
                if (!surface.Alive) { surface.Dispose(); surfaces.RemoveAt(i); }
                else surface.Refresh(dirty);
            }
            dirty = false;
        }

        /// <summary>The MeadWorks card drawn for a native notice text, while one exists (for layout checks).</summary>
        public static RectTransform FrameOf(TMP_Text text)
        {
            for (int i = 0; i < surfaces.Count; i++) if (surfaces[i].Owns(text)) return surfaces[i].Frame;
            return null;
        }

        /// <summary>Screen rectangles of the game's own visible notice and unlock cards, so other MeadWorks HUD can stay off them.</summary>
        internal static void AppendShownRects(List<Rect> into)
        {
            if (into == null) return;
            for (int i = 0; i < surfaces.Count; i++)
            {
                RectTransform frame = surfaces[i].Frame;
                if (frame != null && frame.gameObject.activeInHierarchy && KitHudLayout.ScreenRect(frame, out Rect framed))
                    into.Add(framed);
            }
            if (host == null) return;
            List<GameObject> unlocks = Native.Unlocks(host);
            if (unlocks == null) return;
            for (int i = 0; i < unlocks.Count; i++)
            {
                GameObject root = unlocks[i];
                if (root == null || !root.activeInHierarchy || FrameFor(root) != null) continue;
                var box = root.transform.Find("UnlockMessage/bkg") as RectTransform;
                if (box == null) box = root.transform.Find("UnlockMessage") as RectTransform;
                if (box != null && KitHudLayout.ScreenRect(box, out Rect rect)) into.Add(rect);
            }
        }

        static RectTransform FrameFor(GameObject root)
        {
            for (int i = 0; i < surfaces.Count; i++)
                if (surfaces[i].NativeRoot == root && surfaces[i].Frame != null && surfaces[i].Frame.gameObject.activeInHierarchy)
                    return surfaces[i].Frame;
            return null;
        }

        static bool Has(GameObject root)
        {
            for (int i = 0; i < surfaces.Count; i++) if (surfaces[i].NativeRoot == root) return true;
            return false;
        }

        static void Add(TMP_Text title, TMP_Text body, GameObject root, Image box = null)
        {
            if (title != null) surfaces.Add(new Surface(title, body, root, box));
        }

        static void Clear()
        {
            for (int i = 0; i < surfaces.Count; i++) surfaces[i].Dispose();
            surfaces.Clear();
            for (int i = 0; i < wrapping.Count; i++) wrapping[i].Restore();
            wrapping.Clear();
        }

        public static void Shutdown()
        {
            KitAppearance.Changed -= Changed;
            SceneManager.activeSceneChanged -= SceneChanged;
            Clear(); host = null; initialized = dirty = false;
        }

        sealed class NoticeWrapping
        {
            readonly TMP_Text text;
            readonly bool center;
            readonly TextWrappingModes originalWrapping;
            readonly float originalWidthDelta;
            float appliedWidthDelta;
            bool ownsWrapping = true, ownsWidth = true;

            internal NoticeWrapping(TMP_Text text, bool center)
            {
                this.text = text; this.center = center;
                originalWrapping = text.textWrappingMode;
                originalWidthDelta = appliedWidthDelta = text.rectTransform.sizeDelta.x;
                text.textWrappingMode = TextWrappingModes.Normal;
            }

            internal void Refresh()
            {
                if (text == null) return;
                // A later writer owns its changes; do not fight or restore over another mod.
                if (text.textWrappingMode != TextWrappingModes.Normal) ownsWrapping = false;
                var rect = text.rectTransform;
                if (rect.sizeDelta.x != appliedWidthDelta) ownsWidth = false;
                if (!ownsWidth || !ownsWrapping || Screen.width <= 0) return;
                var canvas = text.canvas;
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && camera == null) return;
                Rect safe = Screen.safeArea;
                if (safe.width <= 32f) return;
                Vector2 pivotScreen = RectTransformUtility.WorldToScreenPoint(camera, rect.position);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, new Vector2(safe.xMin + 16f, pivotScreen.y), camera, out Vector2 left)
                    || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, new Vector2(safe.xMax - 16f, pivotScreen.y), camera, out Vector2 right)) return;
                float span = right.x - left.x;
                if (span <= 0f || left.x >= 0f || right.x <= 0f) return;
                float maxPixels = center ? Mathf.Min(Screen.width * .8f, 960f) : Mathf.Min(Screen.width * .45f, 520f);
                float width = maxPixels * span / (safe.width - 32f);
                if (rect.pivot.x > 0f) width = Mathf.Min(width, -left.x / rect.pivot.x);
                if (rect.pivot.x < 1f) width = Mathf.Min(width, right.x / (1f - rect.pivot.x));
                // The native center label is narrow because NoWrap ignored its rect. Let long center text use the cap.
                // The top-left label retains the existing icon/text column width.
                float nativeWidth = rect.rect.width + originalWidthDelta - appliedWidthDelta;
                if (nativeWidth > 0f) width = Mathf.Min(width, center ? Mathf.Max(nativeWidth, text.preferredWidth) : nativeWidth);
                if (width <= 0f || Mathf.Approximately(width, rect.rect.width)) return;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                appliedWidthDelta = rect.sizeDelta.x;
            }

            internal void Restore()
            {
                if (text == null) return;
                if (ownsWrapping && text.textWrappingMode == TextWrappingModes.Normal) text.textWrappingMode = originalWrapping;
                var rect = text.rectTransform;
                if (ownsWidth && rect.sizeDelta.x == appliedWidthDelta)
                    rect.sizeDelta = new Vector2(originalWidthDelta, rect.sizeDelta.y);
            }
        }

        sealed class Label
        {
            internal readonly TMP_Text Text;
            readonly TMP_FontAsset font;
            readonly Material material;
            readonly Color color;
            string content;
            Rect rect;
            Vector3 position, scale;
            internal Label(TMP_Text text)
            { Text = text; font = text.font; material = text.fontSharedMaterial; color = text.color; }
            internal void Apply(bool on)
            {
                if (Text == null) return;
                TMP_FontAsset selected = on ? KitAppearance.Font(KitFontRole.Body) : null;
                Text.font = selected != null ? selected : font;
                Text.fontSharedMaterial = selected != null ? selected.material : material;
                Color next = on ? KitAppearance.Color(KitColorRole.Text) : color;
                // Native animations own the live alpha independently of the chosen text color.
                next.a = Text.color.a; Text.color = next;
            }
            internal bool Changed()
            {
                var transform = Text.rectTransform;
                bool changed = content != Text.text || rect != transform.rect || position != transform.localPosition || scale != transform.localScale;
                content = Text.text; rect = transform.rect; position = transform.localPosition; scale = transform.localScale;
                return changed;
            }
            internal void Restore()
            {
                if (Text == null) return;
                Text.font = font; Text.fontSharedMaterial = material;
                Color next = color; next.a = Text.color.a; Text.color = next;
            }
        }

        sealed class Surface
        {
            internal readonly GameObject NativeRoot;
            readonly Label first, second;
            readonly Animator animator;
            // Unlock messages draw a native translucent box (bkg) behind the icon and both texts; the card replaces it
            // at exactly that rect so the icon sits inside the card. Text-only notices keep the text-bounds card.
            readonly Image box;
            readonly Color boxColor;
            bool boxHidden;
            RectTransform frame;
            CanvasGroup opacity;
            bool fresh = true, styled;
            internal RectTransform Frame => frame;
            internal bool Owns(TMP_Text text) => text != null && (first.Text == text || second != null && second.Text == text);
            internal bool Alive =>first.Text != null && (ReferenceEquals(NativeRoot, null) || NativeRoot != null);
            internal Surface(TMP_Text title, TMP_Text body, GameObject root, Image box)
            {
                NativeRoot = root; first = new Label(title); second = body != null ? new Label(body) : null;
                if (root != null) animator = root.GetComponentInChildren<Animator>();
                if (box != null && box.transform.parent == title.transform.parent) { this.box = box; boxColor = box.color; }
            }
            void ShowBox(bool native)
            {
                if (box == null || boxHidden == !native) return;
                boxHidden = !native;
                Color next = boxColor; if (!native) next.a = 0f; box.color = next;
            }
            internal void Refresh(bool changed)
            {
                bool on = KitAppearance.Active;
                bool update = fresh || changed || on != styled;
                if (update) { first.Apply(on); second?.Apply(on); fresh = false; styled = on; }
                TMP_Text text = first.Text;
                bool visible = on && text.gameObject.activeInHierarchy && !string.IsNullOrEmpty(text.text)
                    && (animator == null || !animator.isActiveAndEnabled || !animator.GetCurrentAnimatorStateInfo(0).IsTag("done"));
                if (visible && frame == null) MakeFrame();
                if (frame == null) { ShowBox(true); return; }
                frame.gameObject.SetActive(visible);
                ShowBox(!visible);
                if (!visible) return;
                float alpha = text.canvasRenderer.GetAlpha() * text.color.a;
                if (second != null && second.Text != null && second.Text.gameObject.activeInHierarchy)
                    alpha = Mathf.Min(alpha, second.Text.canvasRenderer.GetAlpha() * second.Text.color.a);
                opacity.alpha = alpha;
                bool geometry = first.Changed();
                if (second != null && second.Text != null) geometry |= second.Changed();
                if (update || geometry) Fit();
            }
            void MakeFrame()
            {
                Sprite card = KitAppearance.Sprite(KitSurfaceRole.Card);
                if (card == null) return;
                var go = new GameObject("Crucible_NativeNoticeFrame", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(LayoutElement));
                go.layer = first.Text.gameObject.layer;
                frame = (RectTransform)go.transform;
                frame.SetParent(first.Text.transform.parent, false);
                // Above the native box, below the icon and texts.
                frame.SetSiblingIndex(box != null ? box.transform.GetSiblingIndex() + 1 : first.Text.transform.GetSiblingIndex());
                frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
                opacity = go.GetComponent<CanvasGroup>(); opacity.blocksRaycasts = opacity.interactable = false;
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                Image image = go.GetComponent<Image>(); image.raycastTarget = false;
                image.sprite = card; image.type = Image.Type.Sliced;
                KitAppearance.Fit(image, KitSurfaceRole.Card);
                Fit();
            }
            void Fit()
            {
                if (box != null)
                {
                    var native = box.rectTransform;
                    frame.anchorMin = native.anchorMin; frame.anchorMax = native.anchorMax; frame.pivot = native.pivot;
                    frame.anchoredPosition = native.anchoredPosition; frame.sizeDelta = native.sizeDelta;
                    frame.localPosition = new Vector3(frame.localPosition.x, frame.localPosition.y, native.localPosition.z);
                    return;
                }
                bool any = false; Vector2 min = Vector2.zero, max = Vector2.zero;
                Bounds(first, ref any, ref min, ref max);
                if (second != null && second.Text != null) Bounds(second, ref any, ref min, ref max);
                if (!any) return;
                Vector2 pad = new Vector2(12f, 8f);
                if (KitNativeMenus.NoticePlate(first.Text, out float left, out RectTransform icon))
                {
                    // Proposed notice layout: the plate starts on the shared left edge and holds the message icon.
                    var image = icon != null ? icon.GetComponent<Image>() : null;
                    if (image != null && image.sprite != null && icon.gameObject.activeInHierarchy && image.canvasRenderer.GetAlpha() > .01f)
                    {
                        var corners = new Vector3[4]; icon.GetWorldCorners(corners);
                        for (int i = 0; i < 4; i++) { Vector2 p = frame.parent.InverseTransformPoint(corners[i]); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                        if (icon.parent == frame.parent && icon.GetSiblingIndex() < frame.GetSiblingIndex()) frame.SetSiblingIndex(icon.GetSiblingIndex());
                    }
                    min -= pad; max += pad;
                    min.x = frame.parent.InverseTransformPoint(new Vector3(left, first.Text.transform.position.y, first.Text.transform.position.z)).x;
                    pad = Vector2.zero;
                }
                else
                {
                    // Back directly behind the text (the proposal may have lowered it behind the icon).
                    int t = first.Text.transform.GetSiblingIndex(), f = frame.GetSiblingIndex();
                    if (f != t - 1) frame.SetSiblingIndex(f < t ? t - 1 : t);
                }
                frame.localPosition = new Vector3((min.x + max.x) * .5f, (min.y + max.y) * .5f, first.Text.transform.localPosition.z);
                frame.sizeDelta = max - min + pad * 2f;
            }
            void Bounds(Label label, ref bool any, ref Vector2 min, ref Vector2 max)
            {
                TMP_Text text = label.Text;
                if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) return;
                text.ForceMeshUpdate();
                UnityEngine.Bounds bounds = text.textBounds;
                Vector3 low = bounds.min, high = bounds.max;
                for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
                {
                    Vector2 point = frame.parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(x == 0 ? low.x : high.x, y == 0 ? low.y : high.y, 0f)));
                    if (!any) { min = max = point; any = true; }
                    else { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
                }
            }
            internal void Dispose()
            {
                first.Restore(); second?.Restore();
                if (box != null) ShowBox(true);
                if (frame != null) { frame.gameObject.SetActive(false); Object.Destroy(frame.gameObject); }
            }
        }
    }
}
