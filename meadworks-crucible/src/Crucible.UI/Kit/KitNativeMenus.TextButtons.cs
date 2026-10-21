// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        sealed class TextButtonLook : IDisposable
        {
            static readonly Func<Selectable, bool> pointerInside = Getter("isPointerInside");
            static readonly Func<Selectable, bool> pointerDown = Getter("isPointerDown");
            static readonly Func<Selectable, bool> selected = Getter("hasSelection");
            internal static bool Supported => pointerInside != null && pointerDown != null && selected != null;
            readonly Button button;
            readonly Graphic originalGraphic;
            readonly TMP_Text target;
            readonly RectTransform source, rect;
            readonly Image background;
            readonly KitNativeMenuTextButton refresh;
            readonly bool sibling;
            bool on, yielded, disposed;

            static Func<Selectable, bool> Getter(string name)
            {
                var getter = typeof(Selectable).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetGetMethod(true);
                try { return getter == null ? null : (Func<Selectable, bool>)Delegate.CreateDelegate(typeof(Func<Selectable, bool>), getter); }
                catch (Exception) { return null; }
            }
            internal TextButtonLook(Button button, TMP_Text target)
            {
                this.button = button; originalGraphic = button.targetGraphic; this.target = target; source = (RectTransform)button.transform;
                // A child would cover text rendered by the button root itself.
                sibling = target.transform == source;
                var node = new GameObject("Crucible_native_text_button_" + button.name, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
                node.layer = button.gameObject.layer;
                node.GetComponent<LayoutElement>().ignoreLayout = true;
                rect = (RectTransform)node.transform;
                rect.SetParent(sibling ? source.parent : source, false);
                if (!sibling)
                {
                    rect.SetAsFirstSibling(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                background = node.GetComponent<Image>(); background.raycastTarget = false; background.type = Image.Type.Sliced;
                owned.Add(background);
                refresh = node.AddComponent<KitNativeMenuTextButton>(); refresh.Refresh = Draw;
                node.SetActive(false);
            }
            internal void Apply(bool enabled)
            {
                on = enabled;
                Draw();
            }
            void Draw()
            {
                if (disposed || background == null) return;
                if (button == null || target == null || source == null || button.targetGraphic != originalGraphic) yielded = true;
                if (!on || yielded || !button.gameObject.activeInHierarchy || !target.enabled || !target.gameObject.activeInHierarchy)
                { if (background.gameObject.activeSelf) background.gameObject.SetActive(false); return; }
                bool enabled = button.IsActive() && button.IsInteractable();
                var role = !enabled ? KitSurfaceRole.ButtonDisabled : pointerDown(button) ? KitSurfaceRole.ButtonPressed
                    : selected(button) || pointerInside(button) ? KitSurfaceRole.ButtonHover : KitSurfaceRole.Button;
                var sprite = KitAppearance.Sprite(role);
                if (sprite == null) { background.gameObject.SetActive(false); return; }
                if (sibling)
                {
                    if (rect.parent != source.parent) rect.SetParent(source.parent, false);
                    int at = source.GetSiblingIndex(), current = rect.GetSiblingIndex();
                    int before = current < at ? at - 1 : at;
                    if (current != before) rect.SetSiblingIndex(before);
                    // Every frame (LateUpdate): write only what moved, so an idle menu rebuilds no layout or canvas.
                    if (rect.anchorMin != source.anchorMin) rect.anchorMin = source.anchorMin;
                    if (rect.anchorMax != source.anchorMax) rect.anchorMax = source.anchorMax;
                    if (rect.pivot != source.pivot) rect.pivot = source.pivot;
                    if (rect.sizeDelta != source.sizeDelta) rect.sizeDelta = source.sizeDelta;
                    if (rect.anchoredPosition3D != source.anchoredPosition3D) rect.anchoredPosition3D = source.anchoredPosition3D;
                    if (rect.localRotation != source.localRotation) rect.localRotation = source.localRotation;
                    if (rect.localScale != source.localScale) rect.localScale = source.localScale;
                }
                if (background.sprite != sprite)
                { background.sprite = sprite; Fit(background, KitAppearance.PixelsPerUnit(role)); }
                var dim = button.colors.disabledColor;
                var face = enabled ? Color.white : new Color(dim.r, dim.g, dim.b, 1);
                if (background.color != face) background.color = face;
                // Follow the native label fade without changing the button target or transition.
                float alpha = target.canvasRenderer.GetAlpha() * Mathf.Clamp01(target.color.a);
                if (background.canvasRenderer.GetAlpha() != alpha) background.canvasRenderer.SetAlpha(alpha);
                if (!background.gameObject.activeSelf) background.gameObject.SetActive(true);
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (refresh != null) refresh.Refresh = null;
                owned.Remove(background);
                if (background != null) { background.gameObject.SetActive(false); Object.Destroy(background.gameObject); }
            }
        }
    }

    public sealed class KitNativeMenuTextButton : MonoBehaviour
    {
        internal Action Refresh;
        void OnEnable()
        {
            // Native button clones copy this component without its owner's delegate.
            if (Refresh == null) gameObject.SetActive(false);
        }
        void LateUpdate()
        {
            if (Refresh == null) gameObject.SetActive(false);
            else Refresh();
        }
    }
}
