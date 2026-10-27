// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// KX06 build-menu tab service. Behaviour follows OpenKeep 3.0.0's Blueprints tab (BlueprintTab, BlueprintPieceList,
// HammerTable, BlueprintSelection, BlueprintTabPatches; GPL-3.0-or-later, Copyright (C) 2026 Gerald Glasgow, reference
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1), rebuilt by expDARE in 2026 as a shared service any product registers with.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>One entry of a product's build-menu tab: an opaque id (stable across refills), a display name and an icon.</summary>
    public sealed class KitBuildTabEntry
    {
        public string Id { get; }
        /// <summary>The name the menu and the piece info show (plain text or a $ localisation token).</summary>
        public string Name { get; }
        public Sprite Icon { get; }
        public string Description { get; }

        /// <summary>
        /// An action entry (a switch, a command): a click raises <see cref="KitBuildTab.EntryClicked"/> and the entry is
        /// never selected, so the build tool keeps its piece and the menu stays open. The product refills the tab to show
        /// a new state (name, icon).
        /// </summary>
        public bool Action { get; }
        /// <summary>Eligible for file-style group selection and folder drops. Tool entries can opt out without becoming actions.</summary>
        public bool Movable { get; }

        public KitBuildTabEntry(string id, string name, Sprite icon, string description = "")
            : this(id, name, icon, description, false)
        {
        }

        /// <summary>An entry; <paramref name="action"/> makes it an action entry (<see cref="Action"/>).</summary>
        public KitBuildTabEntry(string id, string name, Sprite icon, string description, bool action)
            : this(id, name, icon, description, action, !action) { }

        public KitBuildTabEntry(string id, string name, Sprite icon, string description, bool action, bool movable)
        {
            string refusal = KitBuildTabRules.IdRefusal(id);
            if (refusal != null)
                throw new ArgumentException($"Tab entry id refused ({refusal}).", nameof(id));
            Id = id;
            Name = string.IsNullOrEmpty(name) ? id : name;
            Icon = icon;
            Description = description ?? "";
            Action = action;
            Movable = movable && !action;
        }
    }

    /// <summary>
    /// KX06: a product's tab in the game's build menu (BuildUi: By Usage, By Material, Recent, Favorites, then product tabs),
    /// for any build tool's piece table.
    /// <list type="bullet">
    /// <item>The tab is a copy of the row's own last game tab, appended to the menu's piece lists, tab buttons and tab handler
    /// (tab keys and pad bumpers step to it). It is installed on every <c>BuildUi.Awake</c>; a menu not shaped as expected
    /// gets nothing and <see cref="Refusal"/> says why. <see cref="Dispose"/> takes it out again.</item>
    /// <item>The product's view function decides each frame: hidden (keys skip it; a menu on it goes back to the first
    /// tab), shown, or blocked (C03: visible, unusable, the reason as tooltip).</item>
    /// <item>Entries (<see cref="SetEntries"/>) are hidden pieces of the kit's own in the product's piece table, under one
    /// spare build category. Only this tab lists them; the game never learns them with its "new piece" message, never
    /// places them, never removes a building with one selected, never makes them favourites or recent pieces, and never
    /// writes their names into the character file.</item>
    /// <item>The selected and hovered entry are reported (<see cref="SelectedId"/>, <see cref="HoveredId"/> and their
    /// events); a selection survives refills, and <see cref="Select"/> chooses an entry.</item>
    /// <item>Every click on an entry in the menu is reported (<see cref="EntryClicked"/>). Action entries
    /// (<see cref="KitBuildTabEntry.Action"/>) act on the click and are never selected.</item>
    /// </list>
    /// On a headless process (C10) <see cref="Register"/> returns null and nothing is patched.
    /// </summary>
    public sealed partial class KitBuildTab : IDisposable
    {
        private static readonly List<KitBuildTab> All = new List<KitBuildTab>();

        public string Owner { get; }
        public string Caption { get; }
        private readonly Func<PieceTable> table;
        private readonly Func<PieceTable, KitBuildTabView> view;
        private bool disposed;
        private bool viewFailed;
        private bool tableFailed;
        private bool tickFailed;
        private KitBuildFolders folders;

        public KitBuildFolders AttachFolders()
        {
            if (disposed) throw new ObjectDisposedException(nameof(KitBuildTab));
            TagColumn = true;
            return folders ?? (folders = new KitBuildFolders(this));
        }

        internal BuildUi CurrentMenu => menu;
        internal IReadOnlyList<string> PickOrder() {
            var ids = new List<string>();
            foreach (var go in shownObjects) { string id = go != null ? IdOf(go.GetComponent<Piece>()) : null; if (id != null && visibleIds.Contains(id)) ids.Add(id); }
            return ids;
        }
        internal static bool GroupControlActive() {
            foreach (var tab in All) if (tab.folders != null && tab.folders.Showing && KitBuildFolders.Ctrl) return true;
            return false;
        }

        internal bool Draggable(string id) => !disposed && Showing && id != null && !actionIds.Contains(id) && visibleIds.Contains(id);

        /// <summary>Current multi-selection in displayed order; independent of the piece chosen for building.</summary>
        public IReadOnlyList<string> PickedIds => folders != null ? folders.PickedIds : Array.Empty<string>();

        /// <summary>The game shows its tag column (with the search field) on this tab; the product may fill the column itself.</summary>
        public bool TagColumn { get; set; }

        /// <summary>What the tab is now, as last decided by the product's view.</summary>
        public KitBuildTabState State { get; private set; } = KitBuildTabState.Hidden;

        /// <summary>The entry selected in build mode with the tab's piece table, or null.</summary>
        public string SelectedId { get; private set; }

        /// <summary>The entry under the mouse or chosen with a pad in the open menu on this tab, or null.</summary>
        public string HoveredId { get; private set; }

        public event Action<string> SelectionChanged;
        public event Action<string> HoverChanged;

        /// <summary>
        /// An entry of this tab was clicked (or submitted with a pad) in the menu, with its id; raised before the game
        /// selects an ordinary entry. For an action entry this is the whole effect: nothing is selected, the menu stays open.
        /// </summary>
        public event Action<string> EntryClicked;

        /// <summary>The build menu opened (after it chose its tab), e.g. to return to this tab when it was the last one shown.</summary>
        public event Action Opened;

        private KitBuildTab(string owner, string caption, Func<PieceTable> table, Func<PieceTable, KitBuildTabView> view)
        {
            Owner = owner;
            Caption = caption ?? owner;
            this.table = table;
            this.view = view;
        }

        /// <summary>
        /// Registers a tab for <paramref name="owner"/> (one per owner). <paramref name="table"/> names the piece table the
        /// entries belong to (null: none yet); <paramref name="view"/> gets the local player's build tool (or null) and
        /// answers hidden, shown or blocked. Null on a headless process or before Crucible.UI is awake.
        /// </summary>
        public static KitBuildTab Register(string owner, string caption, Func<PieceTable> table, Func<PieceTable, KitBuildTabView> view)
        {
            if (!CrucibleUiPlugin.Active)
                return null;
            if (string.IsNullOrEmpty(owner))
                throw new ArgumentException("Owner required.", nameof(owner));
            if (table == null)
                throw new ArgumentNullException(nameof(table));
            if (view == null)
                throw new ArgumentNullException(nameof(view));
            foreach (KitBuildTab other in All)
                if (other.Owner == owner)
                    throw new InvalidOperationException("A build-menu tab is already registered for " + owner);
            var tab = new KitBuildTab(owner, caption, table, view);
            All.Add(tab);
            if (menu != null)
                tab.Install(menu);
            return tab;
        }

        /// <summary>The piece is an entry of any registered tab.</summary>
        public static bool IsAnyEntry(Piece piece) => Lookup(piece, out _, out _);

        /// <summary>The piece is an entry of a tab installed in the current menu (the game's own lists leave it out).</summary>
        internal static bool IsInstalledEntry(Piece piece) => Lookup(piece, out KitBuildTab tab, out _) && tab.Installed;

        /// <summary>The piece is one of this tab's entries.</summary>
        public bool IsTabEntry(Piece piece) => Lookup(piece, out KitBuildTab tab, out _) && tab == this;

        /// <summary>The piece is one of this tab's entries; <paramref name="id"/> is its id.</summary>
        public bool IsTabEntry(Piece piece, out string id)
        {
            if (Lookup(piece, out KitBuildTab tab, out string found) && tab == this)
            {
                id = found;
                return true;
            }
            id = null;
            return false;
        }

        /// <summary>The id of one of this tab's entries, or null for any other piece.</summary>
        public string IdOf(Piece piece) => IsTabEntry(piece, out string id) ? id : null;

        /// <summary>
        /// The tab lists <paramref name="shown"/> in order; <paramref name="keep"/> entries stay in the piece table without
        /// being listed (a chosen entry of another folder). Ids must be unique; names and icons may change between
        /// refills. Applied at the next frame, with the selection restored.
        /// </summary>
        public void SetEntries(IReadOnlyList<KitBuildTabEntry> shown, IReadOnlyList<KitBuildTabEntry> keep = null)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(KitBuildTab));
            if (shown == null)
                throw new ArgumentNullException(nameof(shown));
            var ids = new List<string>();
            foreach (KitBuildTabEntry entry in shown)
                ids.Add(entry?.Id ?? "");
            KitBuildTabRules.CheckEntries(ids);
            Stage(shown, keep);
        }

        /// <summary>Chooses <paramref name="id"/>: it is selected at the next frame the tab's build tool is out and it is in the table.</summary>
        public void Select(string id)
        {
            if (!disposed && KitBuildTabRules.Click(id, actionIds) != KitBuildTabClick.Act)
                selection.Choose(id);
        }

        /// <summary>
        /// The menu's click on <paramref name="piece"/> (BuildUi.OnSelectPiece prefix). Reports an entry click to its tab;
        /// false (skip the game's selection) for an action entry.
        /// </summary>
        internal static bool PieceClicked(Piece piece)
        {
            if (KitBuildFolderDrag.Suppresses(piece)) return false;
            if (!Lookup(piece, out KitBuildTab tab, out string id))
                return true;
            KitBuildTabClick click = KitBuildTabRules.Click(id, tab.actionIds);
            if (click != KitBuildTabClick.Act && tab.folders != null && tab.folders.BeforeClick(id)) return false;
            tab.Raise(tab.EntryClicked, id, "click");
            if (click != KitBuildTabClick.Act)
                return true;
            Player.m_localPlayer?.PlayButtonSound();
            return false;
        }

        internal static void SyncFolderButtons(BuildUi ui)
        {
            foreach (var button in ui.GetComponentsInChildren<BuildUiPieceButton>(true))
            {
                KitBuildFolders panel = null;
                if (Lookup(button.Piece, out var owner, out var id) && owner.folders != null && owner.Draggable(id)) panel = owner.folders;
                var drag = button.GetComponent<KitBuildFolderDrag>();
                if (panel != null && drag == null) drag = button.gameObject.AddComponent<KitBuildFolderDrag>();
                if (drag != null) { drag.Panel = panel; drag.enabled = panel != null; }
                var mark = button.GetComponent<KitBuildPickMark>();
                if (panel != null && mark == null) mark = button.gameObject.AddComponent<KitBuildPickMark>();
                if (mark != null) { mark.Panel = panel; mark.Button = button; mark.Show(panel != null && panel.IsPicked(id)); if (panel != null) panel.TrackMark(mark); }
            }
        }

        /// <summary>Switches the open menu to this tab; false when the tab is not installed, not shown or the menu is closed.</summary>
        public bool Open()
        {
            if (disposed || !Installed || State != KitBuildTabState.Shown || !menu.gameObject.activeSelf || KitBuildMenuAccess.BuildTool(menu) == null)
                return false;
            menu.SelectPieceList(index);
            return true;
        }

        private KitBuildTabView Decide(PieceTable tool)
        {
            try
            {
                return view(tool);
            }
            catch (Exception e)
            {
                if (!viewFailed)
                    CrucibleUiPlugin.Log?.Error($"Crucible.UI: build tab {Owner} view failed; the tab stays hidden: {e}");
                viewFailed = true;
                return KitBuildTabView.Hidden;
            }
        }

        private PieceTable Table()
        {
            try
            {
                return table();
            }
            catch (Exception e)
            {
                if (!tableFailed)
                    CrucibleUiPlugin.Log?.Error($"Crucible.UI: build tab {Owner} table lookup failed: {e}");
                tableFailed = true;
                return null;
            }
        }

        /// <summary>Per frame (the Crucible.UI plugin's Update): entries, view and reports of every tab.</summary>
        internal static void TickAll()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                KitBuildTab tab = All[i];
                try
                {
                    tab.Tick();
                    tab.tickFailed = false;
                }
                catch (Exception e)
                {
                    // A tab that keeps throwing is reported once, not formatted again every frame; it is reported
                    // again only after a tick succeeds.
                    if (!tab.tickFailed)
                        CrucibleUiPlugin.Log?.Error($"Crucible.UI: build tab {tab.Owner} tick failed: {e}");
                    tab.tickFailed = true;
                }
            }
        }

        private void Tick()
        {
            if (disposed)
                return;
            TickTable();
            TickView();
            TickReports();
            folders?.Tick();
        }

        /// <summary>The views alone (BuildUi.OpenBuildMenu prefix and postfix), so a reopened menu never lands on a hidden tab.</summary>
        internal static void TickViews()
        {
            foreach (KitBuildTab tab in All)
            {
                try
                {
                    tab.TickView();
                }
                catch (Exception e)
                {
                    CrucibleUiPlugin.Log?.Error($"Crucible.UI: build tab {tab.Owner} view failed: {e}");
                }
            }
        }

        internal static void MenuOpened()
        {
            foreach (KitBuildTab tab in All.ToArray())
            {
                try
                {
                    tab.Opened?.Invoke();
                }
                catch (Exception e)
                {
                    CrucibleUiPlugin.Log?.Error($"Crucible.UI: build tab {tab.Owner} open handler failed: {e}");
                }
            }
        }

        private void TickReports()
        {
            string hovered = Showing ? IdOf(KitBuildMenuAccess.Hovered(menu)?.Piece) : null;
            if (hovered != HoveredId)
            {
                HoveredId = hovered;
                Raise(HoverChanged, hovered, "hover");
            }
        }

        private void Raise(Action<string> handler, string id, string what)
        {
            try
            {
                handler?.Invoke(id);
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Error($"Crucible.UI: build tab {Owner} {what} handler failed: {e}");
            }
        }

        /// <summary>Takes the tab out of the menu and its entries out of the piece table and the player's known set.</summary>
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            folders?.Dispose();
            folders = null;
            try
            {
                ClearTable();
                Uninstall();
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Error($"Crucible.UI: build tab {Owner} teardown failed: {e}");
            }
            DestroyEntries();
            All.Remove(this);
            SelectionChanged = null;
            HoverChanged = null;
            EntryClicked = null;
            Opened = null;
        }

        /// <summary>Plugin teardown: every tab goes.</summary>
        internal static void Shutdown()
        {
            foreach (KitBuildTab tab in All.ToArray())
                tab.Dispose();
            menu = null;
        }
    }
}
