using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace ExtraPotions.Burrow.Embedded.PatchGuard;

/// <summary>
/// Makes exceptions thrown by a mod's own code show up under the mod's name in BepInEx/LogOutput.log.
/// <para>
/// Unity only logs the game method that was running, and with Harmony that is a dynamic method whose name says
/// nothing about whose code failed. <see cref="Install"/> records the mod's logger and assembly. <see cref="Run(string, Action)"/>
/// and <see cref="Wrap(string, Action)"/> run an entry point and, when it throws, hand the exception to
/// <see cref="Report"/>, which walks the stack of the exception and its inner exceptions (frames first, the text of
/// the trace as a fallback) and logs it through the mod's logger with the given context when the mod's assembly or
/// one of its root namespaces is on it. Run and Wrap then rethrow unchanged, so behaviour stays exactly as before;
/// exceptions thrown elsewhere are never logged here. An exception is logged once, however many guards it crosses.
/// </para>
/// <para>
/// Wrap the mod's entry points (Update, RPC handlers, SettingChanged callbacks, coroutines) with Run or Wrap. Inside
/// a Harmony patch, wrap the body the same way when the method name in Unity's log is not enough. <see cref="Profiler"/>
/// is the companion for finding out which patched method eats frame time.
/// </para>
/// </summary>
public static class Guard
{
	private const string Marker = "PatchGuard.Logged";

	private static ManualLogSource? log;
	private static Assembly? modAssembly;
	private static string[] namespaces = Array.Empty<string>();

	/// <summary>
	/// Records the mod's logger and assembly so <see cref="Run(string, Action)"/>, <see cref="Wrap(string, Action)"/>
	/// and <see cref="Report"/> can attribute exceptions. It does not patch anything: an extra Harmony patch on every
	/// method the mod touches cost about 2 ms per call on some setups (seen with a large mod list), which made object
	/// creation and per-frame updates crawl. Exceptions inside patches are logged by Unity with the game method name;
	/// the Run and Wrap helpers cover the mod's own entry points.
	/// </summary>
	/// <returns>Always 0, kept for callers that log the count.</returns>
	public static int Install(Harmony harmony, ManualLogSource logger, Assembly assembly)
	{
		log = logger;
		modAssembly = assembly;
		namespaces = RootNamespaces(assembly);
		return 0;
	}

	/// <summary>Runs <paramref name="action"/>; an exception from the mod's code is logged with <paramref name="context"/> and rethrown.</summary>
	public static void Run(string context, Action action)
	{
		try
		{
			action();
		}
		catch (Exception e)
		{
			Report(e, context);
			throw;
		}
	}

	/// <summary>Same as <see cref="Run(string, Action)"/> for code that returns a value.</summary>
	public static T Run<T>(string context, Func<T> func)
	{
		try
		{
			return func();
		}
		catch (Exception e)
		{
			Report(e, context);
			throw;
		}
	}

	/// <summary>Wraps a callback so it is guarded when invoked, for event handlers and delegates handed to the game.</summary>
	public static Action Wrap(string context, Action action) => () => Run(context, action);

	public static EventHandler Wrap(string context, EventHandler handler) => (sender, args) => Run(context, () => handler(sender, args));

	/// <summary>Logs <paramref name="exception"/> under the mod's name if the mod's code is on its stack. Does not throw.</summary>
	public static void Report(Exception exception, string context)
	{
		if (log is null || exception.Data.Contains(Marker) || !IsFromMod(exception))
		{
			return;
		}
		try
		{
			exception.Data[Marker] = true;
		}
		catch (Exception)
		{
			// some exception types expose a read-only Data dictionary; logging twice is the worst case
		}
		log.LogError($"Exception in {context} (thrown by this mod's code):\n{exception}");
	}

	/// <summary>Whether the method carries a prefix, postfix, transpiler or finalizer declared in the assembly.</summary>
	internal static bool HasPatchFrom(MethodBase method, Assembly assembly)
	{
		Patches? info = Harmony.GetPatchInfo(method);
		if (info is null)
		{
			return false;
		}
		return info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers)
			.Any(p => p.PatchMethod.DeclaringType?.Assembly == assembly);
	}

	private static bool IsFromMod(Exception exception)
	{
		for (Exception? e = exception; e is not null; e = e.InnerException)
		{
			StackFrame[]? frames = null;
			try
			{
				frames = new StackTrace(e, false).GetFrames();
			}
			catch (Exception)
			{
				// fall through to the text check
			}
			if (frames is not null && frames.Any(f => f.GetMethod()?.DeclaringType?.Assembly == modAssembly))
			{
				return true;
			}
			string? text = e.StackTrace;
			if (text is not null && namespaces.Any(ns => text.Contains(" " + ns + ".") || text.Contains("<" + ns + ".")))
			{
				return true;
			}
		}
		return false;
	}

	private static string[] RootNamespaces(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes()
				.Select(t => t.Namespace)
				.Where(ns => !string.IsNullOrEmpty(ns))
				.Select(ns => ns!.Split('.')[0])
				.Distinct()
				.ToArray();
		}
		catch (ReflectionTypeLoadException e)
		{
			return e.Types.Where(t => t?.Namespace is not null).Select(t => t!.Namespace!.Split('.')[0]).Distinct().ToArray();
		}
	}

	internal static string Describe(MethodBase method) => $"{method.DeclaringType?.Name}.{method.Name}";
}
