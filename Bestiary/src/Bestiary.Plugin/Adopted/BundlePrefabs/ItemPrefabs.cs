// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later: ValheimModLibs/BundlePrefabs/ItemPrefabs.cs at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 (https://github.com/geraldjglasgow/ValheimMods).
// Changed by expDARE, 2026: namespace, callback cleanup and exact owned registration tracking.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;

/// <summary>
/// Keeps a mod's item prefabs (an ItemDrop on the root) in ObjectDB: in the live database now, and in every database
/// the game builds or copies later (the main menu's, then the world's). The game looks items up there by name hash.
/// </summary>
public static class ItemPrefabs
{
	private static readonly List<GameObject> items = new();
	private static readonly AccessTools.FieldRef<ObjectDB, Dictionary<int, GameObject>> byHash =
		AccessTools.FieldRefAccess<ObjectDB, Dictionary<int, GameObject>>("m_itemByHash");
	private static bool patched;

	public static void Register(Harmony harmony, GameObject item)
	{
		if (!items.Contains(item))
		{
			items.Add(item);
		}
		Patch(harmony);
		if (ObjectDB.instance != null)
		{
			AddAll(ObjectDB.instance);
		}
	}

	private static void Patch(Harmony harmony)
	{
		if (patched)
		{
			return;
		}
		patched = true;
		var after = new HarmonyMethod(typeof(ItemPrefabs), nameof(AfterBuilt));
		harmony.Patch(AccessTools.Method(typeof(ObjectDB), "Awake"), postfix: after);
		harmony.Patch(AccessTools.Method(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB)), postfix: after);
	}

	private static void AfterBuilt(ObjectDB __instance) => AddAll(__instance);

    internal static void ResetCallbacks() { items.Clear(); patched = false; }
	private static void AddAll(ObjectDB db)
	{
		Dictionary<int, GameObject> table = byHash(db);
		foreach (GameObject item in items)
		{
			int hash = item.name.GetStableHashCode();
			if (!table.ContainsKey(hash))
			{
				db.m_items.Add(item);
				table.Add(hash, item);
                Native.Adapters.NativeRuntime.Item(db, item);
			}
		}
	}
}
