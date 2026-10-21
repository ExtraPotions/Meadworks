// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// The Rune Table's window, open or not (rune-table.md section 6). Opening shows the game's inventory screen with the
    /// table's panel in the crafting panel's place; it closes with the inventory screen (Esc, Tab, the use key, death,
    /// another screen), when the player walks away, when the table goes, when its ownership is lost or the setting is
    /// switched off. Redrawn only when something it shows changed: the player's inventory, an item's inscriptions, a
    /// click in it. Local player only.
    /// </summary>
    internal static class TableWindow
    {
        private static TablePanel? _panel;
        private static RuneTable? _table;
        private static Inventory? _watched;
        private static bool _dirty;
        private static bool _opening;
        private static bool _owned;

        // The window left up after closing while the inventory fades out; the next other Show swaps the crafting panel back.
        private static bool _lingering;
        private static float _openedAt;

        // How long the window waits for the table's ownership to arrive from the server after the owner said yes.
        private const float OwnershipWait = 5f;

        public static bool IsOpen => _table != null;

        /// <summary>Whether this machine has this table open (the owner refuses everyone else meanwhile).</summary>
        public static bool IsOpenAt(RuneTable table) => _table != null && _table == table;

        /// <summary>Whether a Show of the inventory screen is the window's own.</summary>
        public static bool Opening => _opening;

        public static bool ShiftHeld => ZInput.GetKey(UnityEngine.KeyCode.LeftShift) || ZInput.GetKey(UnityEngine.KeyCode.RightShift);

        public static void Open(RuneTable table)
        {
            InventoryGui gui = InventoryGui.instance;
            Player player = Player.m_localPlayer;
            if (gui == null || player == null)
            {
                return;
            }
            _opening = true;
            gui.Show(null, 1);
            _opening = false;
            _table = table;
            _owned = false;
            _openedAt = UnityEngine.Time.time;
            Watch(player.GetInventory());
            PanelOf(gui).Show();
            _lingering = false;
            // Filled in the frame it shows (user 2026-10-07: opening showed the crafting menu first, then the table's).
            _dirty = false;
            if (View() is { } view)
            {
                _panel!.Render(view);
            }
        }

        public static void Close() => Close(fading: false);

        /// <summary>
        /// The inventory is closing: the window stays up through its fade-out, so the crafting panel never shows behind
        /// it (user 2026-10-07: "it doesn't look janky"); <see cref="BeforeShow"/> puts the crafting panel back.
        /// </summary>
        public static void CloseFading() => Close(fading: true);

        /// <summary>Before an inventory Show that is not the table's: the window closed and the crafting panel back.</summary>
        public static void BeforeShow()
        {
            if (_opening)
            {
                return;
            }
            Close();
            if (_lingering)
            {
                _lingering = false;
                _panel?.Hide();
            }
        }

        private static void Close(bool fading)
        {
            if (_table == null)
            {
                return;
            }
            _table = null;
            Watch(null);
            HoverBox.Hide();
            TableSplit.Cancel();
            if (fading)
            {
                _lingering = true;
                return;
            }
            _panel?.Hide();
        }

        public static void MarkDirty() => _dirty = true;

        /// <summary>Runs a click's work against the open table, then redraws; a failure is logged, never thrown into the UI.</summary>
        public static void Run(Action<TableView> work)
        {
            TableView? view = View();
            if (view == null)
            {
                return;
            }
            try
            {
                work(view);
            }
            catch (Exception e)
            {
                Log.Error($"Rune Table: {e}");
            }
            _dirty = true;
        }

        /// <summary>Every frame the inventory screen is up: closes when the table can no longer be used, redraws when needed.</summary>
        public static void Tick()
        {
            if (_table == null)
            {
                return;
            }
            Player player = Player.m_localPlayer;
            if (!Usable(player))
            {
                InventoryGui.instance?.Hide();
                return;
            }
            _panel!.Follow();
            if (!_owned && _table.Store.Writable)
            {
                _owned = true;
                _table.Store.MigrateLegacy();
                _dirty = true;
            }
            if (_dirty && View() is { } view)
            {
                _dirty = false;
                _panel!.Render(view);
            }
        }

        // The table's ownership comes with the server's next update after the owner's yes: until it has come the
        // window shows but its buttons wait; once had, losing it closes the window.
        private static bool Usable(Player player)
        {
            bool owner = _table != null && _table.Store.Writable;
            bool waiting = !_owned && UnityEngine.Time.time - _openedAt < OwnershipWait;
            return player != null && _table != null && _table.IsValid && (owner || waiting) && _table.InReach(player)
                && ModSettings.RuneTable.Value && _panel != null && _panel.Alive;
        }

        private static TableView? View()
        {
            Player player = Player.m_localPlayer;
            return _table != null && player != null && _panel != null ? new TableView(_table, player, _panel.Parts) : null;
        }

        private static TablePanel PanelOf(InventoryGui gui)
        {
            if (_panel == null || !_panel.Alive)
            {
                _panel = new TablePanel(gui);
            }
            return _panel;
        }

        private static void Watch(Inventory? inventory)
        {
            if (_watched != null)
            {
                _watched.m_onChanged -= MarkDirty;
                ItemStateCache.Written -= OnWritten;
            }
            _watched = inventory;
            if (_watched != null)
            {
                _watched.m_onChanged += MarkDirty;
                ItemStateCache.Written += OnWritten;
            }
        }

        private static void OnWritten(ItemDrop.ItemData item) => _dirty = true;
    }
}
