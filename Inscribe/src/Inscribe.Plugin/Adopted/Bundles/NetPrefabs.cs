// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace and resource ownership.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Bundles;

/// <summary>
/// Registers a mod's prefabs with ZNetScene, the game's table of networked prefabs, keyed by the stable hash of the
/// prefab's name. Every peer (server and clients) runs the same registration when its ZNetScene wakes, so the hash
/// that travels in a ZDO resolves to the same prefab everywhere. A mod builds its prefabs inside the callback, when
/// the game's own prefabs (which it may copy) are available.
/// </summary>
public static class NetPrefabs
{
	private static readonly List<Action<ZNetScene>> builders = new();
	private static readonly AccessTools.FieldRef<ZNetScene, Dictionary<int, GameObject>> named =
		AccessTools.FieldRefAccess<ZNetScene, Dictionary<int, GameObject>>("m_namedPrefabs");
	private static bool patched;

	/// <summary>Runs `build` after every ZNetScene.Awake (once per game session).</summary>
	public static void OnSceneAwake(Harmony harmony, Action<ZNetScene> build)
	{
		builders.Add(build);
		if (patched)
		{
			return;
		}
		patched = true;
		harmony.Patch(AccessTools.Method(typeof(ZNetScene), "Awake"), postfix: new HarmonyMethod(typeof(NetPrefabs), nameof(AfterAwake)));
	}

	/// <summary>Adds the prefab to the scene's tables; a name already present (a second call, another mod) is left alone.</summary>
	public static bool Register(ZNetScene scene, GameObject prefab)
	{
		int hash = scene.GetPrefabHash(prefab);
		Dictionary<int, GameObject> table = named(scene);
		if (table.ContainsKey(hash))
		{
			return false;
		}
		scene.m_prefabs.Add(prefab);
		table.Add(hash, prefab);
		return true;
	}

	/// <summary>
	/// Runs every builder on its own: one that throws is logged and its prefabs are left out, and the others still run.
	/// No exception may leave this postfix: Unity switches off a component whose Awake throws, and a ZNetScene switched
	/// off creates no world object at all, so the game waits on its loading screen for ever (2026-09-28: a builder
	/// that copied a prefab the scene does not have).
	/// </summary>
	private static void AfterAwake(ZNetScene __instance)
	{
		foreach (Action<ZNetScene> build in builders)
		{
			try
			{
				build(__instance);
			}
			catch (Exception e)
			{
				Debug.LogError($"BundlePrefabs: a prefab builder failed; its prefabs are left out and the game carries on: {e}");
			}
		}
	}
}
