// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Bundles;

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
            global::ExtraPotions.Inscribe.Runtime.NativeGameplay.Own(bench);
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
