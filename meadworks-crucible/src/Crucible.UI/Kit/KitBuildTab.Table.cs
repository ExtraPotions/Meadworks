// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// KX06 tab entries in a piece table; behaviour as OpenKeep 3.0.0 HammerTable, BlueprintEntries, BlueprintMenu and
// BlueprintSelection (see KitBuildTab.cs), rebuilt generic: entries keyed by product ids, rules in KitBuildTabRules.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public sealed partial class KitBuildTab
    {
        /// <summary>Every live entry object of every tab, for the per-piece questions the game asks many times a frame.</summary>
        private static readonly Dictionary<GameObject, (KitBuildTab Tab, string Id)> Entries = new Dictionary<GameObject, (KitBuildTab, string)>();
        private static GameObject holder;

        /// <summary>The spare build category picked per table (shared by every tab's entries in that table).</summary>
        private static readonly Dictionary<PieceTable, int> Categories = new Dictionary<PieceTable, int>();

        private readonly Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>();
        private readonly List<GameObject> shownObjects = new List<GameObject>();
        private readonly List<GameObject> tableObjects = new List<GameObject>();
        private readonly KitBuildTabSelection selection = new KitBuildTabSelection();
        private readonly KitKnownNames known = new KitKnownNames();
        private List<KitBuildTabEntry> stagedShown = new List<KitBuildTabEntry>();
        private List<KitBuildTabEntry> stagedKeep = new List<KitBuildTabEntry>();
        /// <summary>The action entries of the last refill (<see cref="KitBuildTabEntry.Action"/>).</summary>
        private HashSet<string> actionIds = new HashSet<string>(System.StringComparer.Ordinal);
        private readonly HashSet<string> visibleIds = new HashSet<string>(System.StringComparer.Ordinal);
        private bool dirty;
        private PieceTable filledTable;
        private Player trackedPlayer;

        private static bool Lookup(Piece piece, out KitBuildTab tab, out string id)
        {
            if (piece != null && Entries.TryGetValue(piece.gameObject, out var owner) && !owner.Tab.disposed)
            {
                tab = owner.Tab;
                id = owner.Id;
                return true;
            }
            tab = null;
            id = null;
            return false;
        }

        /// <summary>The piece of one of this tab's entries in the table now, or null.</summary>
        public Piece PieceOf(string id)
        {
            return id != null && objects.TryGetValue(id, out GameObject go) && go != null && tableObjects.Contains(go) ? go.GetComponent<Piece>() : null;
        }

        private void Stage(IReadOnlyList<KitBuildTabEntry> shown, IReadOnlyList<KitBuildTabEntry> keep)
        {
            stagedShown = new List<KitBuildTabEntry>(shown);
            stagedKeep = keep != null ? new List<KitBuildTabEntry>(keep) : new List<KitBuildTabEntry>();
            var actions = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (KitBuildTabEntry entry in stagedShown)
                if (entry != null && entry.Action)
                    actions.Add(entry.Id);
            foreach (KitBuildTabEntry entry in stagedKeep)
                if (entry != null && entry.Action)
                    actions.Add(entry.Id);
            actionIds = actions;
            visibleIds.Clear(); foreach (var entry in stagedShown) if (entry != null && entry.Movable) visibleIds.Add(entry.Id);
            dirty = true;
        }

        /// <summary>The tab's piece list: shown entries the table has available (another table gets nothing).</summary>
        internal void List(PieceTable pieceTable, IList<Piece> resultOut)
        {
            if (disposed || pieceTable == null || pieceTable != filledTable)
                return;
            foreach (GameObject go in shownObjects)
            {
                Piece piece = go != null ? go.GetComponent<Piece>() : null;
                if (piece != null && pieceTable.m_availablePieces.Contains(piece))
                    resultOut.Add(piece);
            }
        }

        /// <summary>Fills the table when entries, the table or the local player changed, or another mod rebuilt the table; then tracks the selection.</summary>
        private void TickTable()
        {
            PieceTable now = Table();
            Player player = Player.m_localPlayer;
            if (!ReferenceEquals(player, trackedPlayer))
            {
                trackedPlayer = player;
                known.Reset();
                selection.Reset();
                dirty = true;
            }
            if (now != filledTable)
            {
                ClearTable();
                filledTable = now;
                dirty = true;
            }
            if (now == null)
                return;
            if (dirty || !Intact(now))
                Fill(now, player);
            Track(now, player);
        }

        private bool Intact(PieceTable t)
        {
            if (tableObjects.Count == 0)
                return true;
            GameObject last = tableObjects[tableObjects.Count - 1];
            return last != null && t.m_pieces.Contains(last) && t.m_pieces.Contains(tableObjects[0]);
        }

        private void Fill(PieceTable t, Player player)
        {
            dirty = false;
            t.m_pieces.RemoveAll(go => go != null && tableObjects.Contains(go));
            var byId = new Dictionary<string, KitBuildTabEntry>();
            var shownIds = new List<string>();
            foreach (KitBuildTabEntry entry in stagedShown)
            {
                byId[entry.Id] = entry;
                shownIds.Add(entry.Id);
            }
            var keepIds = new List<string>();
            foreach (KitBuildTabEntry entry in stagedKeep)
            {
                if (entry == null || byId.ContainsKey(entry.Id))
                    continue;
                byId[entry.Id] = entry;
                keepIds.Add(entry.Id);
            }
            IReadOnlyList<string> ids = KitBuildTabRules.TableIds(shownIds, keepIds);
            int category = CategoryFor(t);
            shownObjects.Clear();
            tableObjects.Clear();
            foreach (string id in ids)
            {
                GameObject go = EntryObject(byId[id]);
                go.GetComponent<Piece>().m_category = (Piece.PieceCategory)category;
                tableObjects.Add(go);
            }
            foreach (string id in shownIds)
                shownObjects.Add(objects[id]);
            t.m_pieces.AddRange(tableObjects);
            ListCategory(t, category);
            HashSet<string> knownSet = KitBuildMenuAccess.Known(player);
            if (knownSet != null)
            {
                foreach (GameObject go in tableObjects)
                    known.Learn(knownSet, go.GetComponent<Piece>().m_name);
                KitBuildMenuAccess.UpdateAvailable(player);
                Prune();
            }
            selection.Filled();
            Redraw();
        }

        /// <summary>With the table's build tool out: the selection after a refill goes back to the chosen entry; reports changes.</summary>
        private void Track(PieceTable t, Player player)
        {
            string selected = null;
            if (player != null && player.InPlaceMode() && player.GetBuildTool() == t)
            {
                selected = IdOf(player.GetSelectedPiece());
                string restore = Restore(t, selected);
                if (restore != null && player.SetSelectedPiece(PieceOf(restore)))
                    selected = restore;
            }
            if (selected != SelectedId)
            {
                SelectedId = selected;
                Raise(SelectionChanged, selected, "selection");
            }
        }

        // Its own method: a lambda capturing t in Track allocated its closure on every call, every frame, even outside place mode.
        private string Restore(PieceTable t, string selected) => selection.Track(selected, id => PieceOf(id) != null && t.m_availablePieces.Contains(PieceOf(id)));

        /// <summary>The entry's object, made once per id under an inactive holder (its Piece never wakes) and updated from the entry.</summary>
        private GameObject EntryObject(KitBuildTabEntry entry)
        {
            if (!objects.TryGetValue(entry.Id, out GameObject go) || go == null)
            {
                go = new GameObject(KitBuildTabRules.PrefabName(Owner, entry.Id));
                go.transform.SetParent(Holder(), false);
                Piece made = go.AddComponent<Piece>();
                made.m_resources = new Piece.Requirement[0];
                made.m_canBeRemoved = false;
                made.m_canRotate = false;
                objects[entry.Id] = go;
                Entries[go] = (this, entry.Id);
            }
            Piece piece = go.GetComponent<Piece>();
            piece.m_name = entry.Name;
            piece.m_description = entry.Description;
            piece.m_icon = entry.Icon;
            return go;
        }

        /// <summary>Entry objects no longer in the table are destroyed (only once the player's lists no longer hold them).</summary>
        private void Prune()
        {
            var gone = new List<string>();
            foreach (var pair in objects)
                if (pair.Value == null || !tableObjects.Contains(pair.Value))
                    gone.Add(pair.Key);
            foreach (string id in gone)
            {
                GameObject go = objects[id];
                objects.Remove(id);
                if (go != null)
                {
                    Entries.Remove(go);
                    UnityEngine.Object.Destroy(go);
                }
            }
        }

        private static Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("Meadworks Build Tab Entries");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
            }
            return holder.transform;
        }

        /// <summary>The table's spare category, picked once per table from the categories and pieces it uses (Misc when none is spare).</summary>
        private int CategoryFor(PieceTable t)
        {
            if (Categories.TryGetValue(t, out int picked))
                return picked;
            var used = new HashSet<int>();
            foreach (Piece.PieceCategory c in t.m_categories)
                used.Add((int)c);
            foreach (GameObject go in t.m_pieces)
            {
                Piece piece = go != null ? go.GetComponent<Piece>() : null;
                if (piece != null && !Entries.ContainsKey(go))
                    used.Add((int)piece.m_category);
            }
            int spare = KitBuildTabRules.SpareCategory(used);
            picked = spare >= 0 ? spare : KitBuildTabRules.Misc;
            Categories[t] = picked;
            CrucibleUiPlugin.Log?.Info(spare >= 0 ? $"Crucible.UI: build tab entries in {t.name} use build category {(Piece.PieceCategory)picked}."
                : $"Crucible.UI: {t.name} uses every spare build category; build tab entries share Misc.");
            return picked;
        }

        /// <summary>The spare category is listed in the table while any tab's entries are in it, and taken out after.</summary>
        private void ListCategory(PieceTable t, int category)
        {
            if (category == KitBuildTabRules.Misc)
                return;
            var value = (Piece.PieceCategory)category;
            bool any = false;
            foreach (GameObject go in t.m_pieces)
                if (go != null && Entries.ContainsKey(go)) { any = true; break; }
            int at = t.m_categories.IndexOf(value);
            if (any && at < 0)
            {
                t.m_categories.Add(value);
                t.m_categoryLabels.Add(Caption);
            }
            else if (!any && at >= 0)
            {
                t.m_categories.RemoveAt(at);
                if (at < t.m_categoryLabels.Count)
                    t.m_categoryLabels.RemoveAt(at);
                if (t.GetSelectedCategory() == value && t.m_categories.Count > 0)
                    t.SetCategory(0);
            }
        }

        /// <summary>The tab's entries leave the table it filled and the player's known set.</summary>
        private void ClearTable()
        {
            if (filledTable != null)
            {
                filledTable.m_pieces.RemoveAll(go => go != null && tableObjects.Contains(go));
                if (Categories.TryGetValue(filledTable, out int category))
                {
                    tableObjects.Clear();
                    ListCategory(filledTable, category);
                }
            }
            tableObjects.Clear();
            shownObjects.Clear();
            HashSet<string> knownSet = KitBuildMenuAccess.Known(trackedPlayer);
            if (knownSet != null)
            {
                known.Unlearn(knownSet);
                KitBuildMenuAccess.UpdateAvailable(trackedPlayer);
            }
            known.Reset();
            if (SelectedId != null)
            {
                SelectedId = null;
                Raise(SelectionChanged, null, "selection");
            }
        }

        private void DestroyEntries()
        {
            foreach (GameObject go in objects.Values)
            {
                if (go == null)
                    continue;
                Entries.Remove(go);
                UnityEngine.Object.Destroy(go);
            }
            objects.Clear();
        }

        /// <summary>Player.AddKnownPiece prefix: an entry becomes known without the game's message or a recent-piece record.</summary>
        internal static bool LearnQuietly(Player player, Piece piece)
        {
            if (!Lookup(piece, out KitBuildTab tab, out _))
                return true;
            HashSet<string> knownSet = KitBuildMenuAccess.Known(player);
            if (knownSet != null && player == tab.trackedPlayer)
                tab.known.Learn(knownSet, piece.m_name);
            return false;
        }

        /// <summary>Player.Save prefix: every tab's names leave the known set for the save; <see cref="AfterSave"/> puts them back.</summary>
        internal static List<(KitBuildTab, IReadOnlyList<string>)> BeforeSave(Player player)
        {
            var withdrawn = new List<(KitBuildTab, IReadOnlyList<string>)>();
            HashSet<string> knownSet = KitBuildMenuAccess.Known(player);
            if (knownSet == null)
                return withdrawn;
            foreach (KitBuildTab tab in All)
                if (player == tab.trackedPlayer)
                    withdrawn.Add((tab, tab.known.Withdraw(knownSet)));
            return withdrawn;
        }

        internal static void AfterSave(Player player, List<(KitBuildTab Tab, IReadOnlyList<string> Names)> withdrawn)
        {
            HashSet<string> knownSet = KitBuildMenuAccess.Known(player);
            if (knownSet == null || withdrawn == null)
                return;
            foreach (var pair in withdrawn)
                pair.Tab.known.Restore(knownSet, pair.Names);
        }
    }
}
