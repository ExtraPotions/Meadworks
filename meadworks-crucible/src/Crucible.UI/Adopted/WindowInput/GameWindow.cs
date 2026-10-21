// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/WindowInput/GameWindow.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.WindowInput; subsequent behavior changes are recorded below.
// Modified by expDARE, 2026-10-18: HUD visibility can remain on during a layout editor without releasing gameplay input.
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ExtraPotions.Meadworks.Crucible.UI.WindowInput;

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
		public Func<bool> BlocksHud = null!;
	}

	private static readonly List<Entry> windows = new();

	public static void Install(Harmony harmony) => WindowPatches.Install(harmony);

	public static void Add(Func<bool> isOpen, Action close) => Add(isOpen, close, isOpen);

	public static void Add(Func<bool> isOpen, Action close, Func<bool> blocksHud) =>
		windows.Add(new Entry { IsOpen = isOpen, Close = close, BlocksHud = blocksHud });

	public static bool AnyHudBlocking
	{
		get
		{
			foreach (Entry window in windows)
				if (Open(window))
				{
					try { if (window.BlocksHud()) return true; }
					catch (Exception) { return true; }
				}
			return false;
		}
	}

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
