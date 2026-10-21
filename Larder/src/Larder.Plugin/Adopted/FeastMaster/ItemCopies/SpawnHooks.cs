// Adopted from ValheimMods ValheimModLibs/ItemCopies/SpawnHooks.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster.ItemCopies;

/// <summary>
/// Runs a callback for every new <see cref="ItemDrop.ItemData"/> copy: a world item's own Awake, and an item
/// successfully added to any <see cref="Inventory"/>. The patches are installed once per merged copy of this
/// library (one per consuming mod); every caller's callback runs off that same pair of patches.
/// </summary>
internal static class SpawnHooks
{
	private static readonly List<Action<ItemDrop.ItemData>> callbacks = new();
	private static bool patched;

	public static void Install(Harmony harmony, Action<ItemDrop.ItemData> onCopy)
	{
		callbacks.Add(onCopy);
		if (patched)
		{
			return;
		}
		patched = true;
		harmony.Patch(AccessTools.Method(typeof(ItemDrop), "Awake"), postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.AfterAwake)));
		MethodBase addItem = AccessTools.Method(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) });
		harmony.Patch(addItem, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.AfterAddItem)));
	}

	public static void Clear() { callbacks.Clear(); patched = false; }

	private static void Notify(ItemDrop.ItemData item)
	{
		foreach (Action<ItemDrop.ItemData> callback in callbacks)
		{
			callback(item);
		}
	}

	private static class Patches
	{
		public static void AfterAwake(ItemDrop __instance) => Notify(__instance.m_itemData);

		public static void AfterAddItem(bool __result, ItemDrop.ItemData item)
		{
			if (__result)
			{
				Notify(item);
			}
		}
	}
}
