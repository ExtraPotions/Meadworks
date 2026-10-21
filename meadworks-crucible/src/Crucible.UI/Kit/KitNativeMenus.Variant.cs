// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        static readonly FieldInfo variantRows = typeof(VariantDialog).GetField("m_elements", BindingFlags.Instance | BindingFlags.NonPublic);
        static VariantSurface variantSurface;
        internal static object VariantStatus => variantSurface?.Status;
        static void TickVariant()
        {
            var gui = MeadworksHeadless.AllowsUi ? InventoryGui.instance : null;
            var host = gui != null ? gui.m_variantDialog : null;
            if (variantSurface != null && !variantSurface.Matches(host)) { variantSurface.Dispose(); variantSurface = null; }
            if (variantSurface == null && host != null && host.gameObject.activeInHierarchy && Native(host.transform, host.gameObject) && VariantSurface.LaidOut(host))
                variantSurface = new VariantSurface(host);
            variantSurface?.Apply(On);
        }
        static void ShutdownVariant() { variantSurface?.Dispose(); variantSurface = null; }
        static bool ReservesVariant(Image image)
        {
            var gui = InventoryGui.instance;
            var host = gui != null ? gui.m_variantDialog : null;
            return host != null && image != null && Native(image.transform, host.gameObject);
        }
        sealed class VariantSurface : IDisposable
        {
            readonly VariantDialog host;
            readonly Transform listRoot;
            readonly GameObject prefab;
            readonly Image body;
            readonly ImageLook backing;
            bool backingApplied;
            readonly string backingReason;
            readonly List<BookButton> close = new List<BookButton>();
            readonly List<FontLook> labels = new List<FontLook>();
            internal object Status => new {
                present = host != null, visible = host != null && host.gameObject.activeInHierarchy,
                closeControls = close.Count, labels = labels.Count,
                rows = host != null ? (variantRows?.GetValue(host) as List<GameObject>)?.Count ?? 0 : 0,
                backingStyled = backingApplied, backingApplied, appearanceQualified = false,
                backingBound = backing != null, backingPath = body != null ? "VariantFrame/border (1)" : "",
                backingLimit = backing == null ? backingReason : backingApplied ? "" : "off-or-backing-yielded", on = On
            };
            internal VariantSurface(VariantDialog host)
            {
                this.host = host; listRoot = host.m_listRoot; prefab = host.m_elementPrefab;
                var root = host.gameObject;
                body = Body(host);
                if (body != null && body.sprite != null && body.sprite.name == "woodpanel_512x512"
                    && body.material != null && body.material.name == "litpanel" && body.type == Image.Type.Sliced
                    && body.color == Color.white && body.enabled && body.gameObject.activeInHierarchy
                    && body.rectTransform.rect.width > 0 && body.rectTransform.rect.height > 0)
                    backing = new ImageLook(body, KitSurfaceRole.Window, protectOffState: true);
                backingReason = backing == null ? "unknown-or-unlaid-native-body-declined" : "";
                if (listRoot == null || prefab == null || !Native(listRoot, root)) return;
                bool Shell(Transform at) => Native(at, root) && !at.IsChildOf(listRoot)
                    && !at.IsChildOf(prefab.transform);
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    if (!Shell(button.transform) || body == null || !button.transform.IsChildOf(body.transform.parent)
                        || button.transform.parent == root.transform && button.name == "Closebutton") continue;
                    bool nativeClose = false;
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        if (button.onClick.GetPersistentTarget(i) == host && button.onClick.GetPersistentMethodName(i) == nameof(VariantDialog.OnClose)) nativeClose = true;
                    if (nativeClose) close.Add(new BookButton(button, root));
                }
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                    if (Shell(label.transform) && body != null && label.transform.IsChildOf(body.transform.parent)) labels.Add(new FontLook(label));
            }
            internal static bool LaidOut(VariantDialog current)
            {
                var image = Body(current);
                return image == null || image.rectTransform.rect.width > 0 && image.rectTransform.rect.height > 0;
            }
            static Image Body(VariantDialog current)
            {
                if (current == null) return null;
                var root = current.gameObject;
                Transform frame = null;
                foreach (Transform child in root.transform)
                    if (child.name == "VariantFrame") { if (frame != null) return null; frame = child; }
                if (frame == null || !Native(frame, root)) return null;
                Transform border = null;
                foreach (Transform child in frame)
                    if (child.name == "border (1)") { if (border != null) return null; border = child; }
                if (border == null || !Native(border, root)) return null;
                return border.GetComponent<Image>();
            }
            internal bool Matches(VariantDialog current) => host != null && host == current
                && host.m_listRoot == listRoot && host.m_elementPrefab == prefab && Body(host) == body;
            internal void Apply(bool on)
            {
                backingApplied = backing != null && backing.Apply(on);
                foreach (var button in close) button.Apply(on);
                foreach (var font in labels) font.Apply(on);
            }
            public void Dispose()
            {
                backing?.Dispose(); backingApplied = false;
                foreach (var button in close) button.Dispose();
                foreach (var font in labels) font.Apply(false);
            }
        }
    }
}
