// Adopted from ValheimMods ValheimModLibs/ItemCopies/Copies.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster.ItemCopies;

/// <summary>
/// Writes a mod's item values into a prefab's <see cref="ItemDrop.ItemData.SharedData"/> and into every live copy
/// of it. The game deep-clones that data into its own object for every world drop and every item loaded from a
/// save (<see cref="ItemDrop.ItemData.Clone"/>, used for a stack split, is the one path that keeps the same
/// object), so a value written only into the prefab never reaches an item already in the world. A copy reachable
/// through more than one of <see cref="CopyScan"/>'s sources is still written at most once.
/// </summary>
public static class Copies
{
	/// <summary>Writes <paramref name="write"/> into the named prefab's own shared data and into every live copy of it.</summary>
	public static void Apply(string prefabName, Action<ItemDrop.ItemData.SharedData> write) =>
		Walk(name => name == prefabName, (_, shared) => write(shared));

	/// <summary>
	/// Writes <paramref name="write"/> into several prefabs and every live copy of them in one walk of the prefabs and
	/// the live items, where one <see cref="Apply(string, Action{ItemDrop.ItemData.SharedData})"/> per name walks them
	/// once each; it is told which prefab each copy belongs to. Pass a <see cref="HashSet{T}"/> for many names.
	/// </summary>
	public static void Apply(ICollection<string> prefabNames, Action<string, ItemDrop.ItemData.SharedData> write)
	{
		if (prefabNames.Count > 0)
		{
			Walk(prefabNames.Contains, write);
		}
	}

	/// <summary>Writes <paramref name="write"/> into every registered prefab and every live copy, each at most once, naming the prefab it belongs to.</summary>
	public static void ApplyAll(Action<string, ItemDrop.ItemData.SharedData> write) => Walk(_ => true, write);

	// One walk: the registered prefabs, then the live copies; a copy without a prefab name is never written.
	private static void Walk(Func<string, bool> wanted, Action<string, ItemDrop.ItemData.SharedData> write)
	{
		HashSet<ItemDrop.ItemData.SharedData> seen = new();
		foreach ((string name, ItemDrop.ItemData.SharedData shared) in CopyScan.Prefabs())
		{
			if (wanted(name) && seen.Add(shared))
			{
				write(name, shared);
			}
		}
		foreach ((string? name, ItemDrop.ItemData.SharedData shared) in CopyScan.LiveItems())
		{
			if (name != null && wanted(name) && seen.Add(shared))
			{
				write(name, shared);
			}
		}
	}

	/// <summary>Runs <paramref name="onCopy"/> for every new item copy from now on: a world drop's Awake, an item added to an inventory.</summary>
	public static void HookSpawns(Harmony harmony, Action<ItemDrop.ItemData> onCopy) => SpawnHooks.Install(harmony, onCopy);
	public static void UnhookSpawns() => SpawnHooks.Clear();
}
