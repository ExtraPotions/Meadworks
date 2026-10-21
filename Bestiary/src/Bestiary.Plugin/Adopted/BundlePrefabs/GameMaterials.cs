// Portions Copyright (C) 2026 Gerald Glasgow.
// EliteCreaturesPack 0.7.2 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: ValheimModLibs/BundlePrefabs/GameMaterials.cs
// Changed by expDARE, 2026: namespace isolation for Bestiary; reference algorithms retained.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;

/// <summary>
/// Gives a mod's bundle parts the game's own look at runtime instead of shipping copies of its textures: borrow a
/// material from a game prefab (its textures, shader and settings), then put materials on a model by the names its
/// placeholder materials carry.
/// </summary>
public static class GameMaterials
{
	/// <summary>A copy of the first material on the prefab's renderers (or on the named child's), or null.</summary>
	public static Material? Borrow(GameObject? prefab, string? child = null)
	{
		if (prefab == null)
		{
			return null;
		}
		Transform root = child == null ? prefab.transform : Find(prefab.transform, child) ?? prefab.transform;
		Renderer? renderer = root.GetComponentInChildren<Renderer>(true);
		return renderer == null || renderer.sharedMaterial == null ? null : new Material(renderer.sharedMaterial);
	}

	/// <summary>Replaces each placeholder material (by name) with the one `pick` returns; null keeps the placeholder.</summary>
	public static void Apply(GameObject model, Func<string, Material?> pick)
	{
		foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
		{
			Material[] materials = renderer.sharedMaterials;
			for (int i = 0; i < materials.Length; i++)
			{
				materials[i] = pick(materials[i].name.Replace(" (Instance)", "")) ?? materials[i];
			}
			renderer.sharedMaterials = materials;
		}
	}

	/// <summary>
	/// A copy of a game material (its shader, lighting and settings) wearing a bundle placeholder's own baked textures:
	/// its albedo and, when it has one, its normal map. For a model textured in the workshop that should still be lit
	/// the way the game lights the thing it belongs to.
	/// </summary>
	public static Material Dress(Material game, Material placeholder)
	{
		var material = new Material(game) { name = placeholder.name, color = Color.white };
		material.mainTexture = placeholder.mainTexture;
		material.mainTextureScale = Vector2.one;
		material.mainTextureOffset = Vector2.zero;
		if (material.HasProperty("_BumpMap"))
		{
			material.SetTexture("_BumpMap", placeholder.HasProperty("_BumpMap") ? placeholder.GetTexture("_BumpMap") : null);
		}
		return material;
	}

	/// <summary>
	/// A dressed material (<see cref="Dress"/>) without the game maps it kept that are laid out for the game model's
	/// UVs, not the workshop model's: metal and gloss, glow, style variants. No metal, the given gloss, no glow, no
	/// styles (a material with styles on, such as a painted shield's, draws its emptied style map as plain white). The
	/// game's creature and item shader (<c>Custom/Creature</c>) has all of these; any it lacks are skipped.
	/// </summary>
	public static Material Plain(Material material, float gloss)
	{
		foreach (string map in new[] { "_MetallicGlossMap", "_EmissionMap", "_StyleTex" })
		{
			if (material.HasProperty(map))
			{
				material.SetTexture(map, null);
			}
		}
		SetFloat(material, "_UseStyles", 0f);
		material.DisableKeyword("_USESTYLES_ON");
		SetFloat(material, "_Metallic", 0f);
		SetFloat(material, "_MetalGloss", 0f);
		SetFloat(material, "_Glossiness", gloss);
		if (material.HasProperty("_EmissionColor"))
		{
			material.SetColor("_EmissionColor", Color.black);
		}
		material.DisableKeyword("_EMISSION");
		return material;
	}

	private static void SetFloat(Material material, string name, float value)
	{
		if (material.HasProperty(name))
		{
			material.SetFloat(name, value);
		}
	}

	/// <summary>A transform anywhere below root, by name.</summary>
	public static Transform? Find(Transform root, string name)
	{
		foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
		{
			if (child.name == name)
			{
				return child;
			}
		}
		return null;
	}
}
