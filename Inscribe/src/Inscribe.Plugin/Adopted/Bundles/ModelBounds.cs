// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace and resource ownership.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Bundles;

/// <summary>
/// A model's size and place measured from its meshes, for fitting a bundle model into a copied game prefab. Renderer
/// bounds are empty while a prefab sits inactive on the <see cref="PrefabBench"/>, so the meshes' own bounds are used.
/// </summary>
public static class ModelBounds
{
	/// <summary>The box round every mesh under `model`, in `space`'s local axes; a 0.4 m box at its origin when it has none.</summary>
	public static Bounds In(GameObject model, Transform space)
	{
		Bounds bounds = default;
		bool first = true;
		foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
		{
			if (filter.sharedMesh == null)
			{
				continue;
			}
			Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
			foreach (Vector3 corner in Corners(filter.sharedMesh.bounds))
			{
				Vector3 point = toSpace.MultiplyPoint3x4(corner);
				bounds = first ? new Bounds(point, Vector3.zero) : bounds;
				bounds.Encapsulate(point);
				first = false;
			}
		}
		return first ? new Bounds(Vector3.zero, Vector3.one * 0.4f) : bounds;
	}

	private static IEnumerable<Vector3> Corners(Bounds box)
	{
		for (int i = 0; i < 8; i++)
		{
			yield return new Vector3((i & 1) == 0 ? box.min.x : box.max.x, (i & 2) == 0 ? box.min.y : box.max.y, (i & 4) == 0 ? box.min.z : box.max.z);
		}
	}
}
