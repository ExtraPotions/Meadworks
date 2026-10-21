using ExtraPotions.Meadworks.Crucible.UI.PlateColumn;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// The game's tooltip on kit controls, in PackPanel's look: the inventory item tooltip with PlateColumn's thin gold
    /// border (<see cref="Column.DressTooltip"/>) when the inventory exists, else the craft button's tooltip prefab.
    /// The main menu uses a framed native template or a kit reading panel when its native tooltips contain only text.
    /// </summary>
    public static partial class KitTooltip
    {
        private static GameObject fallback;
        private static GameObject holder;
        private static readonly Dictionary<GameObject, GameObject> marked = new Dictionary<GameObject, GameObject>();

        /// <summary>The control's <c>UITooltip</c>, added and dressed when missing; null headless.</summary>
        public static UITooltip Attach(GameObject control)
        {
            if (control == null || !MeadworksHeadless.AllowsUi)
                return null;
            UITooltip tip = control.GetComponent<UITooltip>();
            if (tip == null)
                tip = control.AddComponent<UITooltip>();
            Dress(tip);
            var presenter = control.GetComponent<KitControlTip>();
            if (presenter == null) presenter = control.AddComponent<KitControlTip>();
            presenter.Bind(tip);
            return tip;
        }

        /// <summary>The topic and text shown on hover; empty text hides the tooltip.</summary>
        public static void Set(GameObject control, string topic, string text)
        {
            UITooltip tip = Attach(control);
            if (tip != null)
                { tip.Set(topic ?? "", text ?? ""); control.GetComponent<KitControlTip>()?.ClearStructured(); }
        }

        /// <summary>A dedicated mod menu's pinned tip, placed beside its control and kept inside the screen.</summary>
        public static void SetMenu(GameObject control, string topic, string text)
        {
            Set(control, topic, text);
        }

        /// <summary>Explicit hover owners may show a topic without a body; ordinary Set still hides an empty body.</summary>
        public static void Show(GameObject control, string topic, string text)
        {
            if (Attach(control) == null) return;
            Set(control, topic, text);
            var presenter = control.GetComponent<KitControlTip>();
            presenter.AllowTopicOnly = true;
            presenter.Begin();
        }

        public static void Hide(GameObject control)
        {
            if (control != null) control.GetComponent<KitControlTip>()?.Hide();
        }

        public static void Dress(UITooltip tip)
        {
            if (tip == null)
                return;
            InventoryGui gui = InventoryGui.instance;
            if (gui != null && Column.DressTooltip(gui, tip))
            { tip.m_tooltipPrefab = Mark(tip.m_tooltipPrefab); return; }
            if (tip.m_tooltipPrefab != null && Framed(tip.m_tooltipPrefab)) { tip.m_tooltipPrefab = Mark(tip.m_tooltipPrefab); return; }
            if (gui != null && gui.m_craftButton != null)
            {
                UITooltip craft = gui.m_craftButton.GetComponent<UITooltip>();
                if (craft != null && Framed(craft.m_tooltipPrefab))
                {
                    tip.m_tooltipPrefab = Mark(craft.m_tooltipPrefab);
                    return;
                }
            }
            tip.m_tooltipPrefab = Mark(Fallback());
        }

        // Epic Loot's existing RecipeTips seam: keep its scroll-box marker on an owned inactive copy,
        // never change the game's tooltip prefab. Portions Copyright (C) 2026 Gerald Glasgow.
        // OpenKeep RecipeTips at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, GPL-3.0-or-later.
        // Changed by expDARE, 2026: shared kit template, current source identity and teardown.
        private static GameObject Mark(GameObject source)
        {
            if (source == null || source.transform.Find("Scroll View") != null) return source;
            if (marked.TryGetValue(source, out var copy) && copy != null) return copy;
            EnsureHolder();
            copy = Object.Instantiate(source, holder.transform, false);
            copy.name = "Crucible_" + source.name;
            var marker = new GameObject("Scroll View");
            marker.SetActive(false);
            marker.transform.SetParent(copy.transform, false);
            marked[source] = copy;
            return copy;
        }

        internal static bool OwnsTemplate(GameObject candidate)
        {
            if (candidate == null) return false;
            foreach (var pair in marked)
                if (pair.Key != null && pair.Value != null && ReferenceEquals(pair.Value, candidate)) return true;
            return false;
        }
        private static void EnsureHolder()
        {
            if (holder != null) return;
            holder = new GameObject("Crucible_tooltip_templates");
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);
        }

        private static bool Framed(GameObject source)
        {
            if (source == null || source.transform.childCount == 0) return false;
            var frame = source.transform.GetChild(0) as RectTransform;
            if (frame == null) return false;
            bool topic = false, text = false, background = false;
            foreach (var label in frame.GetComponentsInChildren<TMP_Text>(true))
            { topic |= label.name == "Topic"; text |= label.name == "Text"; }
            foreach (var image in frame.GetComponentsInChildren<Image>(true))
                background |= image.enabled && image.color.a > .2f && image.sprite != null;
            return topic && text && background;
        }

        private static GameObject MainMenuTemplate()
        {
            EnsureHolder();
            var root = new GameObject("Crucible_menu_tooltip", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(holder.transform, false);
            ((RectTransform)root.transform).sizeDelta = Vector2.zero;
            var group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var frame = KitPanel.Create(root.transform, "Frame", new Vector2(400, 0));
            if (frame == null) { Object.Destroy(root); return null; }
            KitForged.SidePlate(frame);
            frame.anchorMin = frame.anchorMax = frame.pivot = Vector2.zero;
            frame.anchoredPosition = new Vector2(12, 12);
            var background = KitPanel.Background(frame); background.raycastTarget = false;
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var layout = frame.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12); layout.spacing = 6;
            layout.childAlignment = TextAnchor.UpperLeft; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var size = frame.gameObject.AddComponent<ContentSizeFitter>(); size.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            KitCanvas.Text(frame, "Topic", "", 20, KitAppearance.Color(KitColorRole.Warning), TextAlignmentOptions.TopLeft, true);
            KitCanvas.Text(frame, "Text", "", 18, KitAppearance.Color(KitColorRole.Text), TextAlignmentOptions.TopLeft, true);
            return root;
        }

        internal static void Shutdown()
        {
            KitControlTip.Shutdown();
            marked.Clear(); fallback = null;
            if (holder != null) Object.Destroy(holder);
            holder = null;
        }

        internal static void Prune()
        {
            foreach (var source in new List<GameObject>(marked.Keys))
            {
                if (source != null && marked[source] != null) continue;
                if (marked[source] != null) Object.Destroy(marked[source]);
                marked.Remove(source);
            }
            if (fallback == null) fallback = null;
        }

        private static GameObject Fallback()
        {
            if (fallback != null && Framed(fallback))
                return fallback;
            foreach (UITooltip other in Resources.FindObjectsOfTypeAll<UITooltip>())
                if (other != null && Framed(other.m_tooltipPrefab))
                    return fallback = other.m_tooltipPrefab;
            return fallback = MainMenuTemplate();
        }
    }

    internal sealed partial class KitControlTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        static readonly HashSet<KitControlTip> presenters = new HashSet<KitControlTip>();
        static KitControlTip active;
        UITooltip source;
        bool nativeEnabled;
        float showAt = -1;
        GameObject box;
        string shownTopic, shownText;
        GameObject shownPrefab;
        readonly Vector3[] targetCorners = new Vector3[4];
        readonly Vector3[] shownCorners = new Vector3[4];
        int screenWidth, screenHeight;
        internal bool AllowTopicOnly;

        internal void Bind(UITooltip tip)
        {
            if (source != tip) { source = tip; nativeEnabled = tip.enabled; }
            source.enabled = false;
            presenters.Add(this);
            foreach (var old in GetComponents<MonoBehaviour>())
            {
                string name = old.GetType().FullName;
                if (name != "ExtraPotions.Meadworks.Crucible.UI.PlateColumn.PlateTip"
                    && name != "ExtraPotions.Mantle.Embedded.PlateColumn.PlateTip") continue;
                old.enabled = false; Destroy(old);
            }
        }

        internal void Begin()
        {
            if (active != null && active != this) active.Hide();
            active = this;
            UITooltip.HideTooltip();
            showAt = Time.unscaledTime + .5f;
        }

        public void OnPointerEnter(PointerEventData eventData) => Begin();
        public void OnPointerExit(PointerEventData eventData) => Hide();
        public void OnSelect(BaseEventData eventData) { if (ZInput.IsGamepadActive()) Begin(); }
        public void OnDeselect(BaseEventData eventData) => Hide();

        void Update()
        {
            if (active != this) return;
            if (source == null || !source.gameObject.activeInHierarchy) { Hide(); return; }
            if (!HasStructuredContent && string.IsNullOrEmpty(source.m_text) && (!AllowTopicOnly || string.IsNullOrEmpty(source.m_topic)))
            { if (box != null) Destroy(box); box = null; return; }
            if (showAt >= 0 && Time.unscaledTime < showAt) return;
            ((RectTransform)transform).GetWorldCorners(targetCorners);
            bool moved = screenWidth != Screen.width || screenHeight != Screen.height;
            for (int i = 0; i < targetCorners.Length; i++) moved |= targetCorners[i] != shownCorners[i];
            if (box != null && !moved && source.m_topic == shownTopic && source.m_text == shownText && source.m_tooltipPrefab == shownPrefab && StructuredKey == shownStructured) return;
            if (box != null) Destroy(box);
            box = StructuredBox() ?? TipBox.Show(source.m_tooltipPrefab, (RectTransform)transform, source.m_topic, source.m_text);
            shownTopic = source.m_topic; shownText = source.m_text; shownPrefab = source.m_tooltipPrefab;
            System.Array.Copy(targetCorners, shownCorners, targetCorners.Length);
            screenWidth = Screen.width; screenHeight = Screen.height;
            showAt = -1;
        }

        internal void Hide()
        {
            if (box != null) Destroy(box);
            box = null; showAt = -1;
            if (active == this) active = null;
        }

        void OnDisable() => Hide();
        void OnDestroy()
        {
            Hide(); presenters.Remove(this);
            if (source != null) source.enabled = nativeEnabled;
        }

        internal static void Shutdown()
        {
            foreach (var presenter in new List<KitControlTip>(presenters))
                if (presenter != null) { presenter.Hide(); Destroy(presenter); }
            presenters.Clear(); active = null;
        }
    }

    /// <summary>The game's <c>$token</c> localisation, or the text itself before the game's localisation exists.</summary>
    public static class KitLocalize
    {
        public static string Text(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? "";
            return Localization.instance != null ? Localization.instance.Localize(text) : text;
        }
    }
}
