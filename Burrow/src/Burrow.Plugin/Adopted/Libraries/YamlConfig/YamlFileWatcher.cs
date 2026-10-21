using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BepInEx;
using BepInEx.Logging;

namespace ExtraPotions.Burrow.Embedded.YamlConfig;

/// <summary>
/// Reload from disk for a <see cref="YamlFileHub"/>. A timer thread compares the files' last-write times with a
/// snapshot every five seconds (polling, deliberately: event-based watching on Mono for Linux servers reported the
/// mod's own reads as changes) and hands a changed set to the main thread, where the hub parses and publishes it.
/// </summary>
internal sealed class YamlFileWatcher
{
	private const int PollMilliseconds = 5000;

	private readonly YamlFileStore store;
	private readonly IReadOnlyList<YamlFileSet> sets;
	private readonly Action<YamlFileSet> reload;
	private readonly ManualLogSource log;
	private readonly string modName;
	private System.Threading.Timer? timer;

	/// <param name="reload">Called on the main thread for a set whose files changed.</param>
	public YamlFileWatcher(YamlFileStore store, IReadOnlyList<YamlFileSet> sets, Action<YamlFileSet> reload, ManualLogSource log, string modName)
	{
		this.store = store;
		this.sets = sets;
		this.reload = reload;
		this.log = log;
		this.modName = modName;
	}

	/// <summary>Starts the timer; further calls do nothing.</summary>
	public void Start()
	{
		timer ??= new System.Threading.Timer(_ => Poll(), null, PollMilliseconds, PollMilliseconds);
	}

	/// <summary>Remembers the current write times of the set's files as the state the next poll compares with.</summary>
	public void TakeSnapshot(YamlFileSet set)
	{
		Dictionary<string, DateTime> now = store.WriteTimes(set);
		lock (set.Snapshot)
		{
			set.Snapshot.Clear();
			foreach (KeyValuePair<string, DateTime> pair in now)
			{
				set.Snapshot[pair.Key] = pair.Value;
			}
		}
	}

	// System.Threading.Timer thread: file system checks only, nothing touches the game here.
	private void Poll()
	{
		try
		{
			foreach (YamlFileSet set in sets.Where(HasChanged))
			{
				if (Interlocked.CompareExchange(ref set.ReloadPending, 1, 0) == 0)
				{
					ThreadingHelper.SynchronizingObject.BeginInvoke(new Action(() => Reload(set)), null);
				}
			}
		}
		catch (Exception e)
		{
			log.LogError($"{modName}: watching YAML files failed: {e.Message}");
		}
	}

	private bool HasChanged(YamlFileSet set)
	{
		Dictionary<string, DateTime> now = store.WriteTimes(set);
		lock (set.Snapshot)
		{
			return now.Count != set.Snapshot.Count
				|| now.Any(pair => !set.Snapshot.TryGetValue(pair.Key, out DateTime time) || time != pair.Value);
		}
	}

	// Main thread: let the hub parse and publish the changed files.
	private void Reload(YamlFileSet set)
	{
		try
		{
			reload(set);
		}
		catch (Exception e)
		{
			log.LogError($"{modName}: reloading {set.MainFileName} failed: {e}");
		}
		finally
		{
			Interlocked.Exchange(ref set.ReloadPending, 0);
		}
	}
}
