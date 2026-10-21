using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Mantle.Embedded.BundlePrefabs;

/// <summary>
/// Particle effects built in AssetWorkshop/vfx and shipped in a mod's bundle. The bundle holds each effect's prefab
/// (particle systems, lights, our textures and meshes on placeholder materials) and a recipe, &lt;name&gt;_parts, for the
/// game components a bundle cannot carry. <see cref="Prepare"/> makes an inactive copy with the placeholders dressed in
/// the game's own particle shaders (each placeholder's VfxShader tag names one; VfxFloats, VfxColours and VfxKeywords
/// hold its settings; VfxBorrow names a game prefab whose material to copy instead) and the recipe's LightFlicker,
/// LightLod, TimedDestruction and CamShaker added. An undressed placeholder still draws, in Unity's standard particle
/// shader.
/// </summary>
public static class BundleEffects
{
	private static readonly Dictionary<string, Shader?> shaders = new();
	private static readonly Dictionary<string, Type> parts = new()
	{
		["LightFlicker"] = typeof(LightFlicker), ["LightLod"] = typeof(LightLod),
		["TimedDestruction"] = typeof(TimedDestruction), ["CamShaker"] = typeof(CamShaker),
	};

	/// <summary>
	/// The effect ready to use: an inactive copy under the prefab bench (renamed when `copyName` is given), dressed and
	/// with its game components. Local by default: pass it to LocalEffects (Flash, Attach) or instantiate it on each
	/// machine from state that machine already has. Call after ZNetScene wakes, so the game's shaders are loaded.
	/// </summary>
	public static GameObject Prepare(AssetBundle bundle, string name, string? copyName = null)
	{
		GameObject copy = PrefabBench.Copy(EmbeddedBundle.Prefab(bundle, name), copyName ?? name);
		Dress(copy);
		TextAsset? recipe = bundle.LoadAsset<TextAsset>(name + "_parts");
		if (recipe != null)
		{
			AddParts(copy, recipe.text);
		}
		return copy;
	}

	/// <summary>Every placeholder material under the effect swapped for its dressed copy (trail materials too).</summary>
	public static void Dress(GameObject effect)
	{
		var dressed = new Dictionary<Material, Material>();
		foreach (Renderer renderer in effect.GetComponentsInChildren<Renderer>(true))
		{
			renderer.sharedMaterials = renderer.sharedMaterials
				.Select(m => m == null ? m! : dressed.TryGetValue(m, out Material done) ? done : dressed[m] = Dress(m)).ToArray();
		}
	}

	/// <summary>A placeholder in the game's shader with its own texture and settings; the placeholder itself when it has no
	/// VfxShader tag or the shader is not loaded.</summary>
	public static Material Dress(Material placeholder)
	{
		string shader = placeholder.GetTag("VfxShader", false, "");
		if (shader.Length == 0)
		{
			return placeholder;
		}
		Material? material = Borrowed(placeholder.GetTag("VfxBorrow", false, "")) ?? Fresh(shader);
		if (material == null)
		{
			return placeholder;
		}
		material.name = placeholder.name;
		if (placeholder.mainTexture != null)
		{
			material.mainTexture = placeholder.mainTexture;
		}
		Settings(material, placeholder);
		material.renderQueue = placeholder.renderQueue;
		return material;
	}

	private static Material? Fresh(string shader)
	{
		Shader? found = FindShader(shader);
		return found == null ? null : new Material(found);
	}

	/// <summary>"Prefab" or "Prefab/child": a copy of that game prefab's material, found in ZNetScene.</summary>
	private static Material? Borrowed(string borrow)
	{
		if (borrow.Length == 0 || ZNetScene.instance == null)
		{
			return null;
		}
		string[] path = borrow.Split(new[] { '/' }, 2);
		return GameMaterials.Borrow(ZNetScene.instance.GetPrefab(path[0]), path.Length > 1 ? path[1] : null);
	}

	/// <summary>A loaded shader by name: Shader.Find, then every loaded shader (the game's come in with its bundles).</summary>
	public static Shader? FindShader(string name)
	{
		if (!shaders.TryGetValue(name, out Shader? shader) || shader == null)
		{
			shader = Shader.Find(name) ?? Resources.FindObjectsOfTypeAll<Shader>().FirstOrDefault(s => s.name == name);
			shaders[name] = shader;
		}
		return shader;
	}

	private static void Settings(Material material, Material placeholder)
	{
		foreach (var (key, values) in Pairs(placeholder.GetTag("VfxFloats", false, "")))
		{
			if (material.HasProperty(key))
			{
				material.SetFloat(key, values[0]);
			}
		}
		foreach (var (key, values) in Pairs(placeholder.GetTag("VfxColours", false, "")))
		{
			if (material.HasProperty(key) && values.Length >= 4)
			{
				material.SetColor(key, new Color(values[0], values[1], values[2], values[3]));
			}
		}
		foreach (string keyword in placeholder.GetTag("VfxKeywords", false, "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
		{
			material.EnableKeyword(keyword);
		}
	}

	/// <summary>"key=1;key=0.5,0.5,1,1" -> (key, numbers).</summary>
	private static IEnumerable<(string key, float[] values)> Pairs(string text)
	{
		foreach (string pair in text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
		{
			string[] kv = pair.Split('=');
			if (kv.Length == 2)
			{
				yield return (kv[0], kv[1].Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray());
			}
		}
	}

	/// <summary>
	/// The recipe's game components: one line per component, "path|Component|field=value;field=value", the path from the
	/// effect's root ("" is the root), fields by the game's own names (m_flickerIntensity, m_timeout, ...).
	/// </summary>
	public static void AddParts(GameObject effect, string recipe)
	{
		foreach (string line in recipe.Split('\n'))
		{
			string[] cells = line.Trim().Split('|');
			if (cells.Length != 3 || !parts.TryGetValue(cells[1], out Type type))
			{
				continue;
			}
			Transform? node = cells[0].Length == 0 ? effect.transform : effect.transform.Find(cells[0]);
			if (node != null)
			{
				Component part = node.gameObject.AddComponent(type);
				foreach (var (key, values) in Pairs(cells[2]))
				{
					Set(part, key, values[0]);
				}
			}
		}
	}

	private static void Set(Component part, string field, float value)
	{
		FieldInfo? info = AccessTools.Field(part.GetType(), field);
		if (info == null)
		{
			return;
		}
		object boxed = info.FieldType == typeof(bool) ? value > 0.5f : info.FieldType == typeof(int) ? (int)value : value;
		info.SetValue(part, boxed);
	}

	/// <summary>
	/// For an effect that must show on every peer the way the game's own do (an entry in a creature's or item's
	/// EffectList, which only the peer that runs it instantiates): a non-persistent ZNetView, so the copy becomes a ZDO
	/// every nearby peer spawns, and registration in ZNetScene. Call inside NetPrefabs.OnSceneAwake so every peer registers
	/// it; the effect's TimedDestruction (the recipe's timeout) then removes it on the owner, which removes it everywhere.
	/// </summary>
	public static GameObject Networked(ZNetScene scene, GameObject effect)
	{
		if (effect.GetComponent<ZNetView>() == null)
		{
			effect.AddComponent<ZNetView>();
		}
		NetPrefabs.Register(scene, effect);
		return effect;
	}
}
