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
        static readonly FieldInfo trophyRows = typeof(InventoryGui).GetField("m_trophyList", BindingFlags.Instance | BindingFlags.NonPublic);
        static TrophySurface trophies;
        static int trophyRevision;
        internal static object TrophiesStatus => trophies?.Status;
        static void TickTrophies()
        {
            var gui = MeadworksHeadless.AllowsUi ? InventoryGui.instance : null;
            var root = gui != null ? gui.m_trophiesPanel : null;
            if (trophies != null && trophies.Root != root) { trophies.Dispose(); trophies = null; }
            if (trophies == null && root != null && root.activeInHierarchy && Native(root.transform, root)) {
                var body = TrophyBacking(root);
                if (body != null && body.enabled && body.gameObject.activeInHierarchy && body.sprite != null && body.fillCenter
                    && body.color.a > 0 && body.canvasRenderer.GetAlpha() > 0 && Native(body.transform, root)
                    && !owned.Contains(body) && !KitSkin.Owns(body) && !KitNativeDialogs.Owns(body) && GamePanelTheme.TryRelease(body))
                    trophies = new TrophySurface(gui, body);
            }
            if (trophies != null)
            {
                if (trophies.Revision != trophyRevision) trophies.Rows(trophyRows?.GetValue(gui) as List<GameObject>, trophyRevision);
                trophies.Apply(On);
            }
        }
        static void ShutdownTrophies() { trophies?.Dispose(); trophies = null; }
        static bool ReservesTrophies(Image image)
        {
            var gui = InventoryGui.instance;
            return gui != null && gui.m_trophiesPanel != null && image != null && Native(image.transform, gui.m_trophiesPanel);
        }
        [HarmonyPatch(typeof(InventoryGui), "UpdateTrophyList")]
        static class TrophyRowsChanged
        {
            static void Postfix(InventoryGui __instance)
            { if (__instance == InventoryGui.instance) trophyRevision++; }
        }
        static Image TrophyBacking(GameObject root)
        {
            if (root == null) return null;
            Transform frame = null, body = null;
            foreach (Transform child in root.transform) if (child.name == "TrophiesFrame") { if (frame != null) return null; frame = child; }
            if (frame == null) return null;
            foreach (Transform child in frame) if (child.name == "border (1)") { if (body != null) return null; body = child; }
            return body != null ? body.GetComponent<Image>() : null;
        }
        sealed class TrophySurface : IDisposable
        {
            internal readonly GameObject Root;
            readonly InventoryGui gui;
            readonly Image background;
            readonly ImageLook backing;
            bool backingApplied, yielded;
            readonly List<FontLook> labels = new List<FontLook>();
            readonly List<BookButton> buttons = new List<BookButton>();
            readonly List<ImageLook> controls = new List<ImageLook>();
            readonly Dictionary<GameObject, List<FontLook>> rows = new Dictionary<GameObject, List<FontLook>>();
            internal int Revision = -1;
            int rejected;
            internal object Status => new { present = Root != null, visible = Root != null && Root.activeInHierarchy,
                labels = labels.Count, buttons = buttons.Count, controls = controls.Count, rows = rows.Count,
                rejectedRows = rejected, revision = Revision, backingStyled = backingApplied, yielded,
                backing = "TrophiesFrame/border (1)", backingLimit = "" };
            internal TrophySurface(InventoryGui gui, Image body)
            {
                this.gui = gui; Root = gui.m_trophiesPanel; background = body; backing = new ImageLook(body, KitSurfaceRole.Window, protectOffState: true);
                bool Shell(Transform t) => Native(t, Root) && !t.IsChildOf(gui.m_trophieListRoot)
                    && (gui.m_trophieElementPrefab == null || !t.IsChildOf(gui.m_trophieElementPrefab.transform));
                foreach (var label in Root.GetComponentsInChildren<TMP_Text>(true))
                    if (Shell(label.transform)) labels.Add(new FontLook(label));
                foreach (var button in Root.GetComponentsInChildren<Button>(true))
                {
                    if (!Shell(button.transform)) continue;
                    bool nativeClose = false;
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        if (button.onClick.GetPersistentTarget(i) == gui && button.onClick.GetPersistentMethodName(i) == nameof(InventoryGui.OnCloseTrophies)) nativeClose = true;
                    if (nativeClose) buttons.Add(new BookButton(button, Root));
                }
                var scroll = gui.m_trophyListScroll;
                if (scroll != null && Native(scroll.transform, Root))
                {
                    var image = scroll.targetGraphic as Image;
                    if (image != null && Native(image.transform, Root) && !owned.Contains(image) && !KitSkin.Owns(image)) controls.Add(new ImageLook(image, KitSurfaceRole.Card));
                }
            }
            internal void Rows(List<GameObject> live, int revision)
            {
                Revision = revision; rejected = 0;
                var keep = new HashSet<GameObject>();
                if (live != null && gui.m_trophieElementPrefab != null) foreach (var row in live)
                {
                    if (row == null || row.transform.parent != gui.m_trophieListRoot || !Native(row.transform, Root)
                        || !BookShape(row.transform, gui.m_trophieElementPrefab.transform, 0)) { rejected++; continue; }
                    keep.Add(row);
                    if (rows.ContainsKey(row)) continue;
                    var fonts = new List<FontLook>();
                    foreach (var path in new[] { "name", "description" })
                    {
                        var at = row.transform.Find(path);
                        var label = at != null && Native(at, Root) ? at.GetComponent<TMP_Text>() : null;
                        if (label != null) fonts.Add(new FontLook(label));
                    }
                    rows.Add(row, fonts);
                }
                var obsolete = new List<GameObject>();
                foreach (var row in rows) if (!keep.Contains(row.Key)) { foreach (var font in row.Value) font.Apply(false); obsolete.Add(row.Key); }
                foreach (var row in obsolete) rows.Remove(row);
            }
            internal void Apply(bool on)
            {
                if (yielded) return;
                if (TrophyBacking(Root) != background) { Dispose(); yielded = true; return; }
                backingApplied = backing.Apply(on);
                foreach (var font in labels) font.Apply(on);
                foreach (var fontList in rows.Values) foreach (var font in fontList) font.Apply(on);
                foreach (var button in buttons) button.Apply(on);
                foreach (var image in controls) image.Apply(on);
            }
            public void Dispose()
            {
                backing.Dispose(); backingApplied = false;
                foreach (var font in labels) font.Apply(false);
                foreach (var fontList in rows.Values) foreach (var font in fontList) font.Apply(false);
                foreach (var button in buttons) button.Dispose();
                foreach (var image in controls) image.Dispose();
                rows.Clear();
            }
        }
    }
}
