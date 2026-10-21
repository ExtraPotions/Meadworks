using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExtraPotions.Mantle.Embedded.BundlePrefabs;

/// <summary>
/// A new body on a copy of a game creature (the workshop's route b: a mesh modelled and weighted on an exact copy of
/// the creature's own skeleton, AssetWorkshop gamerig). The creature keeps its skeleton, avatar, animator controller,
/// clips, attacks and AI; only its main body renderer's mesh changes, rebound to the creature's own bones by name, so
/// the game's clips play the new body. The renderer stays the one <c>LevelEffects.m_mainRender</c> and
/// <c>VisEquipment.m_bodyModel</c> name (m_mainRender is pointed at it if it named another), so star-level tints and
/// skinned gear still find it, and its material 0 is the game body's material wearing the bundle body's baked textures.
/// The death ragdoll or effect is still the game creature's: a mod gives the creature its own.
/// </summary>
public static class CreatureBody
{
	/// <summary>
	/// Puts the bundle body (a prefab from the workshop: the skeleton copy with one SkinnedMeshRenderer) on the
	/// creature: its mesh, bones by name, bounds, the sockets the creature lacks, and the game body's material dressed
	/// in the body's textures (<paramref name="gloss"/> as its smoothness). Returns the creature's body renderer.
	/// Call it on a bench copy (<see cref="PrefabBench"/>) before the prefab is registered.
	/// </summary>
	public static SkinnedMeshRenderer Wear(GameObject creature, GameObject bundleBody, float gloss = 0.2f)
	{
		SkinnedMeshRenderer ours = bundleBody.GetComponentInChildren<SkinnedMeshRenderer>(true)
			?? throw new InvalidOperationException($"{bundleBody.name} has no SkinnedMeshRenderer");
		SkinnedMeshRenderer body = MainRenderer(creature)
			?? throw new InvalidOperationException($"{creature.name} has no body renderer");
		Transform visual = creature.transform.Find("Visual") ?? creature.transform;
		Dictionary<string, Transform> bones = Bones(visual);
		AddSockets(bundleBody.transform, bones);
		Rebind(body, ours, bones);
		Material game = body.sharedMaterials.Length > 0 && body.sharedMaterials[0] != null ? body.sharedMaterials[0] : ours.sharedMaterial;
		body.sharedMaterials = new[] { GameMaterials.Plain(GameMaterials.Dress(game, ours.sharedMaterial), gloss) };
		KeepMain(creature, body);
		return body;
	}

	/// <summary>LevelEffects tints material 0 of the renderer it names: make that the body, in case it named another.</summary>
	public static void KeepMain(GameObject creature, SkinnedMeshRenderer body)
	{
		LevelEffects? level = creature.GetComponentInChildren<LevelEffects>(true);
		if (level != null && level.m_mainRender != body)
		{
			level.m_mainRender = body;
		}
	}

	/// <summary>The renderer LevelEffects tints per star, else the one VisEquipment skins gear to, else the most-boned skin.</summary>
	public static SkinnedMeshRenderer? MainRenderer(GameObject creature)
	{
		LevelEffects? level = creature.GetComponentInChildren<LevelEffects>(true);
		if (level != null && level.m_mainRender is SkinnedMeshRenderer main)
		{
			return main;
		}
		VisEquipment? equipment = creature.GetComponent<VisEquipment>();
		if (equipment != null && equipment.m_bodyModel != null)
		{
			return equipment.m_bodyModel;
		}
		return creature.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.bones.Length).FirstOrDefault();
	}

	/// <summary>The body renderer takes the bundle mesh, its bind poses and bounds, and the creature's bones in the mesh's order.</summary>
	public static void Rebind(SkinnedMeshRenderer body, SkinnedMeshRenderer ours, Dictionary<string, Transform> bones)
	{
		string[] missing = ours.bones.Where(b => b == null || !bones.ContainsKey(b.name)).Select(b => b == null ? "(none)" : b.name).ToArray();
		if (missing.Length > 0)
		{
			throw new InvalidOperationException($"the creature lacks bones the body is weighted to: {string.Join(", ", missing)}");
		}
		body.sharedMesh = ours.sharedMesh;
		body.bones = ours.bones.Select(b => bones[b.name]).ToArray();
		if (ours.rootBone != null && bones.TryGetValue(ours.rootBone.name, out Transform root))
		{
			body.rootBone = root;
		}
		body.localBounds = ours.localBounds;
	}

	/// <summary>
	/// Adds under the creature every transform of the bundle body's skeleton it lacks (the sockets made in the
	/// workshop), under the bone of the same name as its parent, at the same local position, rotation and scale; the
	/// skeleton is an exact copy, so a socket lands where it was modelled. Returns how many were added.
	/// </summary>
	public static int AddSockets(Transform bundleBody, Dictionary<string, Transform> bones)
	{
		int added = 0;
		foreach (Transform node in bundleBody.GetComponentsInChildren<Transform>(true))
		{
			if (node == bundleBody || node.parent == null || bones.ContainsKey(node.name) || node.GetComponent<Renderer>() != null
				|| !bones.TryGetValue(node.parent.name, out Transform parent))
			{
				continue;
			}
			bones[node.name] = Copy(node, parent);
			added++;
		}
		return added;
	}

	/// <summary>Switches off every other mesh under the creature's Visual (the game body's eyes, a back stone ...); effects stay.</summary>
	public static void HideOthers(GameObject creature, Renderer keep)
	{
		Transform visual = creature.transform.Find("Visual") ?? creature.transform;
		foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
		{
			if (renderer != keep && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
			{
				renderer.enabled = false;
			}
		}
	}

	/// <summary>Every transform below the creature's Visual by name; the first of a name wins (bone names are unique).</summary>
	public static Dictionary<string, Transform> Bones(Transform visual)
	{
		var bones = new Dictionary<string, Transform>();
		foreach (Transform node in visual.GetComponentsInChildren<Transform>(true))
		{
			if (!bones.ContainsKey(node.name))
			{
				bones.Add(node.name, node);
			}
		}
		return bones;
	}

	private static Transform Copy(Transform source, Transform parent)
	{
		var node = new GameObject(source.name).transform;
		node.SetParent(parent, false);
		node.localPosition = source.localPosition;
		node.localRotation = source.localRotation;
		node.localScale = source.localScale;
		return node;
	}
}
