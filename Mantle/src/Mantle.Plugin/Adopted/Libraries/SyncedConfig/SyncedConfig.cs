using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ExtraPotions.Mantle.Embedded.Charter;
using ExtraPotions.Mantle.Embedded.YamlConfig;

namespace ExtraPotions.Mantle.Embedded.SyncedConfig;

/// <summary>
/// Everything a mod's configuration needs in one place, built on Charter:
/// <list type="bullet">
/// <item>Bind entries that take the server's value while it binds (default) or stay local.</item>
/// <item>A locking entry, the charter's binding: while it is on, players cannot amend the synced entries.</item>
/// <item>The .cfg is written at startup and hot reloaded when edited.</item>
/// <item>YAML files with the same sync, reload and an in-game editor.</item>
/// <item>A version check between server and clients.</item>
/// </list>
/// Create it in Awake, bind your entries, register YAML sources, then call <see cref="Finish"/>.
/// </summary>
public sealed class SyncedConfiguration
{
	public ExtraPotions.Mantle.Embedded.Charter.Charter Sync { get; }
	public ConfigFile Config { get; }
	public ManualLogSource Log { get; }
	public YamlFileHub Yaml { get; }
	public IReadOnlyList<string> SearchPaths { get; }
	public IDisposable? ConfigWatcher { get; private set; }

	/// <summary>The server binds this configuration and this player may not amend it.</summary>
	public bool IsLocked => Sync.IsBound && !Sync.MayAmend;

	/// <summary>This player is on the server's admin list, or is the host / single player.</summary>
	public bool IsAdmin => Sync.IsSteward;

	/// <summary>This side's values are authoritative right now: the server, or a player under no binding.</summary>
	public bool IsAuthor => Sync.IsAuthor;

	/// <param name="plugin">Your plugin. Its Config, GUID and folder are used.</param>
	/// <param name="log">Your plugin's Logger (it is protected, so it has to be passed in).</param>
	/// <param name="title">The mod's name as players see it in messages and refusals.</param>
	/// <param name="version">Current version, compared between server and clients.</param>
	/// <param name="oldestAccepted">The oldest version of the mod the server still lets in; defaults to <paramref name="version"/>.</param>
	/// <param name="mandatory">Both sides must have the mod. False: a side without it is fine and the mod stays local there.</param>
	public SyncedConfiguration(BaseUnityPlugin plugin, ManualLogSource log, string title, string version, string? oldestAccepted = null, bool mandatory = true)
	{
		Config = plugin.Config;
		Log = log;
		string guid = plugin.Info.Metadata.GUID;
		Sync = new ExtraPotions.Mantle.Embedded.Charter.Charter(guid, title, version, oldestAccepted, mandatory);
		SearchPaths = new[]
		{
			Path.GetDirectoryName(Config.ConfigFilePath) ?? Paths.ConfigPath,
			Path.GetDirectoryName(plugin.GetType().Assembly.Location) ?? Paths.PluginPath,
		};
		Yaml = new YamlFileHub(title, Log, SearchPaths, Sync);
	}

	/// <summary>Binds an entry. Synced entries take the server's value while connected.</summary>
	public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description, bool synced = true, AcceptableValueBase? acceptableValues = null, params object[] tags)
	{
		return Bind(section, key, defaultValue, new ConfigDescription(description, acceptableValues, tags), synced);
	}

	public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, ConfigDescription description, bool synced = true)
	{
		ConfigEntry<T> entry = Config.Bind(section, key, defaultValue, description);
		Sync.Clause(entry, local: !synced);
		return entry;
	}

	/// <summary>Registers an already bound entry as a clause (for entries created elsewhere).</summary>
	public Clause<T> Add<T>(ConfigEntry<T> entry, bool synced = true) => Sync.Clause(entry, local: !synced);

	/// <summary>Binds the master switch: while it is true on the server, players take the server's values and cannot amend them.</summary>
	public ConfigEntry<bool> BindLocking(string section, string key, bool defaultValue, string description)
	{
		ConfigEntry<bool> entry = Config.Bind(section, key, defaultValue, description);
		Sync.Binding(entry);
		return entry;
	}

	/// <summary>Registers a YAML file set. Load and apply happen through the hooks installed by <see cref="Finish"/>.</summary>
	public YamlFileSet AddYaml(YamlFileSet set) => Yaml.Register(set);

	/// <summary>
	/// Call at the end of Awake: installs the Charter patches, writes the .cfg, starts hot reloading it, installs
	/// the YAML load/apply hooks.
	/// </summary>
	public void Finish(Harmony harmony, int yamlApplyPriority = Priority.Normal)
	{
		ExtraPotions.Mantle.Embedded.Charter.Charter.Install(harmony);
		ConfigWatcher = ExtraPotions.Mantle.Embedded.ConfigReload.ConfigReloader.Setup(Config, Log, true, Sync.HoldsServerValue, Sync.ReloadKeepingServerValues);
		if (Yaml.Sets.Count > 0)
		{
			Yaml.HookGame(harmony, yamlApplyPriority);
		}
	}

	/// <summary>Reads an embedded resource of the plugin assembly, convenient for YAML example files.</summary>
	public static Func<byte[]?> EmbeddedResource(Assembly assembly, string resourceName)
	{
		return () =>
		{
			using Stream? stream = assembly.GetManifestResourceStream(resourceName);
			if (stream is null)
			{
				return null;
			}
			using MemoryStream memory = new();
			stream.CopyTo(memory);
			return memory.ToArray();
		};
	}
}
