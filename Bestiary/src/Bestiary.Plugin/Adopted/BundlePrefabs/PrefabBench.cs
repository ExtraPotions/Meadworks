// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later: ValheimModLibs/BundlePrefabs/PrefabBench.cs at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 (https://github.com/geraldjglasgow/ValheimMods).
// Changed by expDARE, 2026: namespace changed; no other change.
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;

/// <summary>
/// Where a mod builds its prefabs: an inactive, never-destroyed parent. A copy made under it runs no Awake (so no
/// ZNetView registers a ZDO and no Character starts up) until the game instantiates the prefab into the world, where
/// the instance is active like any other.
/// </summary>
public static class PrefabBench
{
	private static GameObject? bench;

	public static Transform Root
	{
		get
		{
			if (bench == null)
			{
				bench = new GameObject("BundlePrefabs_bench");
				bench.SetActive(false);
				Object.DontDestroyOnLoad(bench);
			}
			return bench.transform;
		}
	}

	/// <summary>An inactive copy of a prefab (the game's or a bundle's), renamed; the game hashes the name, so make it unique.</summary>
	public static GameObject Copy(GameObject prefab, string name)
	{
		GameObject copy = Object.Instantiate(prefab, Root, false);
		copy.name = name;
		return copy;
	}
}
