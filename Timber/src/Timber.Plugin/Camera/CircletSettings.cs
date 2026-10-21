// Cartur Build Camera. SPDX-License-Identifier: GPL-3.0-only
// Adopted from jekkle/cartur-build-camera f81977b2553d48a2a79eea62969f86bb9d9fa32e / src/DvergrCircletCameraLight.cs.
// Changed by expDARE, 2026: only colour parsing and shadow selection; existing OpenKeep copy/slot adapter retained.
using System;
using UnityEngine;
namespace Timber.Camera
{
 public enum CameraLightShadows { Original, Off, Hard, Soft }
 internal static class CircletSettings
 {
 private const string OriginalColor = "Original";
 private static string _cachedColorSetting = string.Empty;
 private static bool _cachedColorOverride;
 private static Color _cachedColor;
	internal static bool TryGetConfiguredColor(out Color color)
	{
		string setting = CameraPrefs.CircletColour.Value ?? string.Empty;
		if (setting != _cachedColorSetting) CacheConfiguredColor(setting);
		color = _cachedColor;
		return _cachedColorOverride;
	}

	private static void CacheConfiguredColor(string setting)
	{
		_cachedColorSetting = setting;
		string color = setting.Trim();
		if (string.IsNullOrEmpty(color) || color.Equals(OriginalColor, StringComparison.OrdinalIgnoreCase))
		{
			_cachedColorOverride = false;
			return;
		}

		if (!color.StartsWith("#", StringComparison.Ordinal)) color = "#" + color;
		_cachedColorOverride = ColorUtility.TryParseHtmlString(color, out _cachedColor);
	}

	internal static LightShadows GetConfiguredShadows(LightShadows sourceShadows)
	{
		return CameraPrefs.CircletShadows.Value switch
		{
			CameraLightShadows.Off => LightShadows.None,
			CameraLightShadows.Hard => LightShadows.Hard,
			CameraLightShadows.Soft => LightShadows.Soft,
			_ => sourceShadows,
		};
	}

 }
}
