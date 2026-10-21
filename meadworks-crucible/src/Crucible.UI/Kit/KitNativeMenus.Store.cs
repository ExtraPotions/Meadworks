// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        static readonly FieldInfo storeRows = typeof(StoreGui).GetField("m_itemList", BindingFlags.Instance | BindingFlags.NonPublic);
        static StoreSurface storeSurface;
        static int storeRevision, storeGeneration;
        static void MarkStoreChanged() { storeGeneration++; }
        internal static object StoreStatus => storeSurface?.Status;
        static bool ReservesStore(Image image)
        {
            var host = StoreGui.instance;
            return image != null && host != null && host.m_rootPanel != null && StoreNative(image.transform, host.m_rootPanel);
        }
        static void TickStore()
        {
            if (!MeadworksHeadless.AllowsUi) { ShutdownStore(); return; }
            var host = StoreGui.instance;
            if (storeSurface != null && !storeSurface.Matches(host)) { storeSurface.Dispose(); storeSurface = null; }
            if (storeSurface == null && host != null && host.m_rootPanel != null && host.m_rootPanel.activeInHierarchy)
                storeSurface = new StoreSurface(host);
            if (storeSurface == null) return;
            if (storeSurface.Revision != storeRevision) storeSurface.Reconcile(storeRows?.GetValue(host) as List<GameObject>, storeRevision);
            storeSurface.Apply(On);
        }
        static void ShutdownStore() { storeSurface?.Dispose(); storeSurface = null; }
        [HarmonyPatch(typeof(StoreGui), "FillList")]
        static class StoreFilled
        {
            // Reconcile next tick, after native/product temporary price finalizers unwind.
            static void Postfix(StoreGui __instance) { if (__instance == StoreGui.instance) storeRevision++; }
        }
        static bool StoreNative(Transform at, GameObject root)
        {
            for (var parent = at; parent != null; parent = parent.parent)
            {
                if (parent.name.StartsWith("Barter_", StringComparison.Ordinal)) return false;
                if (parent == root.transform) return Native(at, root);
            }
            return false;
        }
        sealed class StoreSurface : IDisposable
        {
            readonly StoreGui host;
            internal readonly GameObject Root;
            readonly RectTransform list;
            readonly GameObject prefab;
            readonly Button buy, sell;
            readonly TMP_Text coins;
            readonly Scrollbar scroll;
            readonly List<ImageLook> images = new List<ImageLook>();
            readonly List<BookButton> buttons = new List<BookButton>();
            readonly List<FontLook> fonts = new List<FontLook>();
            readonly Dictionary<GameObject, StoreRow> rows = new Dictionary<GameObject, StoreRow>();
            internal int Revision { get; private set; } = -1;
            int paintedGeneration = -1, paintedRevision = -1, rejected;
            bool paintedOn;
            string reason = "awaiting-native-row-roster";
            internal object Status => new { root = Root != null ? Root.name : "", visible = Root != null && Root.activeInHierarchy,
                images = images.Count, buttons = buttons.Count, labels = fonts.Count, rows = rows.Count, rejectedRows = rejected,
                revision = Revision, reason, on = On, templateExcluded = true };
            internal StoreSurface(StoreGui host)
            {
                this.host = host; Root = host.m_rootPanel; list = host.m_listRoot; prefab = host.m_listElement;
                buy = host.m_buyButton; sell = host.m_sellButton; coins = host.m_coinText; scroll = host.m_listScroll;
                foreach (var button in new[] { buy, sell })
                    if (button != null && StoreNative(button.transform, Root)) buttons.Add(new BookButton(button, Root));
                if (scroll != null && StoreNative(scroll.transform, Root)) AddImage(scroll.targetGraphic as Image, KitSurfaceRole.Card);
                foreach (var image in Root.GetComponentsInChildren<Image>(true))
                {
                    if (!Static(image.transform) || image.GetComponentInParent<Selectable>() != null || image.sprite == null
                        || (image.type != Image.Type.Sliced && image.type != Image.Type.Tiled) || !KitThemeRules.IsWoodSprite(image.sprite.name)) continue;
                    var canvas = image.canvas; var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
                    if (canvasRect == null || image.rectTransform.rect.width <= 0 || image.rectTransform.rect.height <= 0
                        || image.rectTransform.rect.width >= canvasRect.rect.width || image.rectTransform.rect.height >= canvasRect.rect.height) continue;
                    if (!GamePanelTheme.TryRelease(image) || !image.fillCenter) continue;
                    AddImage(image, KitSurfaceRole.Card, protectOffState: true);
                }
                foreach (var label in Root.GetComponentsInChildren<TMP_Text>(true)) if (Static(label.transform)) fonts.Add(new FontLook(label));
            }
            bool Static(Transform at) => StoreNative(at, Root) && !(list != null && at.IsChildOf(list)) && !(prefab != null && at.IsChildOf(prefab.transform));
            void AddImage(Image image, KitSurfaceRole role, bool protectOffState = false)
            {
                if (image != null && StoreNative(image.transform, Root) && !owned.Contains(image) && !KitSkin.Owns(image)
                    && !KitNativeDialogs.Owns(image)) images.Add(new ImageLook(image, role, protectOffState));
            }
            internal bool Matches(StoreGui current) => current != null && host == current && current.m_rootPanel == Root && current.m_listRoot == list
                && current.m_listElement == prefab && current.m_buyButton == buy && current.m_sellButton == sell && current.m_coinText == coins && current.m_listScroll == scroll;
            internal void Reconcile(List<GameObject> live, int revision)
            {
                Revision = revision; rejected = 0; var keep = new HashSet<GameObject>();
                if (live != null && prefab != null && StoreNative(prefab.transform, prefab)) foreach (var row in live)
                {
                    if (row == null || row.transform.parent != list || !StoreNative(row.transform, Root) || !BookShape(row.transform, prefab.transform, 0)) { rejected++; continue; }
                    keep.Add(row);
                    if (!rows.ContainsKey(row))
                    {
                        var appearance = StoreRow.Capture(row, prefab, Root);
                        if (appearance == null) { keep.Remove(row); rejected++; continue; }
                        rows.Add(row, appearance);
                    }
                }
                var obsolete = new List<GameObject>();
                foreach (var row in rows) if (!keep.Contains(row.Key)) { row.Value.Dispose(); obsolete.Add(row.Key); }
                foreach (var row in obsolete) rows.Remove(row);
                reason = live == null ? "native-roster-unavailable" : rejected == 0 ? "native-store-reconciled" : "unknown-row-appearance-declined";
            }
            internal void Apply(bool on)
            {
                if (paintedGeneration != storeGeneration || paintedRevision != Revision || paintedOn != on)
                {
                    foreach (var image in images) image.Apply(on); foreach (var button in buttons) button.Apply(on);
                    foreach (var font in fonts) font.Apply(on); foreach (var row in rows.Values) row.Apply(on);
                    paintedGeneration = storeGeneration; paintedRevision = Revision; paintedOn = on;
                }
                else foreach (var button in buttons) button.FollowText(on);
            }
            public void Dispose()
            {
                foreach (var image in images) image.Dispose(); foreach (var button in buttons) button.Dispose();
                foreach (var font in fonts) font.Apply(false); foreach (var row in rows.Values) row.Dispose(); rows.Clear();
            }
        }
        sealed class StoreRow : IDisposable
        {
            readonly Button button;
            readonly Graphic graphic;
            readonly List<ImageLook> images = new List<ImageLook>();
            readonly List<FontLook> fonts = new List<FontLook>();
            bool yielded;
            StoreRow(Button button) { this.button = button; graphic = button.targetGraphic; }
            internal static StoreRow Capture(GameObject row, GameObject prefab, GameObject root)
            {
                foreach (var component in row.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component == null || !StoreNative(component.transform, root)) return null;
                var button = row.GetComponent<Button>(); var original = prefab.GetComponent<Button>();
                var icon = row.transform.Find("icon")?.GetComponent<Image>();
                if (button == null || original == null || icon == null) return null;
                var target = button.targetGraphic as Image; var source = original.targetGraphic as Image;
                if (target != null && target != icon && (source == null || !StoreNative(target.transform, root)
                    || target.sprite != source.sprite || target.material != source.material || target.type != source.type
                    || target.color != source.color || !Mathf.Approximately(target.pixelsPerUnitMultiplier, source.pixelsPerUnitMultiplier))) return null;
                var selected = row.transform.Find("selected")?.GetComponent<Image>();
                var sourceSelected = prefab.transform.Find("selected")?.GetComponent<Image>();
                if (selected != null && selected != icon && selected != target && (sourceSelected == null
                    || selected.sprite != sourceSelected.sprite || selected.material != sourceSelected.material
                    || selected.type != sourceSelected.type || selected.color != sourceSelected.color
                    || selected.fillCenter != sourceSelected.fillCenter
                    || !Mathf.Approximately(selected.pixelsPerUnitMultiplier, sourceSelected.pixelsPerUnitMultiplier))) return null;
                var labels = row.GetComponentsInChildren<TMP_Text>(true);
                var sourceLabels = prefab.GetComponentsInChildren<TMP_Text>(true);
                if (labels.Length != sourceLabels.Length) return null;
                for (int i = 0; i < labels.Length; i++)
                    if (labels[i].font != sourceLabels[i].font || labels[i].fontSharedMaterial != sourceLabels[i].fontSharedMaterial) return null;
                if (target != null && target != icon && (owned.Contains(target) || KitSkin.Owns(target) || KitNativeDialogs.Owns(target))
                    || selected != null && selected != target && selected != icon && (owned.Contains(selected) || KitSkin.Owns(selected) || KitNativeDialogs.Owns(selected))) return null;
                var result = new StoreRow(button);
                if (target != null && target != icon && !owned.Contains(target) && !KitSkin.Owns(target)) result.images.Add(new ImageLook(target, KitSurfaceRole.Row));
                if (selected != null && selected != target && selected != icon && StoreNative(selected.transform, root)
                    && !owned.Contains(selected) && !KitSkin.Owns(selected)) result.images.Add(new ImageLook(selected, KitSurfaceRole.ButtonHover));
                foreach (var label in row.GetComponentsInChildren<TMP_Text>(true)) if (StoreNative(label.transform, root)) result.fonts.Add(new FontLook(label));
                return result;
            }
            internal void Apply(bool on)
            {
                if (yielded || button == null) return;
                if (button.targetGraphic != graphic) { Dispose(); yielded = true; return; }
                foreach (var image in images) image.Apply(on); foreach (var font in fonts) font.Apply(on);
            }
            public void Dispose() { foreach (var image in images) image.Dispose(); foreach (var font in fonts) font.Apply(false); }
        }
    }
}
