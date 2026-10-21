using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Mantle.Embedded.ItemCopies;

/// <summary>
/// Finds a prefab's <see cref="ItemDrop.ItemData.SharedData"/> and every live copy of it: the game's registered
/// item prefabs, the world's spawned <see cref="ItemDrop"/> instances, the local player's inventory and eaten
/// foods, and whichever container the local player has open. Private game members are reached through reflection
/// so consumers need not publicize the game assembly for this library's sake.
/// </summary>
internal static class CopyScan
{
	private static readonly FieldInfo? worldInstancesField = AccessTools.Field(typeof(ItemDrop), "s_instances");
	private static readonly FieldInfo? openContainerField = AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

	/// <summary>Every registered item prefab: its own name and its template shared data.</summary>
	public static IEnumerable<(string name, ItemDrop.ItemData.SharedData shared)> Prefabs()
	{
		if (ObjectDB.instance == null)
		{
			yield break;
		}
		foreach (GameObject prefab in ObjectDB.instance.m_items)
		{
			ItemDrop.ItemData.SharedData? shared = prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
			if (shared != null)
			{
				yield return (prefab.name, shared);
			}
		}
	}

	/// <summary>Every live item copy currently loaded: world drops, the local inventory, eaten foods, the open container.</summary>
	public static IEnumerable<(string? name, ItemDrop.ItemData.SharedData shared)> LiveItems()
	{
		IEnumerable<ItemDrop.ItemData> items = WorldItems().Concat(PlayerItems()).Concat(FoodItems()).Concat(ContainerItems());
		foreach (ItemDrop.ItemData item in items)
		{
			if (item.m_shared != null)
			{
				yield return (item.m_dropPrefab != null ? item.m_dropPrefab.name : null, item.m_shared);
			}
		}
	}

	private static IEnumerable<ItemDrop.ItemData> WorldItems()
	{
		List<ItemDrop>? instances = worldInstancesField?.GetValue(null) as List<ItemDrop>;
		return instances?.Where(i => i != null).Select(i => i.m_itemData) ?? Enumerable.Empty<ItemDrop.ItemData>();
	}

	private static IEnumerable<ItemDrop.ItemData> PlayerItems()
	{
		return Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory().GetAllItems() : Enumerable.Empty<ItemDrop.ItemData>();
	}

	private static IEnumerable<ItemDrop.ItemData> FoodItems()
	{
		return Player.m_localPlayer != null ? Player.m_localPlayer.GetFoods().Select(f => f.m_item) : Enumerable.Empty<ItemDrop.ItemData>();
	}

	private static IEnumerable<ItemDrop.ItemData> ContainerItems()
	{
		if (InventoryGui.instance == null)
		{
			return Enumerable.Empty<ItemDrop.ItemData>();
		}
		Container? container = openContainerField?.GetValue(InventoryGui.instance) as Container;
		return container != null ? container.GetInventory().GetAllItems() : Enumerable.Empty<ItemDrop.ItemData>();
	}
}
