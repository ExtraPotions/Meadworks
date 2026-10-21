// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGatePieceRecipe.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>What the Sea Gate Pillar costs, from <see cref="SeaGateConfig.PillarRecipe"/> ("Stone:10,FineWood:5"):
    /// item prefab names with amounts, looked up in the item database. Every entry is given back when the pillar is
    /// taken down. Unknown items and bad amounts are skipped with one warning each; a recipe with no usable entry falls
    /// back to the setting's default. The pillar is in the build menu only while Wayfare and sea gates are on; pillars
    /// already built stand either way. Applied when the prefab is installed, and again on every machine whenever one of
    /// the settings changes, since a server's synced values arrive after startup.</summary>
    public static class SeaGatePieceRecipe
    {
        private static readonly HashSet<string> warned = new HashSet<string>();
        private static bool watching;

        /// <summary>Writes the current recipe and availability into the pillar prefab's Piece and starts following the
        /// settings. Needs the item database: before it exists (the start scene) the next install applies it.</summary>
        public static void Apply()
        {
            Watch();
            Piece piece = SeaGatePiece.Piece;
            if (piece == null || ObjectDB.instance == null)
                return;
            piece.m_resources = Parse(SeaGateConfig.PillarRecipe.Value);
            piece.m_enabled = SeaGateConfig.On;
            applied = SeaGateConfig.PillarRecipe.Value + "|" + piece.m_enabled;
        }

        /// <summary>The requirements a recipe text names; the default recipe's when none of its entries is usable.</summary>
        public static Piece.Requirement[] Parse(string text)
        {
            List<Piece.Requirement> requirements = Read(text);
            if (requirements.Count == 0)
                requirements = Read((string)SeaGateConfig.PillarRecipe.DefaultValue);
            return requirements.ToArray();
        }

        private static List<Piece.Requirement> Read(string text)
        {
            List<Piece.Requirement> requirements = new List<Piece.Requirement>();
            foreach (string part in (text ?? "").Split(','))
            {
                if (part.Trim().Length == 0)
                    continue;
                Piece.Requirement requirement = Entry(part.Trim());
                if (requirement != null)
                    requirements.Add(requirement);
            }
            return requirements;
        }

        /// <summary>One "Name:Amount" entry; null (with one warning) when the amount is not a whole number above 0 or
        /// the item is unknown.</summary>
        private static Piece.Requirement Entry(string part)
        {
            string[] fields = part.Split(':');
            if (fields.Length != 2 || !int.TryParse(fields[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) || amount < 1)
            {
                Warn(part, $"Sea Gate Pillar recipe: \"{part}\" is not Item:Amount with an amount above 0; skipped.");
                return null;
            }
            ItemDrop item = Item(fields[0].Trim());
            if (item == null)
            {
                Warn(fields[0].Trim(), $"Sea Gate Pillar recipe: no item named \"{fields[0].Trim()}\"; skipped.");
                return null;
            }
            return new Piece.Requirement { m_resItem = item, m_amount = amount, m_amountPerLevel = 0, m_recover = true };
        }

        private static ItemDrop Item(string prefabName)
        {
            if (prefabName.Length == 0 || ObjectDB.instance == null)
                return null;
            GameObject prefab = ObjectDB.instance.GetItemPrefab(prefabName);
            return prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        }

        private static void Warn(string key, string message)
        {
            if (warned.Add(key))
                SeaGateLog.LogWarning(message);
        }

        /// <summary>The recipe and the switch as last applied; the server's synced values arrive after startup.</summary>
        private static string applied;

        private static void Watch() => watching = true;

        /// <summary>Every frame on every machine (from the sea gate driver): applies again when the recipe or the switch
        /// changed.</summary>
        internal static void Tick()
        {
            if (!watching || SeaGatePiece.Piece == null || ObjectDB.instance == null)
                return;
            string now = SeaGateConfig.PillarRecipe.Value + "|" + SeaGateConfig.On;
            if (now == applied)
                return;
            try
            {
                Apply();
            }
            catch (Exception e)
            {
                SeaGateLog.LogError($"Sea Gate Pillar recipe update failed: {e}");
            }
        }
    }
}
