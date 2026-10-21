// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original component selection adapter for the approved Fires/Smelters On Floors behavior; no Carturs_Flooring source copied.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Timber.Flooring
{
    public static class FirePrefabs
    {
        public static Dictionary<string, Piece> Listed()
        {
            var result = new Dictionary<string, Piece>(StringComparer.Ordinal);
            if ((!FiresRuntime.Active && !FiresRuntime.SmeltersActive) || ZNetScene.instance == null) return result;
            foreach (var prefab in ZNetScene.instance.m_namedPrefabs.Values)
            {
                if (prefab == null) continue;
                var piece = prefab.GetComponent<Piece>();
                if (piece != null && ((FiresRuntime.Active && (piece.m_comfortGroup == Piece.ComfortGroup.Fire || prefab.GetComponent<Fireplace>() != null)) ||
                    (FiresRuntime.SmeltersActive && prefab.GetComponent<Smelter>() != null)))
                    result[prefab.name] = piece;
            }
            return result;
        }
        public static Piece? Exact(string name)
        {
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            return prefab != null ? prefab.GetComponent<Piece>() : null;
        }
    }
}
