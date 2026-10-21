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
        static readonly FieldInfo skillRows = typeof(SkillsDialog).GetField("m_elements", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo textRows = typeof(TextsDialog).GetField("m_texts", BindingFlags.Instance | BindingFlags.NonPublic);
        static BookSurface skillsBook, textsBook;
        static int skillsRevision, textsRevision, bookAppearanceGeneration;
        static void MarkBooksChanged() { bookAppearanceGeneration++; }
        internal static object BooksStatus => new { skills = skillsBook?.Status, texts = textsBook?.Status };

        static void TickBooks()
        {
            if (!MeadworksHeadless.AllowsUi) { ShutdownBooks(); return; }
            var gui = InventoryGui.instance;
            var skills = gui != null ? gui.m_skillsDialog : null;
            var texts = gui != null ? gui.m_textsDialog : null;
            if (skillsBook != null && skillsBook.Root != (skills != null ? skills.gameObject : null)) { skillsBook.Dispose(); skillsBook = null; }
            if (textsBook != null && textsBook.Root != (texts != null ? texts.gameObject : null)) { textsBook.Dispose(); textsBook = null; }
            if (skillsBook == null && skills != null && skills.gameObject.activeInHierarchy)
                skillsBook = new BookSurface("inventory.skills", skills.gameObject, skills.m_listRoot, skills.m_elementPrefab);
            if (textsBook == null && texts != null && texts.gameObject.activeInHierarchy)
                textsBook = new BookSurface("inventory.texts", texts.gameObject, texts.m_listRoot, texts.m_elementPrefab);
            if (skillsBook != null)
            {
                if (skillsBook.Revision != skillsRevision)
                    skillsBook.Rows(skillRows?.GetValue(skills) as List<GameObject>, skillsRevision);
                skillsBook.Apply(On);
            }
            if (textsBook != null)
            {
                if (textsBook.Revision != textsRevision)
                {
                    var rows = new List<GameObject>();
                    var entries = textRows?.GetValue(texts) as List<TextsDialog.TextInfo>;
                    if (entries != null) foreach (var entry in entries) rows.Add(entry.m_listElement);
                    textsBook.Rows(entries != null ? rows : null, textsRevision);
                }
                textsBook.Apply(On);
            }
        }
        static void ShutdownBooks()
        {
            skillsBook?.Dispose(); textsBook?.Dispose(); skillsBook = textsBook = null;
        }
        static bool ReservesBooks(Image image)
        {
            var gui = InventoryGui.instance;
            if (gui == null || image == null) return false;
            return gui.m_skillsDialog != null && BookNative(image.transform, gui.m_skillsDialog.gameObject)
                || gui.m_textsDialog != null && BookNative(image.transform, gui.m_textsDialog.gameObject);
        }
        [HarmonyPatch(typeof(SkillsDialog), nameof(SkillsDialog.Setup))]
        static class SkillsBookSetup
        {
            static void Postfix(SkillsDialog __instance)
            { if (InventoryGui.instance != null && InventoryGui.instance.m_skillsDialog == __instance) skillsRevision++; }
        }
        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.Setup))]
        static class TextsBookSetup
        {
            static void Postfix(TextsDialog __instance)
            { if (InventoryGui.instance != null && InventoryGui.instance.m_textsDialog == __instance) textsRevision++; }
        }
        static bool BookNative(Transform at, GameObject root)
        {
            for (var parent = at; parent != null; parent = parent.parent)
            {
                if (parent.name.StartsWith("Saga_", StringComparison.Ordinal)) return false;
                if (parent == root.transform) return Native(at, root);
            }
            return false;
        }
        sealed class BookSurface : IDisposable
        {
            internal readonly GameObject Root;
            readonly string id;
            readonly RectTransform list;
            readonly GameObject prefab;
            readonly BookPart fixedPart;
            readonly Dictionary<GameObject, BookPart> rows = new Dictionary<GameObject, BookPart>();
            internal int Revision { get; private set; } = -1;
            int rejected, generation = 1, paintedGeneration, seenAppearanceGeneration = bookAppearanceGeneration;
            bool paintedOn;
            string reason = "not-reconciled";
            internal object Status => new { id, present = Root != null, visible = Root != null && Root.activeInHierarchy,
                captured = fixedPart.Count, rows = rows.Count, rejectedRows = rejected, revision = Revision, reason };
            internal BookSurface(string id, GameObject root, RectTransform list, GameObject prefab)
            { this.id = id; Root = root; this.list = list; this.prefab = prefab; fixedPart = new BookPart(root, root, list, false); }
            internal void Rows(List<GameObject> live, int revision)
            {
                rejected = 0; Revision = revision; generation++;
                var keep = new HashSet<GameObject>();
                if (prefab == null || live == null || !BookNative(prefab.transform, prefab))
                {
                    foreach (var row in rows.Values) row.Dispose(); rows.Clear();
                    reason = "native-row-roster-or-prefab-unavailable"; return;
                }
                foreach (var row in live)
                {
                    if (row == null || row.transform.parent != list || !BookNative(row.transform, Root)
                        || !BookShape(row.transform, prefab.transform, 0)) { rejected++; continue; }
                    keep.Add(row);
                    if (!rows.ContainsKey(row)) rows.Add(row, new BookPart(row, Root, null, true));
                }
                var obsolete = new List<GameObject>();
                foreach (var row in rows) if (!keep.Contains(row.Key)) { row.Value.Dispose(); obsolete.Add(row.Key); }
                foreach (var row in obsolete) rows.Remove(row);
                reason = rejected == 0 ? "native-roster-reconciled" : "unknown-rows-declined";
            }
            internal void Apply(bool on)
            {
                if (seenAppearanceGeneration != bookAppearanceGeneration)
                { seenAppearanceGeneration = bookAppearanceGeneration; generation++; }
                if (paintedOn != on) generation++;
                if (paintedGeneration != generation)
                {
                    fixedPart.Apply(on); foreach (var row in rows.Values) row.Apply(on);
                    paintedOn = on; paintedGeneration = generation;
                }
                else
                {
                    fixedPart.FollowText(on); foreach (var row in rows.Values) row.FollowText(on);
                }
            }
            public void Dispose()
            { fixedPart.Dispose(); foreach (var row in rows.Values) row.Dispose(); rows.Clear(); }
        }
        static bool BookShape(Transform live, Transform prefab, int depth)
        {
            if (depth > 16) return false;
            var a = live.GetComponents<Component>(); var b = prefab.GetComponents<Component>();
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] == null || b[i] == null || a[i].GetType() != b[i].GetType()) return false;
            int index = 0;
            foreach (Transform child in live)
            {
                if (child.name.StartsWith("Crucible_native_text_button_", StringComparison.Ordinal)
                    && child.GetComponent<KitNativeMenuTextButton>() != null) continue;
                if (index >= prefab.childCount) return false;
                var original = prefab.GetChild(index++);
                if (child.name != original.name || !BookShape(child, original, depth + 1)) return false;
            }
            return index == prefab.childCount;
        }
        sealed class BookPart : IDisposable
        {
            readonly List<ImageLook> images = new List<ImageLook>();
            readonly List<BookButton> buttons = new List<BookButton>();
            readonly List<FontLook> fonts = new List<FontLook>();
            internal int Count => images.Count + buttons.Count + fonts.Count;
            internal BookPart(GameObject part, GameObject root, RectTransform exclude, bool nativeRow)
            {
                foreach (var button in part.GetComponentsInChildren<Button>(true))
                {
                    if (exclude != null && button.transform.IsChildOf(exclude) || !BookNative(button.transform, root)) continue;
                    bool nativeAction = nativeRow;
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        var target = button.onClick.GetPersistentTarget(i);
                        if (target != null && target.GetType().Assembly == typeof(Menu).Assembly) nativeAction = true;
                    }
                    if (nativeAction) buttons.Add(new BookButton(button, root));
                }
                if (nativeRow)
                {
                    var selected = part.transform.Find("selected");
                    var image = selected != null && BookNative(selected, root) ? selected.GetComponent<Image>() : null;
                    if (image != null && !owned.Contains(image) && !KitSkin.Owns(image)) images.Add(new ImageLook(image, KitSurfaceRole.ButtonHover));
                }
                foreach (var scrollbar in part.GetComponentsInChildren<Scrollbar>(true))
                {
                    if (exclude != null && scrollbar.transform.IsChildOf(exclude) || !BookNative(scrollbar.transform, root)) continue;
                    var handle = scrollbar.targetGraphic as Image;
                    if (handle != null && BookNative(handle.transform, root) && !owned.Contains(handle) && !KitSkin.Owns(handle))
                        images.Add(new ImageLook(handle, KitSurfaceRole.Card));
                }
                foreach (var image in part.GetComponentsInChildren<Image>(true))
                {
                    if (exclude != null && image.transform.IsChildOf(exclude) || !BookNative(image.transform, root)
                        || owned.Contains(image) || KitSkin.Owns(image) || KitNativeDialogs.Owns(image)) continue;
                    if (image.GetComponentInParent<Selectable>() == null && image.fillCenter && image.sprite != null
                        && (image.type == Image.Type.Sliced || image.type == Image.Type.Tiled) && KitThemeRules.IsWoodSprite(image.sprite.name))
                        images.Add(new ImageLook(image, KitSurfaceRole.Card, protectOffState: true));
                }
                foreach (var label in part.GetComponentsInChildren<TMP_Text>(true))
                    if (!(exclude != null && label.transform.IsChildOf(exclude)) && BookNative(label.transform, root)) fonts.Add(new FontLook(label));
            }
            internal void Apply(bool on)
            { foreach (var image in images) image.Apply(on); foreach (var button in buttons) button.Apply(on); foreach (var font in fonts) font.Apply(on); }
            internal void FollowText(bool on)
            { foreach (var button in buttons) button.FollowText(on); }
            public void Dispose()
            { foreach (var image in images) image.Dispose(); foreach (var button in buttons) button.Dispose(); foreach (var font in fonts) font.Apply(false); }
        }
        sealed class BookButton : IDisposable
        {
            readonly Button button;
            readonly Graphic graphic;
            readonly ButtonLook image;
            readonly TextButtonLook text;
            bool yielded;
            internal BookButton(Button button, GameObject root)
            {
                this.button = button; graphic = button.targetGraphic;
                if (graphic != null && (!graphic.transform.IsChildOf(button.transform) || !BookNative(graphic.transform, root))) return;
                if (graphic is Image target && !owned.Contains(target) && !KitSkin.Owns(target)) image = new ButtonLook(button, target);
                else if ((graphic == null || graphic is TMP_Text) && TextButtonLook.Supported)
                {
                    TMP_Text label = graphic as TMP_Text; int count = label != null ? 1 : 0;
                    if (label == null) foreach (var candidate in button.GetComponentsInChildren<TMP_Text>(true))
                        if (candidate.GetComponentInParent<Button>() == button && BookNative(candidate.transform, root)) { label = candidate; count++; }
                    if (count == 1) text = new TextButtonLook(button, label);
                }
            }
            internal void Apply(bool on)
            {
                if (yielded || button == null) return;
                if (button.targetGraphic != graphic) { Dispose(); yielded = true; return; }
                image?.Apply(on); text?.Apply(on);
            }
            internal void FollowText(bool on)
            {
                if (yielded || button == null || text == null) return;
                if (button.targetGraphic != graphic) { Dispose(); yielded = true; return; }
                text.Apply(on);
            }
            public void Dispose() { image?.Dispose(); text?.Dispose(); }
        }
    }
}
