using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using ExtraPotions.Burrow.Embedded.Charter;

namespace ExtraPotions.Burrow.Embedded.YamlConfig;

/// <summary>
/// The per-mod service for YAML files: finds the files of every registered <see cref="YamlFileSet"/>, parses
/// them, publishes them through Charter so players receive the server's files, writes edits back to disk,
/// reloads edited files every five seconds and applies the parsed models to the mod.
/// <para>
/// Files travel as one Charter article per set (a list alternating path and content). Every side, server or
/// player, reacts to that article changing, so loading, reloading, editing and receiving all end in the same
/// place: build a model, keep the previous one on errors, otherwise store, apply and (on the author)
/// write back. Disk access lives in <see cref="YamlFileStore"/>, the reload poll in <see cref="YamlFileWatcher"/>
/// and the game patches in <see cref="YamlGameHooks"/>.
/// </para>
/// </summary>
public sealed class YamlFileHub
{
	private readonly string modName;
	private readonly ManualLogSource log;
	private readonly ExtraPotions.Burrow.Embedded.Charter.Charter? charter;
	private readonly List<YamlFileSet> sets = new();
	private readonly YamlFileStore store;
	private readonly YamlFileWatcher watcher;
	private bool loaded;

	/// <param name="modName">Prefix of every log line.</param>
	/// <param name="log">The mod's logger.</param>
	/// <param name="searchFolders">Where files are looked for, in order; defaults are written to the first.</param>
	/// <param name="charter">The mod's Charter, or null for files that are never pushed.</param>
	public YamlFileHub(string modName, ManualLogSource log, IEnumerable<string> searchFolders, ExtraPotions.Burrow.Embedded.Charter.Charter? charter)
	{
		this.modName = modName;
		this.log = log;
		this.charter = charter;
		store = new YamlFileStore(modName, log, searchFolders);
		watcher = new YamlFileWatcher(store, sets, ReloadFromDisk, log, modName);
	}

	/// <summary>The registered sets in registration order.</summary>
	public IReadOnlyList<YamlFileSet> Sets => sets;

	/// <summary>True when this side's files are authoritative: a server, or a player under no binding.</summary>
	public bool IsAuthor => charter?.IsAuthor ?? true;

	/// <summary>True when the server binds the configuration and this player may not amend it.</summary>
	public bool IsLocked => charter != null && charter.IsBound && !charter.MayAmend;

	/// <summary>While true, applying a received value does not write the files to disk.</summary>
	public bool SuppressWriteBack { get; set; }

	/// <summary>Whether models may be applied right now; set it to "the game databases exist".</summary>
	public Func<bool> CanApply { get; set; } = () => true;

	/// <summary>Raised after a set's model was handed to the mod.</summary>
	public event Action<YamlFileSet>? Applied;

	/// <summary>Registers a set. Call before the game loads, so the article exists when players connect.</summary>
	public YamlFileSet Register(YamlFileSet set)
	{
		if (sets.Any(s => string.Equals(s.SyncKey, set.SyncKey, StringComparison.OrdinalIgnoreCase)))
		{
			throw new ArgumentException($"{modName}: a YAML file set with sync key '{set.SyncKey}' is already registered");
		}
		sets.Add(set);
		if (charter is not null)
		{
			Article<List<string>> channel = new(charter, set.SyncKey, new List<string>());
			set.Channel = channel;
			channel.Changed += () => Receive(set, YamlFileList.Unflatten(channel.Value));
		}
		return set;
	}

	/// <summary>
	/// Finds, parses and publishes the files of every set, writing default content where no file exists, then
	/// starts watching the files for edits. Done once by <see cref="HookGame"/>; call it yourself otherwise.
	/// </summary>
	public void LoadAll()
	{
		loaded = true;
		foreach (YamlFileSet set in sets)
		{
			LoadSet(set);
		}
		watcher.Start();
	}

	/// <summary>Hands every current model to the mod again, e.g. once the game databases exist.</summary>
	public void ApplyAll()
	{
		if (!CanApply())
		{
			log.LogDebug($"{modName}: YAML apply skipped, the game is not ready");
			return;
		}
		foreach (YamlFileSet set in sets)
		{
			Apply(set);
		}
	}

	/// <summary>
	/// Builds a model from the given files and logs its warnings, and on failure its errors. The model is always
	/// returned; it is only usable when the result is true.
	/// </summary>
	/// <param name="context">Where the files came from, for the log: startup, reload, editor, received.</param>
	public bool TryBuild(YamlFileSet set, IReadOnlyDictionary<string, string> files, out YamlModel model, string context)
	{
		model = set.CreateModel();
		model.LoadAll(files);
		foreach (string warning in model.Warnings)
		{
			log.LogWarning($"{modName}: {warning}");
		}
		if (model.Errors.Count == 0)
		{
			return true;
		}
		log.LogError($"{modName}: {set.MainFileName} ({context}) has {model.Errors.Count} error(s), the previous configuration stays:");
		foreach (string error in model.Errors)
		{
			log.LogError($"{modName}: {error}");
		}
		return false;
	}

	/// <summary>
	/// Replaces a set's files with new contents (the editor's save, a reload): validates them, then publishes them
	/// so every side applies them. With <paramref name="saveToDisk"/> the author also writes them back.
	/// </summary>
	public void Replace(YamlFileSet set, IReadOnlyDictionary<string, string> files, bool saveToDisk)
	{
		if (TryBuild(set, files, out _, "editor"))
		{
			Publish(set, files, saveToDisk);
		}
	}

	/// <summary>
	/// Loads the files once when the main menu starts or a dedicated server's network starts, and applies the
	/// models after the local player's first spawn in a world, with the given Harmony priority so mods that read
	/// the game databases apply late enough. The patches are installed once per assembly and serve every hub that
	/// called this; the first caller's priority is used.
	/// </summary>
	public void HookGame(Harmony harmony, int applyPriority) => YamlGameHooks.Install(this, harmony, applyPriority);

	internal void LoadFromHook()
	{
		if (loaded)
		{
			return;
		}
		try
		{
			LoadAll();
		}
		catch (Exception e)
		{
			log.LogError($"{modName}: loading YAML files failed: {e}");
		}
	}

	internal void ApplyFromHook()
	{
		try
		{
			ApplyAll();
		}
		catch (Exception e)
		{
			log.LogError($"{modName}: applying YAML files failed: {e}");
		}
	}

	private void LoadSet(YamlFileSet set)
	{
		Dictionary<string, string> files = store.ReadOrCreate(set);
		watcher.TakeSnapshot(set);
		if (files.Count == 0)
		{
			if (set.Enabled())
			{
				log.LogWarning($"{modName}: no {set.FilePattern} found in {string.Join(" or ", store.SearchFolders)}");
			}
			return;
		}
		if (TryBuild(set, files, out _, "startup"))
		{
			Publish(set, files, writeBack: false);
		}
	}

	// Main thread, from the watcher: parse and publish the changed files without writing them back.
	private void ReloadFromDisk(YamlFileSet set)
	{
		Dictionary<string, string> files = store.Read(store.Discover(set));
		watcher.TakeSnapshot(set);
		if (files.Count == 0)
		{
			log.LogWarning($"{modName}: every {set.FilePattern} file is gone, keeping the loaded configuration");
		}
		else if (TryBuild(set, files, out _, "reload"))
		{
			Publish(set, files, writeBack: false);
			log.LogInfo($"{modName}: {set.MainFileName} reloaded" + (IsAuthor ? "" : ", not applied, remote configuration active"));
		}
	}

	/// <summary>Assigns the files to the set's article (or applies them directly when there is no charter).</summary>
	private void Publish(YamlFileSet set, IReadOnlyDictionary<string, string> files, bool writeBack)
	{
		bool previous = SuppressWriteBack;
		SuppressWriteBack = previous || !writeBack;
		try
		{
			if (set.Channel is null)
			{
				Receive(set, files);
			}
			// Changed by expDARE, 2026-10-19: a bound player's article holds the server's files and Assign would only
			// warn and do nothing, so a reload while connected skips it; the reload log says the files were not applied.
			else if (IsAuthor)
			{
				set.Channel.Assign(YamlFileList.Flatten(files));
			}
		}
		finally
		{
			SuppressWriteBack = previous;
		}
	}

	/// <summary>The article changed: on players the server pushed files, on the server it assigned its own.</summary>
	private void Receive(YamlFileSet set, IReadOnlyDictionary<string, string> files)
	{
		if (!TryBuild(set, files, out YamlModel model, "received"))
		{
			foreach (KeyValuePair<string, string> file in files)
			{
				log.LogWarning($"{modName}: content of {file.Key}:\n{file.Value}");
			}
			return;
		}
		set.Files = files;
		set.Current = model;
		if (CanApply())
		{
			Apply(set);
		}
		if (IsAuthor && !SuppressWriteBack)
		{
			store.WriteAll(files);
			watcher.TakeSnapshot(set);
		}
	}

	private void Apply(YamlFileSet set)
	{
		if (set.Current is null || !set.Enabled())
		{
			return;
		}
		try
		{
			set.Apply(set.Current);
			Applied?.Invoke(set);
		}
		catch (Exception e)
		{
			log.LogError($"{modName}: applying {set.MainFileName} failed: {e}");
		}
	}
}
