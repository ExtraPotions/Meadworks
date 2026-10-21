#nullable disable
// Cartur Build Camera. SPDX-License-Identifier: GPL-3.0-only
// Adopted from jekkle/cartur-build-camera f81977b2553d48a2a79eea62969f86bb9d9fa32e / src/Compatibility/WardIsLove/WardAccess.cs.
// Changed by expDARE, 2026: Timber effective settings, native lifecycle and owned cleanup.
using System;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace Timber.Camera;

internal static class WardAccess
{
	private const string PluginGuid = "Azumatt.WardIsLove";
	private static readonly System.Version MinimumVersion = new System.Version(2, 3, 3);

	private delegate bool CheckAccessHandler(long playerId, Vector3 point, float radius, bool flash);

	private static readonly bool WardIsLoveLoaded = Chainloader.PluginInfos.TryGetValue(PluginGuid, out BepInEx.PluginInfo plugin) && plugin.Metadata.Version.CompareTo(MinimumVersion) >= 0;
	private static readonly CheckAccessHandler WardIsLoveCheck = WardIsLoveLoaded ? ResolveWardIsLoveCheck() : null;

	private static bool _warnedAboutMissingCheck;

	internal static bool Check(long playerId, Vector3 point)
	{
		if (WardIsLoveCheck != null)
        {
            try { return WardIsLoveCheck(playerId, point, 0f, false); }
            catch (Exception) { WarnFallback(); }
        }

		if (WardIsLoveLoaded) WarnFallback();

		return PrivateArea.CheckAccess(point, flash: false, wardCheck: true);
	}

	private static void WarnFallback()
    {
        if (_warnedAboutMissingCheck) return;
        _warnedAboutMissingCheck = true;
        ExtraPotions.Timber.Runtime.Plugin.Log?.Warning(PluginGuid + " camera pickup access API unavailable; using vanilla ward access.");
    }

	private static CheckAccessHandler ResolveWardIsLoveCheck()
	{
		try
		{
			Type type = Type.GetType("WardIsLove.Util.CustomCheck, WardIsLove", false);
			MethodInfo method = type?.GetMethod("CheckAccess", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(long), typeof(Vector3), typeof(float), typeof(bool) }, null);
			return method == null ? null : (CheckAccessHandler)Delegate.CreateDelegate(typeof(CheckAccessHandler), method);
		}
		catch
		{
			return null;
		}
	}
}
