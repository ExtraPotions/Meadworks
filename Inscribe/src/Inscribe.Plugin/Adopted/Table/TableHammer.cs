// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables
{
    /// <summary>
    /// The Rune Table in the hammer (rune-table.md section 2): its cost, and its place in the hammer's Crafting tab
    /// while the <c>Rune Table</c> setting is on. Refreshed when the item database or the scene wakes and when the
    /// setting changes (a server's value arriving included). Idempotent; every peer.
    /// </summary>
    [HarmonyPatch]
    internal static class TableHammer
    {
        private const string Hammer = "Hammer";

        // The cost: built at a workbench, from early materials, so the table comes with the first runes.
        private static readonly (string Item, int Amount)[] Cost = { ("Wood", 10), ("Stone", 10), ("GreydwarfEye", 5) };

        private static bool _hooked;

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void DatabaseAwake() => Refresh();

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        [HarmonyPostfix]
        private static void SceneAwake() => Refresh();

        public static void Refresh()
        {
            try
            {
                Hook();
                Apply(ObjectDB.instance);
            }
            catch (Exception e)
            {
                Log.Error($"the Rune Table could not be put in the hammer: {e.Message}");
            }
        }

        private static void Hook()
        {
            if (_hooked || ModSettings.RuneTable == null)
            {
                return;
            }
            _hooked = true;
            ModSettings.RuneTable.SettingChanged += (_, __) => Refresh();
        }

        private static void Apply(ObjectDB? database)
        {
            Piece? piece = TablePrefab.Piece;
            PieceTable? table = HammerTable(database);
            if (piece == null || table == null)
            {
                return;
            }
            piece.m_resources = Requirements(database!);
            bool wanted = ModSettings.RuneTable.Value;
            if (wanted && !table.m_pieces.Contains(piece.gameObject))
            {
                table.m_pieces.Add(piece.gameObject);
            }
            else if (!wanted)
            {
                table.m_pieces.Remove(piece.gameObject);
            }
        }

        private static PieceTable? HammerTable(ObjectDB? database)
        {
            GameObject? hammer = database != null ? database.GetItemPrefab(Hammer) : null;
            ItemDrop? item = hammer != null ? hammer.GetComponent<ItemDrop>() : null;
            return item != null ? item.m_itemData.m_shared.m_buildPieces : null;
        }

        private static Piece.Requirement[] Requirements(ObjectDB database)
        {
            var list = new List<Piece.Requirement>();
            foreach ((string item, int amount) in Cost)
            {
                ItemDrop? drop = database.GetItemPrefab(item)?.GetComponent<ItemDrop>();
                if (drop != null)
                {
                    list.Add(new Piece.Requirement { m_resItem = drop, m_amount = amount, m_recover = true });
                }
            }
            return list.ToArray();
        }
    }
}
