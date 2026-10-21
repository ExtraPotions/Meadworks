using System;
using System.Reflection;
using UnityEngine;

namespace ExtraPotions.Mantle.Embedded.BundlePrefabs;

/// <summary>
/// Recolours a copy of an effect (a game effect cloned for a new use, or one of ours): every colour it draws with turns
/// by the same hue and scales by the same saturation and value, so its light and dark parts keep their relation: the
/// particles' start colours, colour over lifetime, the custom data colours the game's gradient-mapped fire takes its
/// colours from, trail colours, lights and material tints (materials are copied first, never changed in place).
/// BundlePrefabs is not compiled against Unity's particle module, so particle systems are reached by reflection.
/// </summary>
public static class EffectTint
{
	private static readonly Type? ParticleSystemType = Type.GetType("UnityEngine.ParticleSystem, UnityEngine.ParticleSystemModule");
	private static readonly string[] MaterialColours = { "_Color", "_TintColor", "_EmissionColor" };

	/// <summary>Turns every colour by `hue` (a fraction of the colour wheel), times `saturation` and `value`.</summary>
	public static void Shift(GameObject effect, float hue, float saturation = 1f, float value = 1f)
	{
		Func<Color, Color> shift = c => Turn(c, hue, saturation, value);
		if (ParticleSystemType != null)
		{
			foreach (Component system in effect.GetComponentsInChildren(ParticleSystemType, true))
			{
				Particles(system, shift);
			}
		}
		foreach (Light light in effect.GetComponentsInChildren<Light>(true))
		{
			light.color = shift(light.color);
		}
		foreach (Renderer renderer in effect.GetComponentsInChildren<Renderer>(true))
		{
			renderer.sharedMaterials = Array.ConvertAll(renderer.sharedMaterials, m => m == null ? m! : Copy(m, shift));
		}
	}

	/// <summary>One colour turned on the wheel; brightness above 1 (the game's HDR fire colours) is kept.</summary>
	public static Color Turn(Color c, float hue, float saturation, float value)
	{
		Color.RGBToHSV(c, out float h, out float s, out float v);
		Color turned = Color.HSVToRGB(Mathf.Repeat(h + hue, 1f), Mathf.Clamp01(s * saturation), v * value, true);
		turned.a = c.a;
		return turned;
	}

	private static void Particles(Component system, Func<Color, Color> shift)
	{
		Module(system, "main", shift, "startColor");
		Module(system, "colorOverLifetime", shift, "color");
		Module(system, "trails", shift, "colorOverLifetime", "colorOverTrail");
		CustomData(system, shift);
	}

	/// <summary>Reads a module (a struct that writes through to the system), recolours its gradients, sets them back.</summary>
	private static void Module(Component system, string module, Func<Color, Color> shift, params string[] gradients)
	{
		object? handle = system.GetType().GetProperty(module)?.GetValue(system);
		if (handle == null)
		{
			return;
		}
		foreach (string name in gradients)
		{
			PropertyInfo? property = handle.GetType().GetProperty(name);
			if (property != null)
			{
				property.SetValue(handle, Recolour(property.GetValue(handle), shift));
			}
		}
	}

	private static void CustomData(Component system, Func<Color, Color> shift)
	{
		object? custom = system.GetType().GetProperty("customData")?.GetValue(system);
		if (custom == null)
		{
			return;
		}
		Type type = custom.GetType();
		MethodInfo? getMode = type.GetMethod("GetMode"), getColor = type.GetMethod("GetColor"), setColor = type.GetMethod("SetColor");
		Type? streamType = getMode?.GetParameters()[0].ParameterType;
		if (getMode == null || getColor == null || setColor == null || streamType == null)
		{
			return;
		}
		foreach (object stream in Enum.GetValues(streamType))
		{
			if (getMode.Invoke(custom, new[] { stream }).ToString() == "Color")
			{
				setColor.Invoke(custom, new[] { stream, Recolour(getColor.Invoke(custom, new[] { stream }), shift) });
			}
		}
	}

	/// <summary>A MinMaxGradient (boxed) with its colours and gradients recoloured; its mode is left as it was.</summary>
	private static object? Recolour(object? minMaxGradient, Func<Color, Color> shift)
	{
		if (minMaxGradient == null)
		{
			return null;
		}
		Type type = minMaxGradient.GetType();
		foreach (string name in new[] { "colorMin", "colorMax" })
		{
			PropertyInfo? property = type.GetProperty(name);
			if (property?.GetValue(minMaxGradient) is Color colour)
			{
				property.SetValue(minMaxGradient, shift(colour));
			}
		}
		foreach (string name in new[] { "gradientMin", "gradientMax" })
		{
			if (type.GetProperty(name)?.GetValue(minMaxGradient) is Gradient gradient)
			{
				Recolour(gradient, shift);
			}
		}
		return minMaxGradient;
	}

	private static void Recolour(Gradient gradient, Func<Color, Color> shift) =>
		gradient.SetKeys(Array.ConvertAll(gradient.colorKeys, k => new GradientColorKey(shift(k.color), k.time)), gradient.alphaKeys);

	private static Material Copy(Material source, Func<Color, Color> shift)
	{
		var material = new Material(source);
		foreach (string name in MaterialColours)
		{
			if (material.HasProperty(name))
			{
				material.SetColor(name, shift(material.GetColor(name)));
			}
		}
		return material;
	}
}
