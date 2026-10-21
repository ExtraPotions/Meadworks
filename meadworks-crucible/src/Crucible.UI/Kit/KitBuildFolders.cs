// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// KX06 folder rows and breadcrumb lifecycle adapted from OpenKeep 3.0.0 FolderRows/FolderPanel/Breadcrumb
// at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 (GPL-3.0-or-later). Product storage stays with its owner.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>Build-tab folder presentation only. Opaque ids and all file operations belong to the product.</summary>
    public sealed partial class KitBuildFolders : IDisposable
    {
        static KitBuildFolders active;
        readonly KitBuildTab tab;
        readonly List<BuildUiTagButton> buttons = new List<BuildUiTagButton>();
        readonly List<KitFolderTarget> targets = new List<KitFolderTarget>();
        readonly List<Button> crumbs = new List<Button>();
        IReadOnlyList<KitFolderLine> rows = Array.Empty<KitFolderLine>(), chain = Array.Empty<KitFolderLine>();
        BuildUi menu;
        RectTransform strip, pieceList;
        Button create;
        Vector2 originalTop, appliedTop;
        bool shifted, hiddenAll, disposed, wasShowing, dirty = true;
        int promptGeneration, roundedWidth = -1;
        public string CurrentFolder { get; private set; } = "";
        public string NewFolderCaption { get; set; } = "+";
        public Sprite FolderIcon { get; set; }
        public Sprite UpIcon { get; set; }
        public event Action<string> OpenRequested;
        public event Action<string> RenameRequested;
        public event Action<string> CreateRequested;
        public event Action<string, string> MoveRequested;
        /// <summary>Ordered immutable group snapshot. When subscribed, receives single and grouped drops instead of MoveRequested.</summary>
        public event Action<IReadOnlyList<string>, string> MoveManyRequested;

        internal KitBuildFolders(KitBuildTab tab) { this.tab = tab; }
        internal bool Showing => !disposed && tab.Showing && tab.State == KitBuildTabState.Shown;
        bool CanName => !disposed && tab.Installed && tab.State == KitBuildTabState.Shown && Player.m_localPlayer != null;

        public void SetRows(IReadOnlyList<KitFolderLine> lines, IReadOnlyList<KitFolderLine> breadcrumbs, string currentFolder)
        {
            if (disposed) throw new ObjectDisposedException(nameof(KitBuildFolders));
            KitFolderRules.CheckRows(lines);
            KitFolderRules.CheckRows(breadcrumbs);
            RememberNavigationFocus();
            rows = new List<KitFolderLine>(lines);
            chain = new List<KitFolderLine>(breadcrumbs);
            if (CurrentFolder != (currentFolder ?? "")) ClearPicks();
            CurrentFolder = currentFolder ?? "";
            dirty = true;
        }

        /// <summary>Uses the game's text box, also with the menu closed. Tab, session and character changes invalidate the answer.</summary>
        public bool AskName(string topic, string initial, int limit, Action<string> accepted)
        {
            if (!CanName || TextInput.instance == null || TextInput.IsVisible() || accepted == null || limit < 1 || limit > 255) return false;
            int generation = ++promptGeneration;
            var actor = Player.m_localPlayer;
            long session = ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation;
            TextInput.instance.RequestText(new KitFolderName(initial, limit, name =>
            {
                if (CanName && generation == promptGeneration && ReferenceEquals(actor, Player.m_localPlayer)
                    && session == ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation) Safe(() => accepted(name));
            }), topic ?? "", limit);
            return true;
        }

        public bool TryRenameHovered()
        {
            var target = RenameTarget();
            if (!Showing || TextInput.IsVisible() || target == null || target.Panel != this || target.Line == null || !target.Line.Renames) return false;
            Rename(target.Line);
            return true;
        }

        internal void Open(KitFolderLine line)
        {
            if (Showing && !TextInput.IsVisible() && !line.Current) Safe(() => OpenRequested?.Invoke(line.Id));
        }
        internal void Rename(KitFolderLine line)
        {
            if (Showing && !TextInput.IsVisible() && line.Renames) Safe(() => RenameRequested?.Invoke(line.Id));
        }
        internal void Move(IReadOnlyList<string> entries, KitFolderLine into)
        {
            if (!Showing || TextInput.IsVisible() || into.Current || entries == null || entries.Count == 0) return;
            foreach (string entry in entries) if (!tab.Draggable(entry)) return;
            var snapshot = new List<string>(entries).AsReadOnly();
            Safe(() => { if (MoveManyRequested != null) MoveManyRequested(snapshot, into.Id);
                else foreach (string entry in snapshot) MoveRequested?.Invoke(entry, into.Id); });
            ClearPicks();
        }
        internal bool DragEntry(BuildUiPieceButton button, out string id)
        {
            id = button != null ? tab.IdOf(button.Piece) : null;
            return Showing && !TextInput.IsVisible() && tab.Draggable(id);
        }
        void Safe(Action action)
        {
            try { action(); }
            catch (Exception error) { CrucibleUiPlugin.Log?.Error($"Crucible.UI: {tab.Owner} folder action failed: {error}"); }
        }

        internal void Tick()
        {
            if (disposed) return;
            if (menu != tab.CurrentMenu) { ClearMenu(); menu = tab.CurrentMenu; dirty = true; }
            if (menu == null) return;
            TickFolderNavigation();
            TickPicks();
            bool show = Showing;
            if (show && active != this) { active?.Hide(); active = this; }
            else if (!show && active == this) active = null;
            if (!show && wasShowing) promptGeneration++;
            wasShowing = show;
            var all = Field<BuildUiTagButton>("m_showAllTagsButton");
            if (show && all != null && all.gameObject.activeSelf) { all.gameObject.SetActive(false); hiddenAll = true; }
            else if (!show && hiddenAll && all != null) { all.gameObject.SetActive(true); hiddenAll = false; }
            if (!show)
            {
                Hide();
                return;
            }
            if (!Install()) return;
            strip.gameObject.SetActive(true);
            create.gameObject.SetActive(true);
            if (!shifted) { originalTop = pieceList.offsetMax; appliedTop = originalTop - new Vector2(0, 30); pieceList.offsetMax = appliedTop; shifted = true; }
            int width = Mathf.RoundToInt(strip.rect.width);
            if (width != roundedWidth) { roundedWidth = width; dirty = true; }
            if (dirty) { RememberNavigationFocus(); DrawRows(); DrawBreadcrumbs(); dirty = false; ConfigureFolderNavigation(); }
            for (int i = 0; i < buttons.Count; i++) if (buttons[i] != null) buttons[i].gameObject.SetActive(i < rows.Count);
        }

        // The lookups are kept: Tick reads m_showAllTagsButton every frame, and AccessTools.Field searched (and allocated) each time.
        static readonly Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
        T Field<T>(string name) where T : class
        {
            if (!fields.TryGetValue(name, out FieldInfo field))
                fields[name] = field = AccessTools.Field(typeof(BuildUi), name);
            return field?.GetValue(menu) as T;
        }
        bool Install()
        {
            if (strip != null) return true;
            var scroll = Field<ScrollRect>("m_pieceScrollRect");
            pieceList = scroll != null ? scroll.transform as RectTransform : null;
            var content = Field<RectTransform>("m_tagListScrollContent");
            if (pieceList == null || pieceList.parent == null || content == null || Field<GameObject>("m_tagButtonPrefab") == null) return false;
            strip = new GameObject("Crucible_" + tab.Owner + "_breadcrumb", typeof(RectTransform)).GetComponent<RectTransform>();
            strip.SetParent(pieceList.parent, false);
            strip.anchorMin = new Vector2(0, 1); strip.anchorMax = Vector2.one; strip.pivot = new Vector2(.5f, 1);
            strip.offsetMin = new Vector2(pieceList.offsetMin.x, pieceList.offsetMax.y - 28);
            strip.offsetMax = new Vector2(pieceList.offsetMax.x, pieceList.offsetMax.y);
            create = KitButton.Create(content, "Crucible_" + tab.Owner + "_newfolder", NewFolderCaption,
                () => { if (Showing && !TextInput.IsVisible()) Safe(() => CreateRequested?.Invoke(CurrentFolder)); }, Vector2.zero, new Vector2(28, 28));
            if (create != null) create.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            InstallBox(scroll);
            return true;
        }

        void DrawRows()
        {
            var content = Field<RectTransform>("m_tagListScrollContent");
            var prefab = Field<GameObject>("m_tagButtonPrefab");
            for (int i = 0; i < rows.Count; i++)
            {
                while (buttons.Count <= i)
                {
                    var go = Object.Instantiate(prefab, content, false);
                    go.name = "Crucible_" + tab.Owner + "_folder";
                    var button = go.GetComponent<BuildUiTagButton>();
                    var target = go.AddComponent<KitFolderTarget>();
                    button.Selected += _ => target.Activate();
                    buttons.Add(button); targets.Add(target);
                }
                var line = rows[i];
                var made = buttons[i];
                made.gameObject.SetActive(true);
                made.Setup(line.Text, 900000 + i, false); made.SetToggled(line.Current);
                targets[i].Set(this, line);
                var icon = targets[i].Icon();
                icon.sprite = line.Up ? UpIcon : FolderIcon;
                icon.gameObject.SetActive(icon.sprite != null);
                icon.rectTransform.anchoredPosition = new Vector2(4 + line.Depth * 14, 0);
                foreach (var text in made.GetComponentsInChildren<TMP_Text>(true)) text.rectTransform.offsetMin = new Vector2((icon.sprite != null ? 28 : 8) + line.Depth * 14, text.rectTransform.offsetMin.y);
                made.transform.SetAsLastSibling();
            }
            if (create != null) create.transform.SetAsFirstSibling();
            if (create != null) foreach (var text in create.GetComponentsInChildren<TMP_Text>(true)) text.text = NewFolderCaption;
        }

        void DrawBreadcrumbs()
        {
            foreach (var old in crumbs) if (old != null) KitUnity.Retire(old.gameObject);
            crumbs.Clear();
            float available = Math.Max(1, strip.rect.width);
            var widths = new List<float>();
            foreach (var line in chain)
            {
                var label = KitCanvas.Text(strip, "measure", line.Text, 16, Color.white, TextAlignmentOptions.Left, false);
                widths.Add(Math.Min(available, label.preferredWidth + 20)); KitUnity.Retire(label.gameObject);
            }
            int start = KitFolderRules.BreadcrumbStart(available, widths);
            float x = 0;
            for (int i = start; i < chain.Count; i++)
            {
                var line = chain[i]; float width = widths[i];
                var button = KitButton.Create(strip, "crumb_" + i, line.Text, () => Open(line), new Vector2(x, 0), new Vector2(width, 28));
                button.gameObject.AddComponent<KitFolderTarget>().Set(this, line);
                foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) { text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Truncate; }
                crumbs.Add(button); x += width + 4;
            }
        }

        void RestoreTop()
        {
            if (shifted && pieceList != null && pieceList.offsetMax == appliedTop) pieceList.offsetMax = originalTop;
            shifted = false;
        }
        void ClearMenu()
        {
            promptGeneration++;
            ClearFolderNavigation();
            ClearPicks();
            ClearBox();
            wasShowing = false;
            if (active == this) active = null;
            RestoreTop();
            if (hiddenAll && menu != null) { var all = Field<BuildUiTagButton>("m_showAllTagsButton"); if (all != null) all.gameObject.SetActive(true); }
            hiddenAll = false;
            foreach (var row in buttons) if (row != null) KitUnity.Retire(row.gameObject);
            buttons.Clear(); targets.Clear(); crumbs.Clear();
            if (strip != null) KitUnity.Retire(strip.gameObject);
            if (create != null) KitUnity.Retire(create.gameObject);
            strip = null; create = null; pieceList = null; roundedWidth = -1;
        }
        void Hide()
        {
            ClearFolderNavigation();
            if (wasShowing) { promptGeneration++; wasShowing = false; }
            foreach (var row in buttons) if (row != null) row.gameObject.SetActive(false);
            if (strip != null) strip.gameObject.SetActive(false);
            if (create != null) create.gameObject.SetActive(false);
            if (hiddenAll && menu != null) { var all = Field<BuildUiTagButton>("m_showAllTagsButton"); if (all != null) all.gameObject.SetActive(true); hiddenAll = false; }
            RestoreTop();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; promptGeneration++; ClearMenu();
            OpenRequested = null; RenameRequested = null; CreateRequested = null; MoveRequested = null; MoveManyRequested = null;
        }
    }

    // Native sign/portal receiver, adapted from OpenKeep NamePrompt at the same pin.
    sealed class KitFolderName : TextReceiver
    {
        readonly string initial;
        readonly int limit;
        Action<string> done;
        internal KitFolderName(string initial, int limit, Action<string> done) { this.initial = initial ?? ""; this.limit = limit; this.done = done; }
        public string GetText() => initial;
        public void SetText(string text)
        {
            var name = KitFolderRules.Name(text, limit);
            var callback = done; done = null;
            if (name != null) callback?.Invoke(name);
        }
    }
}
