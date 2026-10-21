using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ExtraPotions.Mantle.Embedded.YamlConfig;

/// <summary>
/// The moments in the game's lifecycle when a <see cref="YamlFileHub"/> loads files and applies models. Private
/// game members are reached through reflection so consumers need not publicize the game assembly for this
/// library's sake. The patches are installed once per assembly and serve every hub that was registered.
/// </summary>
internal static class YamlGameHooks
{
	private static readonly List<YamlFileHub> hooked = new();
	private static readonly FieldInfo? firstSpawnField = AccessTools.Field(typeof(Game), "m_firstSpawn");
	private static bool patched;

	/// <summary>Registers the hub and installs the patches on the first call; the first caller's priority is used.</summary>
	public static void Install(YamlFileHub hub, Harmony harmony, int applyPriority)
	{
		if (!hooked.Contains(hub))
		{
			hooked.Add(hub);
		}
		if (patched)
		{
			return;
		}
		patched = true;
		harmony.Patch(AccessTools.Method(typeof(FejdStartup), "Start"), postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.AfterMenuStart)));
		harmony.Patch(AccessTools.Method(typeof(ZNet), "Awake"), postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.AfterNetworkStart)));
		HarmonyMethod apply = new(typeof(Patches), nameof(Patches.AfterRespawnRequest)) { priority = applyPriority };
		harmony.Patch(AccessTools.Method(typeof(Game), "RequestRespawn"), postfix: apply);
	}

	private static void LoadHooked()
	{
		foreach (YamlFileHub hub in hooked)
		{
			hub.LoadFromHook();
		}
	}

	private static void ApplyHooked()
	{
		foreach (YamlFileHub hub in hooked)
		{
			hub.ApplyFromHook();
		}
	}

	private static class Patches
	{
		public static void AfterMenuStart() => LoadHooked();

		public static void AfterNetworkStart(ZNet __instance)
		{
			if (__instance.IsServer() && __instance.IsDedicated())
			{
				LoadHooked();
			}
		}

		public static void AfterRespawnRequest(Game __instance)
		{
			if (firstSpawnField?.GetValue(__instance) is true)
			{
				ApplyHooked();
			}
		}
	}
}
