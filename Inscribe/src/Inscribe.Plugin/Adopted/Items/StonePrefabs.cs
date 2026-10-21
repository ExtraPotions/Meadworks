// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; the legacy Awakening
// and Tempering prefabs stay registered and wake as their successor (2026-10-19).
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// The rune prefabs (prefabs.md sections 1-3): the five runes, the two legacy runes (StoneCatalog.LegacyIds), the Dvergr Chisel and the gems. Built once per process from code alone, whatever the YAML says, so every peer (server, host, clients, main
    /// menu) has the same prefab names and hashes before any inventory or ZDO arrives. Registration into the game's
    /// databases is done by <see cref="StoneRegistrationPatches"/>; this class is the registry other areas read.
    /// </summary>
    public static class StonePrefabs
    {
        private static readonly List<StoneEntry> Entries = new List<StoneEntry>();
        private static readonly Dictionary<string, StoneEntry> ByName = new Dictionary<string, StoneEntry>(StringComparer.Ordinal);
        private static readonly Dictionary<GameObject, StoneEntry> ByObject = new Dictionary<GameObject, StoneEntry>();
        private static GameObject? _holder;

        /// <summary>Raised once, right after the prefabs were built (every peer). The Items area applies the rules to them.</summary>
        internal static event Action? Built;

        public static bool IsBuilt => Entries.Count > 0;

        /// <summary>Whether a prefab of this name is one of ours (and has been built).</summary>
        public static bool IsRegistered(string? prefabName) => prefabName != null && ByName.ContainsKey(prefabName);

        /// <summary>
        /// The prefab of a rune id, or null for an unknown id or before the prefabs exist.
        /// </summary>
        public static GameObject? Get(string? stoneId)
        {
            if (stoneId == null)
            {
                return null;
            }
            string? prefab = ActiveRules.Current.Economy.Stone(stoneId)?.Prefab;
            if (prefab == null && StoneCatalog.IsBuiltIn(stoneId))
            {
                prefab = StoneCatalog.PrefabFor(stoneId);
            }
            return prefab != null && ByName.TryGetValue(prefab, out StoneEntry entry) ? entry.Prefab : null;
        }

        /// <summary>Whether the prefab is one of the rune prefabs (the rune click take-over keys on it).</summary>
        public static bool IsStonePrefab(string? prefabName) => prefabName != null && ByName.ContainsKey(prefabName);

        /// <summary>The prefab by its name (<c>ECF_Awakening</c>), or null.</summary>
        public static GameObject? GetByPrefabName(string? prefabName) =>
            prefabName != null && ByName.TryGetValue(prefabName, out StoneEntry entry) ? entry.Prefab : null;

        internal static IReadOnlyList<StoneEntry> All => Entries;

        internal static StoneEntry? EntryOf(GameObject? prefab) =>
            prefab != null && ByObject.TryGetValue(prefab, out StoneEntry entry) ? entry : null;

        /// <summary>
        /// Builds every prefab the first time a base item is reachable in either list (the object database's items or
        /// the net scene's prefabs). False while no base exists yet (the main menu's first, empty ObjectDB Awake).
        /// </summary>
        internal static bool EnsureBuilt(IList<GameObject>? first, IList<GameObject>? second)
        {
            if (IsBuilt)
            {
                return true;
            }
            if (!StoneBases.AnyPresent(first, second))
            {
                return false;
            }
            BuildAll(first, second);
            Log.Info($"{Entries.Count} rune prefabs built");
            Built?.Invoke();
            return true;
        }

        private static void BuildAll(IList<GameObject>? first, IList<GameObject>? second)
        {
            _holder = new GameObject("ExtraPotions.Inscribe.Adopted_StonePrefabs");
            global::ExtraPotions.Inscribe.Runtime.NativeGameplay.Own(_holder);
            _holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(_holder);
            GameObject?[] bases = ResolveBases(first, second);
            GameObject? material = StoneBases.Find(first, second, StoneTablets.MaterialItem);
            foreach (string id in StoneCatalog.PrefabIds)
            {
                Add(new StoneEntry(StoneCatalog.PrefabFor(id), id, StoneTablets.BaseGroup), bases, material);
            }
        }

        // One lookup (and at most one warning) per group, not per rune.
        private static GameObject?[] ResolveBases(IList<GameObject>? first, IList<GameObject>? second)
        {
            StoneGroup[] groups = (StoneGroup[])Enum.GetValues(typeof(StoneGroup));
            GameObject?[] bases = new GameObject?[groups.Length];
            foreach (StoneGroup group in groups)
            {
                bases[(int)group] = StoneBases.Resolve(group, first, second);
            }
            return bases;
        }

        private static void Add(StoneEntry entry, GameObject?[] bases, GameObject? material)
        {
            GameObject? basePrefab = bases[(int)entry.Group];
            if (basePrefab == null)
            {
                Log.Error($"no base item for rune prefab {entry.PrefabName}; it is not registered");
                return;
            }
            StoneCloner.Build(entry, basePrefab, _holder!.transform);
            entry.WearsTablet = StoneTablets.WearOriginal(entry, material);
            Entries.Add(entry);
            ByName[entry.PrefabName] = entry;
            ByObject[entry.Prefab] = entry;
        }

        /// <summary>Appends every stone prefab whose name the list does not hold yet. True when anything was added.</summary>
        internal static bool AddMissing(List<GameObject>? list)
        {
            if (list == null || Entries.Count == 0)
            {
                return false;
            }
            HashSet<string> present = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameObject go in list)
            {
                if (go != null)
                {
                    present.Add(go.name);
                }
            }
            int before = list.Count;
            foreach (StoneEntry entry in Entries)
            {
                if (!present.Contains(entry.PrefabName))
                {
                    list.Add(entry.Prefab);
                }
            }
            return list.Count != before;
        }

        /// <summary>
        /// Points a freshly woken stone item at its prefab's shared data (ItemDrop.Awake, every peer). Outside the editor
        /// every instantiated item gets its own copy of the shared data; linking them all to the prefab's one
        /// instance means a YAML change (name, stack, weight, icon) reaches every existing stack at once.
        /// </summary>
        internal static void LinkShared(ItemDrop.ItemData? item)
        {
            if (Entries.Count == 0 || item?.m_dropPrefab == null)
            {
                return;
            }
            if (ByObject.TryGetValue(item.m_dropPrefab, out StoneEntry entry) && !LegacyRuneConversion.Convert(item))
            {
                item.m_shared = entry.Shared;
            }
        }
    }
}
