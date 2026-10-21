// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, ValheimModLibs, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using HarmonyLib;

namespace ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

/// <summary>
/// Times every method the mod has patched and logs the most expensive ones at a fixed interval, for finding out
/// which patch (or which game method the mod hooks into) is eating frame time. The time is inclusive: it covers
/// the game's own method body plus every prefix and postfix on it, so it names the hot method, not the exact line.
/// Meant for diagnosis behind a config switch, not for release play.
/// </summary>
public static class Profiler
{
	private sealed class Sample
	{
		public long Ticks;
		public long Calls;
	}

	private static ManualLogSource? log;
	private static readonly HashSet<MethodBase> profiled = new();
	private static readonly HashSet<MethodBase> ownPatches = new();
	private static readonly ConcurrentDictionary<MethodBase, Sample> samples = new();
	private static int intervalSeconds;

	/// <summary>
	/// Wraps every method that has a patch from <paramref name="assembly"/> with a timer. Call after all patching is
	/// done. Dispose the result to stop the periodic report (the timing prefixes stay, they are cheap).
	/// </summary>
	public static IDisposable Install(Harmony harmony, ManualLogSource logger, Assembly assembly, int reportEverySeconds = 5, int top = 12)
	{
		log = logger;
		intervalSeconds = reportEverySeconds;
		HarmonyMethod prefix = new(typeof(Profiler), nameof(Prefix)) { priority = Priority.First };
		HarmonyMethod postfix = new(typeof(Profiler), nameof(Postfix)) { priority = Priority.Last };
		int count = TimeGameMethods(harmony, assembly, prefix, postfix);
		int patchCount = TimeOwnPatches(harmony, assembly, prefix, postfix);
		logger.LogInfo($"ExtraPotions.Wildblood.AdoptedLibs.PatchGuard profiler: timing {count} patched game methods and {patchCount} of the mod's own patch methods, report every {reportEverySeconds} s");
		return new System.Threading.Timer(_ => Report(top), null, reportEverySeconds * 1000, reportEverySeconds * 1000);
	}

	/// <summary>Adds the timing pair to every game method that carries a patch from the assembly; returns how many.</summary>
	private static int TimeGameMethods(Harmony harmony, Assembly assembly, HarmonyMethod prefix, HarmonyMethod postfix)
	{
		int count = 0;
		foreach (MethodBase method in Harmony.GetAllPatchedMethods().ToList())
		{
			if (profiled.Contains(method) || !Guard.HasPatchFrom(method, assembly))
			{
				continue;
			}
			if (TryTime(harmony, method, prefix, postfix, ""))
			{
				profiled.Add(method);
				count++;
			}
		}
		return count;
	}

	// The mod's own patch methods get timed as well, so the report separates "the game method is slow" from
	// "our prefix or postfix on it is slow".
	private static int TimeOwnPatches(Harmony harmony, Assembly assembly, HarmonyMethod prefix, HarmonyMethod postfix)
	{
		int count = 0;
		foreach (MethodInfo patchMethod in OwnPatchMethods(assembly))
		{
			if (ownPatches.Add(patchMethod) && TryTime(harmony, patchMethod, prefix, postfix, "patch "))
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>The assembly's own prefixes, postfixes and finalizers on the methods being timed.</summary>
	private static IEnumerable<MethodInfo> OwnPatchMethods(Assembly assembly)
	{
		foreach (MethodBase original in profiled.ToList())
		{
			Patches? info = Harmony.GetPatchInfo(original);
			if (info is null)
			{
				continue;
			}
			foreach (Patch patch in info.Prefixes.Concat(info.Postfixes).Concat(info.Finalizers))
			{
				MethodInfo patchMethod = patch.PatchMethod;
				if (patchMethod.DeclaringType?.Assembly == assembly && patchMethod.DeclaringType != typeof(Profiler))
				{
					yield return patchMethod;
				}
			}
		}
	}

	private static bool TryTime(Harmony harmony, MethodBase method, HarmonyMethod prefix, HarmonyMethod postfix, string kind)
	{
		try
		{
			harmony.Patch(method, prefix: prefix, postfix: postfix);
			return true;
		}
		catch (Exception e)
		{
			log?.LogWarning($"ExtraPotions.Wildblood.AdoptedLibs.PatchGuard profiler: could not time {kind}{Guard.Describe(method)}: {e.Message}");
			return false;
		}
	}

	// Start times travel on a per-thread stack instead of Harmony's __state, which is not reliably handed from a
	// prefix to a postfix when other patch classes on the same method declare their own state.
	[ThreadStatic] private static Stack<long>? starts;

	private static void Prefix()
	{
		starts ??= new Stack<long>();
		starts.Push(Stopwatch.GetTimestamp());
	}

	private static void Postfix(MethodBase __originalMethod)
	{
		if (starts is null || starts.Count == 0)
		{
			return;
		}
		long elapsed = Stopwatch.GetTimestamp() - starts.Pop();
		Sample sample = samples.GetOrAdd(__originalMethod, _ => new Sample());
		Interlocked.Add(ref sample.Ticks, elapsed);
		Interlocked.Increment(ref sample.Calls);
	}

	private static void Report(int top)
	{
		try
		{
			KeyValuePair<MethodBase, Sample>[] snapshot = samples.ToArray();
			samples.Clear();
			if (snapshot.Length == 0)
			{
				return;
			}
			double toMs = 1000.0 / Stopwatch.Frequency;
			double total = snapshot.Sum(kv => kv.Value.Ticks) * toMs;
			IEnumerable<string> lines = snapshot
				.OrderByDescending(kv => kv.Value.Ticks)
				.Take(top)
				.Select(kv => $"{kv.Value.Ticks * toMs,8:0.0} ms  {(ownPatches.Contains(kv.Key) ? "[mod patch] " : "")}{Guard.Describe(kv.Key)}  x{kv.Value.Calls}");
			log?.LogInfo($"ExtraPotions.Wildblood.AdoptedLibs.PatchGuard profile: {total:0} ms inside patched methods in the last {intervalSeconds} s ({total / (intervalSeconds * 10.0):0.0}% of wall time)\n  " + string.Join("\n  ", lines));
		}
		catch (Exception e)
		{
			log?.LogWarning($"ExtraPotions.Wildblood.AdoptedLibs.PatchGuard profiler report failed: {e.Message}");
		}
	}
}
