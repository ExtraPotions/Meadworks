using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ExtraPotions.Mantle.Embedded.ItemCopies;

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
	public static void Apply(string prefabName, Action<ItemDrop.ItemData.SharedData> write)
	{
		HashSet<ItemDrop.ItemData.SharedData> seen = new();
		foreach ((string name, ItemDrop.ItemData.SharedData shared) in CopyScan.Prefabs())
		{
			if (name == prefabName && seen.Add(shared))
			{
				write(shared);
			}
		}
		foreach ((string? name, ItemDrop.ItemData.SharedData shared) in CopyScan.LiveItems())
		{
			if (name == prefabName && seen.Add(shared))
			{
				write(shared);
			}
		}
	}

	/// <summary>Writes <paramref name="write"/> into every registered prefab and every live copy, each at most once, naming the prefab it belongs to.</summary>
	public static void ApplyAll(Action<string, ItemDrop.ItemData.SharedData> write)
	{
		HashSet<ItemDrop.ItemData.SharedData> seen = new();
		foreach ((string name, ItemDrop.ItemData.SharedData shared) in CopyScan.Prefabs())
		{
			if (seen.Add(shared))
			{
				write(name, shared);
			}
		}
		foreach ((string? name, ItemDrop.ItemData.SharedData shared) in CopyScan.LiveItems())
		{
			if (name != null && seen.Add(shared))
			{
				write(name, shared);
			}
		}
	}

	/// <summary>Runs <paramref name="onCopy"/> for every new item copy from now on: a world drop's Awake, an item added to an inventory.</summary>
	public static void HookSpawns(Harmony harmony, Action<ItemDrop.ItemData> onCopy) => SpawnHooks.Install(harmony, onCopy);
}
