// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, ValheimModLibs, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ExtraPotions.Wildblood.AdoptedLibs.WindowInput;

/// <summary>
/// A mod's own windows, treated like the game's own while one is open (<see cref="WindowPatches"/>): the cursor is
/// shown and free, the player does not attack, block, use hotkeys or turn the camera with the mouse, the mouse wheel
/// does not zoom, and Esc closes the window instead of opening the game menu. Walking stays possible, as with the
/// inventory. <see cref="Install"/> once per mod (the patches are installed once per merged copy of this library),
/// then <see cref="Add"/> each window with how to tell it is open and how to close it.
/// </summary>
public static class GameWindow
{
	private sealed class Entry
	{
		public Func<bool> IsOpen = null!;
		public Action Close = null!;
	}

	private static readonly List<Entry> windows = new();

	public static void Install(Harmony harmony) => WindowPatches.Install(harmony);

	public static void Add(Func<bool> isOpen, Action close) => windows.Add(new Entry { IsOpen = isOpen, Close = close });

	/// <summary>One of this mod's windows is open. A window whose check throws counts as closed.</summary>
	public static bool AnyOpen
	{
		get
		{
			foreach (Entry window in windows)
			{
				if (Open(window))
				{
					return true;
				}
			}
			return false;
		}
	}

	/// <summary>Closes every open window of this mod (Esc). False when none was open.</summary>
	public static bool CloseOpen()
	{
		bool closed = false;
		foreach (Entry window in windows)
		{
			if (Open(window))
			{
				window.Close();
				closed = true;
			}
		}
		return closed;
	}

	private static bool Open(Entry window)
	{
		try
		{
			return window.IsOpen();
		}
		catch (Exception)
		{
			return false;
		}
	}
}
