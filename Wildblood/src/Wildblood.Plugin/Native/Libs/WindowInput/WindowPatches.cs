// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, ValheimModLibs, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Wildblood.AdoptedLibs.WindowInput;

/// <summary>
/// The game's own checks for its windows, answered for the mod's windows too: <c>Player.TakeInput</c> (attacks,
/// blocks, hotbar and other player keys), <c>PlayerController.InInventoryEtc</c> (the camera does not follow the
/// mouse), <c>GameCamera.UpdateMouseCapture</c> (the cursor is shown and free unless the game menu is up),
/// <c>ZInput.GetMouseScrollWheel</c> (no zoom; UI scroll views read the wheel themselves) and <c>Menu.Update</c> (Esc
/// closes the window instead of opening the menu). Installed once per merged copy; private members are reached by name.
/// </summary>
internal static class WindowPatches
{
	private static bool patched;

	public static void Install(Harmony harmony)
	{
		if (patched)
		{
			return;
		}
		patched = true;
		Postfix(harmony, AccessTools.Method(typeof(Player), "TakeInput"), nameof(NoPlayerInput));
		Postfix(harmony, AccessTools.Method(typeof(PlayerController), "InInventoryEtc"), nameof(InWindow));
		Postfix(harmony, AccessTools.Method(typeof(GameCamera), "UpdateMouseCapture"), nameof(FreeCursor));
		Postfix(harmony, AccessTools.Method(typeof(ZInput), "GetMouseScrollWheel"), nameof(NoZoom));
		harmony.Patch(AccessTools.Method(typeof(Menu), "Update"), prefix: new HarmonyMethod(typeof(WindowPatches), nameof(Escape)));
	}

	private static void Postfix(Harmony harmony, System.Reflection.MethodInfo target, string patch) =>
		harmony.Patch(target, postfix: new HarmonyMethod(typeof(WindowPatches), patch));

	private static void NoPlayerInput(ref bool __result)
	{
		if (__result && GameWindow.AnyOpen)
		{
			__result = false;
		}
	}

	private static void InWindow(ref bool __result)
	{
		if (!__result && GameWindow.AnyOpen)
		{
			__result = true;
		}
	}

	private static void FreeCursor()
	{
		if (GameWindow.AnyOpen && !Menu.IsVisible())
		{
			ZCursor.LockState = CursorLockMode.None;
			ZCursor.Show();
		}
	}

	private static void NoZoom(ref float __result)
	{
		if (__result != 0f && GameWindow.AnyOpen)
		{
			__result = 0f;
		}
	}

	// While the menu is hidden, Esc would open it; with a window open it closes the window instead, this frame only.
	private static bool Escape(Menu __instance)
	{
		if (__instance.m_root == null || __instance.m_root.gameObject.activeSelf || !ZInput.GetKeyDown(KeyCode.Escape))
		{
			return true;
		}
		return !GameWindow.CloseOpen();
	}
}
