using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Mantle.Embedded.BundlePrefabs;

/// <summary>
/// A mod's own sounds played the game's way. <see cref="Copy(ZNetScene, string, string, AssetBundle, string[], SfxSettings?)"/>
/// copies one of the game's sound prefabs (sfx_greydwarf_idle, sfx_ice_hit, sfx_fire_loop...) with everything that
/// makes it sit in the game's mix: its ZSFX (random clip, pitch and volume, concurrency, fades, captions), its
/// AudioSource (3D reach, roll-off curve, mixer group, priority), TimedDestruction and ZNetView. Then it puts the
/// mod's clips from its bundle in place of the game's, gives the copy its own concurrency hash, applies any
/// settings, and registers it on every peer: a sound played through an EffectList is a networked object (the machine
/// that plays the effect creates it, and nearby peers instantiate it from its ZDO by the prefab's name hash), so an
/// unregistered sound prefab would be missing on everyone else.
/// AudioClip lives in UnityEngine.AudioModule, which this library does not reference, so clips travel as an Array
/// and are set by reflection.
/// </summary>
public static class SfxPrefabs
{
	private static readonly Type ClipType = Type.GetType("UnityEngine.AudioClip, UnityEngine.AudioModule")
		?? throw new InvalidOperationException("UnityEngine.AudioClip is not loaded");
	private static readonly FieldInfo ClipsField = AccessTools.Field(typeof(ZSFX), "m_audioClips");

	/// <summary>A copy of a game sound prefab named <paramref name="name"/> playing the bundle's clips, registered in
	/// ZNetScene. Call it inside <see cref="NetPrefabs.OnSceneAwake"/>. A layered game sound (sfx_sword_swing and its
	/// overlay, sfx_door_close and its slam) keeps only its first layer; <see cref="CopyLayers"/> fills every layer.</summary>
	public static GameObject Copy(ZNetScene scene, string gamePrefab, string name, AssetBundle bundle, string[] clips,
		SfxSettings? settings = null) => CopyLayers(scene, gamePrefab, name, new[] { Load(bundle, clips) }, settings);

	/// <summary>The same with clips already loaded (an Array of AudioClip, from <see cref="Load"/>).</summary>
	public static GameObject Copy(ZNetScene scene, string gamePrefab, string name, Array clips, SfxSettings? settings = null)
		=> CopyLayers(scene, gamePrefab, name, new[] { clips }, settings);

	/// <summary>A copy whose ZSFX layers (in hierarchy order, the prefab's own first) play one clip set each; layers
	/// past the last set are removed. The settings apply to every layer kept.</summary>
	public static GameObject CopyLayers(ZNetScene scene, string gamePrefab, string name, Array[] layers,
		SfxSettings? settings = null)
	{
		GameObject copy = PrefabBench.Copy(Source(scene, gamePrefab), name);
		ZSFX[] sounds = copy.GetComponentsInChildren<ZSFX>(true);
		if (layers.Length == 0 || layers.Length > sounds.Length)
		{
			throw new InvalidOperationException($"{gamePrefab} has {sounds.Length} ZSFX layers; {layers.Length} clip sets given");
		}
		for (int i = 0; i < sounds.Length; i++)
		{
			if (i < layers.Length)
				Dress(sounds[i], layers[i], i == 0 ? name : $"{name}_{i}", settings);
			else
				UnityEngine.Object.DestroyImmediate(sounds[i].gameObject);
		}
		NetPrefabs.Register(scene, copy);
		return copy;
	}

	/// <summary>A copy of a game sound prefab that keeps the game's own clips in every layer and changes only its
	/// settings: how the game makes its variants (the Greyling plays the Greydwarf's clips at pitch 1.8 to 2.2).
	/// No bundle needed.</summary>
	public static GameObject Variant(ZNetScene scene, string gamePrefab, string name, SfxSettings settings)
	{
		ZSFX[] originals = Source(scene, gamePrefab).GetComponentsInChildren<ZSFX>(true);
		var layers = new Array[originals.Length];
		for (int i = 0; i < originals.Length; i++)
			layers[i] = (Array)ClipsField.GetValue(originals[i]);
		return CopyLayers(scene, gamePrefab, name, layers, settings);
	}

	private static GameObject Source(ZNetScene scene, string gamePrefab) =>
		scene.GetPrefab(gamePrefab) ?? throw new InvalidOperationException($"the game has no sound prefab {gamePrefab}");

	/// <summary>The named AudioClips of a bundle, as the Array ZSFX.m_audioClips takes; throws on a missing name.</summary>
	public static Array Load(AssetBundle bundle, params string[] names)
	{
		Array clips = Array.CreateInstance(ClipType, names.Length);
		for (int i = 0; i < names.Length; i++)
		{
			clips.SetValue(bundle.LoadAsset(names[i], ClipType)
				?? throw new InvalidOperationException($"{bundle.name} has no audio clip {names[i]}"), i);
		}
		return clips;
	}

	private static void Dress(ZSFX sound, Array clips, string hashName, SfxSettings? settings)
	{
		ClipsField.SetValue(sound, clips);
		// The game counts concurrent plays per hash; a copy keeping the original's would share its limit.
		sound.m_hash = hashName.GetStableHashCode();
		settings?.Apply(sound);
	}
}

/// <summary>
/// What a copied sound prefab changes from the game prefab it was copied from; everything left null is kept.
/// Set <see cref="Caption"/> for a creature's sounds: the copy otherwise captions the new creature with the old
/// one's name token ("$enemy_greydwarf"); an empty string turns the caption off.
/// </summary>
public sealed class SfxSettings
{
	public float? MinPitch { get; set; }
	public float? MaxPitch { get; set; }
	public float? MinVolume { get; set; }
	public float? MaxVolume { get; set; }
	public int? MaxConcurrent { get; set; }
	public string? Caption { get; set; }

	internal void Apply(ZSFX sound)
	{
		sound.m_minPitch = MinPitch ?? sound.m_minPitch;
		sound.m_maxPitch = MaxPitch ?? sound.m_maxPitch;
		sound.m_minVol = MinVolume ?? sound.m_minVol;
		sound.m_maxVol = MaxVolume ?? sound.m_maxVol;
		sound.m_maxConcurrentSources = MaxConcurrent ?? sound.m_maxConcurrentSources;
		sound.m_closedCaptionToken = Caption ?? sound.m_closedCaptionToken;
	}
}
