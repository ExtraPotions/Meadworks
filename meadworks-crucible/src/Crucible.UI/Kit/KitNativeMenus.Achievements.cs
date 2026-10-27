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
        static readonly FieldInfo achievementDetailsRoot = AccessTools.Field(typeof(AchievementsGui), "m_achievementDetailsListRoot");
        static readonly FieldInfo achievementDetailsPrefab = AccessTools.Field(typeof(AchievementsGui), "m_achievementDetailsElementPrefab");
        static readonly FieldInfo achievementScroll = AccessTools.Field(typeof(AchievementsGui), "m_scrollbar");
        static readonly FieldInfo achievementDetailsScroll = AccessTools.Field(typeof(AchievementsGui), "m_detailsScrollbar");
        static AchievementSurface achievements;
        static int achievementRevision, achievementDetailRevision;
        internal static object AchievementsStatus => achievements?.Status;
        static void TickAchievements()
        {
            var gui = MeadworksHeadless.AllowsUi ? InventoryGui.instance : null;
            var host = gui != null ? gui.m_achievementsPanel : null;
            if (achievements != null && achievements.Host != host) { achievements.Dispose(); achievements = null; }
            if (achievements == null && host != null && host.gameObject.activeInHierarchy && Native(host.transform, host.gameObject)) achievements = new AchievementSurface(gui, host);
            if (achievements != null)
            {
                if (achievements.Revision != achievementRevision) achievements.Rows(achievementRevision);
                if (achievements.DetailRevision != achievementDetailRevision) achievements.Details(achievementDetailRevision);
                // The surface outlives the panel; while it is closed there is nothing to keep in step.
                if (host != null && host.gameObject.activeInHierarchy) achievements.Apply(On);
            }
        }
        static void ShutdownAchievements() { achievements?.Dispose(); achievements = null; }
        static bool ReservesAchievements(Image image)
        {
            var gui = InventoryGui.instance;
            return gui != null && gui.m_achievementsPanel != null && image != null && Native(image.transform, gui.m_achievementsPanel.gameObject);
        }
        static bool NativeAchievementHost(AchievementsGui host) => InventoryGui.instance != null && InventoryGui.instance.m_achievementsPanel == host;
        [HarmonyPatch(typeof(InventoryGui), "UpdateAchievementsList")]
        static class AchievementListChanged { static void Postfix(InventoryGui __instance) { if (__instance == InventoryGui.instance) achievementRevision++; } }
        [HarmonyPatch(typeof(AchievementsGui), "CreateStatRow")]
        static class AchievementDetailCreated { static void Postfix(AchievementsGui __instance) { if (NativeAchievementHost(__instance)) achievementDetailRevision++; } }
        [HarmonyPatch(typeof(AchievementsGui), nameof(AchievementsGui.OnOpenAchievementDetails))]
        static class AchievementDetailOpened { static void Postfix(AchievementsGui __instance) { if (NativeAchievementHost(__instance)) achievementDetailRevision++; } }
        [HarmonyPatch(typeof(AchievementsGui), "CloseDetails")]
        static class AchievementDetailClosed { static void Postfix(AchievementsGui __instance) { if (NativeAchievementHost(__instance)) achievementDetailRevision++; } }
        static Image AchievementBody(GameObject root, string panel, string backing)
        {
            if (root == null) return null;
            Transform foundPanel = null, foundBody = null;
            foreach (Transform child in root.transform) if (child.name == panel) { if (foundPanel != null) return null; foundPanel = child; }
            if (foundPanel == null) return null;
            foreach (Transform child in foundPanel) if (child.name == backing) { if (foundBody != null) return null; foundBody = child; }
            return foundBody != null ? foundBody.GetComponent<Image>() : null;
        }
        sealed class AchievementBacking : IDisposable
        {
            internal readonly Image Image;
            internal readonly ImageLook Look;
            readonly GameObject root;
            readonly string panel, backing;
            readonly Color tint;
            readonly Sprite original;
            readonly Material originalMaterial;
            readonly Image.Type originalType;
            readonly float originalPpu;
            Sprite assigned;
            Material assignedMaterial;
            float assignedPpu;
            internal bool Applied, Yielded;
            internal AchievementBacking(GameObject root, string panel, string backing, Image image)
            { this.root = root; this.panel = panel; this.backing = backing; Image = image; tint = image.color; original = image.sprite; originalMaterial = image.material; originalType = image.type; originalPpu = image.pixelsPerUnitMultiplier; Look = new ImageLook(image, KitSurfaceRole.Window); }
            internal void Apply(bool on)
            {
                if (Yielded) return;
                if (Image == null || AchievementBody(root, panel, backing) != Image || !Native(Image.transform, root)
                    || Image.color != tint || Applied && (Image.sprite != assigned || Image.material != assignedMaterial
                        || Image.type != Image.Type.Sliced || !Mathf.Approximately(Image.pixelsPerUnitMultiplier, assignedPpu))
                    || !Applied && (Image.sprite != original || Image.material != originalMaterial || Image.type != originalType
                        || !Mathf.Approximately(Image.pixelsPerUnitMultiplier, originalPpu)))
                { Look.Dispose(); Applied = false; Yielded = true; return; }
                Applied = Look.Apply(on);
                if (Applied) { assigned = Image.sprite; assignedMaterial = Image.material; assignedPpu = Image.pixelsPerUnitMultiplier; }
            }
            public void Dispose() { Look.Dispose(); Applied = false; }
        }
        sealed class AchievementSurface : IDisposable
        {
            internal readonly AchievementsGui Host;
            readonly InventoryGui gui;
            readonly RectTransform detailRoot;
            readonly AchievementDetailUnlockCondition detailPrefab;
            readonly List<FontLook> labels = new List<FontLook>();
            readonly List<BookButton> buttons = new List<BookButton>();
            readonly List<ImageLook> controls = new List<ImageLook>();
            readonly Dictionary<GameObject, List<FontLook>> rows = new Dictionary<GameObject, List<FontLook>>();
            readonly Dictionary<GameObject, List<FontLook>> details = new Dictionary<GameObject, List<FontLook>>();
            internal int Revision = -1, DetailRevision = -1;
            int rejectedRows, rejectedDetails;
            AchievementBacking listBacking, detailsBacking;
            string listBackingReason = "not-visible", detailsBackingReason = "not-visible";
            internal object Status => new { present = Host != null, visible = Host != null && Host.gameObject.activeInHierarchy,
                labels = labels.Count, buttons = buttons.Count, controls = controls.Count, rows = rows.Count, details = details.Count,
                rejectedRows, rejectedDetails, revision = Revision, detailRevision = DetailRevision,
                listBackingApplied = listBacking != null && listBacking.Applied, detailsBackingApplied = detailsBacking != null && detailsBacking.Applied,
                listBackingYielded = listBacking != null && listBacking.Yielded, detailsBackingYielded = detailsBacking != null && detailsBacking.Yielded,
                listBackingReason, detailsBackingReason,
                backingStyled = listBacking != null && listBacking.Applied && detailsBacking != null && detailsBacking.Applied,
                backingLimit = "Exact native54 census paths only: AchFrame/border (1) and AchDetails/border; untouched fullscreen cover/content." };
            internal AchievementSurface(InventoryGui gui, AchievementsGui host)
            {
                this.gui = gui; Host = host;
                detailRoot = achievementDetailsRoot?.GetValue(host) as RectTransform;
                detailPrefab = achievementDetailsPrefab?.GetValue(host) as AchievementDetailUnlockCondition;
                var root = host.gameObject;
                bool Shell(Transform t) => Native(t, root) && !t.IsChildOf(gui.m_achievementsListRoot)
                    && (detailRoot == null || !t.IsChildOf(detailRoot))
                    && (gui.m_achievementsElementPrefab == null || !t.IsChildOf(gui.m_achievementsElementPrefab.transform))
                    && (detailPrefab == null || !t.IsChildOf(detailPrefab.transform));
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) if (Shell(label.transform)) labels.Add(new FontLook(label));
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    if (!Shell(button.transform) || button.transform.parent == root.transform && button.name == "Closebutton") continue;
                    bool close = false;
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        var target = button.onClick.GetPersistentTarget(i); var method = button.onClick.GetPersistentMethodName(i);
                        if (target == gui && method == nameof(InventoryGui.OnCloseAchievements)
                            || target == host && method == nameof(AchievementsGui.OnCloseAchievementDetails)) close = true;
                    }
                    if (close) buttons.Add(new BookButton(button, root));
                }
                var seen = new HashSet<Image>();
                foreach (var scroll in new[] { gui.m_achievementsListScroll, achievementScroll?.GetValue(host) as Scrollbar, achievementDetailsScroll?.GetValue(host) as Scrollbar })
                {
                    var image = scroll != null && Native(scroll.transform, root) ? scroll.targetGraphic as Image : null;
                    if (image != null && Native(image.transform, root) && seen.Add(image) && !owned.Contains(image) && !KitSkin.Owns(image)) controls.Add(new ImageLook(image, KitSurfaceRole.Card));
                }
            }
            static void ClearMissing(Dictionary<GameObject, List<FontLook>> target, HashSet<GameObject> keep)
            {
                var obsolete = new List<GameObject>();
                foreach (var row in target) if (!keep.Contains(row.Key)) { foreach (var font in row.Value) font.Apply(false); obsolete.Add(row.Key); }
                foreach (var row in obsolete) target.Remove(row);
            }
            internal void Rows(int revision)
            {
                Revision = revision; rejectedRows = 0; var keep = new HashSet<GameObject>();
                foreach (var row in Host.m_achievementsList)
                {
                    if (row == null || row.transform.parent != gui.m_achievementsListRoot || !Native(row.transform, Host.gameObject)
                        || !AchievementRowShape(row, gui.m_achievementsElementPrefab)) { rejectedRows++; continue; }
                    keep.Add(row); if (rows.ContainsKey(row)) continue;
                    var fonts = new List<FontLook>();
                    foreach (var path in new[] { "name", "description" })
                    {
                        var at = row.transform.Find(path); var label = at != null && Native(at, Host.gameObject) ? at.GetComponent<TMP_Text>() : null;
                        if (label != null) fonts.Add(new FontLook(label));
                    }
                    rows.Add(row, fonts);
                }
                ClearMissing(rows, keep);
            }
            internal void Details(int revision)
            {
                DetailRevision = revision; rejectedDetails = 0; var keep = new HashSet<GameObject>();
                if (detailRoot != null && detailPrefab != null) foreach (Transform at in detailRoot)
                {
                    var row = at.GetComponent<AchievementDetailUnlockCondition>();
                    if (row == null || !Native(at, Host.gameObject) || !BookShape(at, detailPrefab.transform, 0)
                        || row.StatName == null || row.Progress == null || !Native(row.StatName.transform, Host.gameObject) || !Native(row.Progress.transform, Host.gameObject)) { rejectedDetails++; continue; }
                    keep.Add(at.gameObject); if (details.ContainsKey(at.gameObject)) continue;
                    details.Add(at.gameObject, new List<FontLook> { new FontLook(row.StatName), new FontLook(row.Progress) });
                }
                ClearMissing(details, keep);
            }
            void CaptureBacking(ref AchievementBacking target, string panel, string backing, ref string reason)
            {
                if (target != null) return;
                var image = AchievementBody(Host.gameObject, panel, backing);
                if (image == null || !image.enabled || !image.gameObject.activeInHierarchy) return;
                if (!Native(image.transform, Host.gameObject) || image.sprite == null || !image.fillCenter
                    || image.color.a <= 0 || image.canvasRenderer.GetAlpha() <= 0 || image.rectTransform.rect.width <= 0 || image.rectTransform.rect.height <= 0)
                { reason = "visible-native-backing-unverified"; return; }
                foreach (var group in image.GetComponentsInParent<CanvasGroup>(true)) if (group.alpha <= 0) return;
                if (owned.Contains(image) || KitSkin.Owns(image) || KitNativeDialogs.Owns(image) || !GamePanelTheme.TryRelease(image))
                { reason = "owned-or-native-handoff-refused"; return; }
                target = new AchievementBacking(Host.gameObject, panel, backing, image); reason = "native54-exact-visible-body";
            }
            internal void Apply(bool on)
            {
                CaptureBacking(ref listBacking, "AchFrame", "border (1)", ref listBackingReason);
                CaptureBacking(ref detailsBacking, "AchDetails", "border", ref detailsBackingReason);
                listBacking?.Apply(on); detailsBacking?.Apply(on);
                foreach (var f in labels) f.Apply(on);
                foreach (var list in rows.Values) foreach (var f in list) f.Apply(on);
                foreach (var list in details.Values) foreach (var f in list) f.Apply(on);
                foreach (var b in buttons) b.Apply(on); foreach (var i in controls) i.Apply(on);
            }
            public void Dispose()
            {
                listBacking?.Dispose(); detailsBacking?.Dispose();
                foreach (var f in labels) f.Apply(false);
                foreach (var list in rows.Values) foreach (var f in list) f.Apply(false);
                foreach (var list in details.Values) foreach (var f in list) f.Apply(false);
                foreach (var b in buttons) b.Dispose(); foreach (var i in controls) i.Dispose(); rows.Clear(); details.Clear();
            }
        }
        static bool AchievementRowShape(GameObject row, GameObject prefab)
        {
            if (prefab == null || row.transform.childCount != prefab.transform.childCount) return false;
            // Native UpdateAchievementsList removes only the root Button for nonclickable/secret rows.
            var a = row.GetComponents<Component>(); var b = prefab.GetComponents<Component>();
            var live = new List<Type>(); var original = new List<Type>();
            foreach (var c in a) if (c != null && !(c is Button)) live.Add(c.GetType());
            foreach (var c in b) if (c != null && !(c is Button)) original.Add(c.GetType());
            if (live.Count != original.Count) return false;
            for (int i = 0; i < live.Count; i++) if (live[i] != original[i]) return false;
            for (int i = 0; i < row.transform.childCount; i++) if (!BookShape(row.transform.GetChild(i), prefab.transform.GetChild(i), 0)) return false;
            return true;
        }
    }
}
