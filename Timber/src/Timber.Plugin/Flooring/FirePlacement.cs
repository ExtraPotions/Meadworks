#nullable disable
// OpenKeep Homestead. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/Homestead/FirePlacement.cs.
// Changed by expDARE, 2026: Timber namespace, component selection and three placement flags, conditional restore and owned reset.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Timber.Flooring
{
    /// <summary>
    /// Existing OpenKeep prefab original-flag store and live ghost refresh, extended to Timber's three fire/smelter placement flags.
    /// Reads server-authoritative fire/smelter selection from the original integration adapter; no other placement rules change.
    /// </summary>
    public static class FirePlacement
    {
        // Prefab name to m_notOnWood as it was the first time this module changed the prefab.
        private static readonly Dictionary<string, PlacementFlags> original = new Dictionary<string, PlacementFlags>(StringComparer.Ordinal);

        public static void ApplyAll()
        {
            if (ZNetScene.instance == null)
                return;
            Dictionary<string, Piece> listed = FirePrefabs.Listed();
            foreach (KeyValuePair<string, Piece> entry in listed)
                Allow(entry.Key, entry.Value);
            foreach (KeyValuePair<string, PlacementFlags> entry in original)
            {
                if (!listed.ContainsKey(entry.Key))
                    Restore(entry.Key, entry.Value);
            }
            RefreshGhost();
        }

        public static void Reset()
        {
            foreach (var entry in original) Restore(entry.Key, entry.Value);
            RefreshGhost();
            original.Clear();
        }

        // Original adapter state extends the upstream one-field capture to the agreed three placement flags.
        private readonly struct PlacementFlags
        {
            readonly bool notOnWood, groundOnly, groundPiece;
            public PlacementFlags(Piece piece)
            { notOnWood = piece.m_notOnWood; groundOnly = piece.m_groundOnly; groundPiece = piece.m_groundPiece; }
            public void Restore(Piece piece)
            {
                // Preserve a distinct later write from another plugin rather than blindly overwrite it.
                if (!piece.m_notOnWood) piece.m_notOnWood = notOnWood;
                if (!piece.m_groundOnly) piece.m_groundOnly = groundOnly;
                if (!piece.m_groundPiece) piece.m_groundPiece = groundPiece;
            }
        }

        private static void Allow(string name, Piece piece)
        {
            if (!original.ContainsKey(name))
            {
                original[name] = new PlacementFlags(piece);
            }
            piece.m_notOnWood = false;
            piece.m_groundOnly = false;
            piece.m_groundPiece = false;
        }

        private static void Restore(string name, PlacementFlags flags)
        {
            Piece piece = FirePrefabs.Exact(name);
            if (piece != null)
                flags.Restore(piece);
        }

        /// <summary>The ghost of the piece in hand copies the field from its prefab, so a change applies without reselecting.</summary>
        private static void RefreshGhost()
        {
            Player player = Player.m_localPlayer;
            GameObject ghost = player != null ? player.m_placementGhost : null;
            Piece piece = ghost != null ? ghost.GetComponent<Piece>() : null;
            Piece prefab = piece != null ? FirePrefabs.Exact(ghost.name) : null;
            if (prefab != null && original.ContainsKey(prefab.name))
            {
                piece.m_notOnWood = prefab.m_notOnWood;
                piece.m_groundOnly = prefab.m_groundOnly;
                piece.m_groundPiece = prefab.m_groundPiece;
            }
        }
    }
}
