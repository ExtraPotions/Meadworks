// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the live reader of the Protected Prefabs setting (server-authoritative through WildbloodConfig).
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Stars;
using UnityEngine;

namespace ExtraPotions.Wildblood.Runtime
{
    /// <summary>
    /// Creatures on the Protected Prefabs list are never starred, traited or scaled (design A11). The list is read through the
    /// server-effective setting, at most once per frame because modifier hooks ask per hit. Creatures already rolled stay as they
    /// are; the list governs creatures that load afterwards.
    /// </summary>
    internal static class ProtectedCreatures
    {
        static int frame = -1;
        static string list = "";

        static string List()
        {
            int now = Time.frameCount;
            if (now != frame)
            {
                frame = now;
                list = Plugin.Settings != null ? Plugin.Settings.Get(WildbloodSettings.General.ProtectedPrefabs) : WildbloodSettings.General.ProtectedPrefabs.Default;
            }
            return list;
        }

        internal static bool IsProtected(string? prefabName) => ProtectedPrefabs.Contains(List(), prefabName);

        internal static bool IsProtected(GameObject? gameObject) => gameObject != null && IsProtected(Utils.GetPrefabName(gameObject));

        /// <summary>By the ZDO's prefab hash, for creatures whose object is not (yet) there.</summary>
        internal static bool IsProtected(ZDO? zdo)
        {
            int hash = zdo != null ? zdo.GetPrefab() : 0;
            if (hash == 0 || ZNetScene.instance == null) return false;
            GameObject? prefab = ZNetScene.instance.GetPrefab(hash);
            return prefab != null && IsProtected(prefab.name);
        }
    }
}
