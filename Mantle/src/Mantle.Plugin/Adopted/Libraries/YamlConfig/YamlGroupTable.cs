using System;
using System.Collections.Generic;

namespace ExtraPotions.Mantle.Embedded.YamlConfig;

/// <summary>
/// Named groups of names, where a member may itself be a group. Answers "which groups does this name belong to"
/// (<see cref="Resolve"/>) and "which names does this group contain" (<see cref="Expand"/>), both case-insensitive
/// and safe against cycles.
/// </summary>
public sealed class YamlGroupTable
{
	private readonly Dictionary<string, List<string>> members = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, List<string>> parents = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The group names in the order they were added.</summary>
	public IReadOnlyCollection<string> Names => members.Keys;

	/// <summary>True when the name is a group.</summary>
	public bool IsGroup(string name) => members.ContainsKey(name);

	/// <summary>Adds a group; adding the same group twice extends it.</summary>
	public void Add(string group, IEnumerable<string> names)
	{
		if (!members.TryGetValue(group, out List<string> list))
		{
			list = new List<string>();
			members.Add(group, list);
		}
		foreach (string name in names)
		{
			list.Add(name);
			if (!parents.TryGetValue(name, out List<string> owners))
			{
				owners = new List<string>();
				parents.Add(name, owners);
			}
			owners.Add(group);
		}
	}

	/// <summary>The groups a name belongs to, directly first, then the groups containing those, and so on.</summary>
	public IReadOnlyList<string> Resolve(string name)
	{
		List<string> result = new();
		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
		Queue<string> pending = new();
		pending.Enqueue(name);
		while (pending.Count > 0)
		{
			if (!parents.TryGetValue(pending.Dequeue(), out List<string> owners))
			{
				continue;
			}
			foreach (string owner in owners)
			{
				if (seen.Add(owner))
				{
					result.Add(owner);
					pending.Enqueue(owner);
				}
			}
		}
		return result;
	}

	/// <summary>Every name in a group, following nested groups; a name that is not a group expands to itself.</summary>
	public IReadOnlyList<string> Expand(string group)
	{
		List<string> result = new();
		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
		Expand(group, seen, result);
		return result;
	}

	private void Expand(string name, HashSet<string> seen, List<string> result)
	{
		if (!seen.Add(name))
		{
			return;
		}
		if (!members.TryGetValue(name, out List<string> list))
		{
			result.Add(name);
			return;
		}
		foreach (string member in list)
		{
			Expand(member, seen, result);
		}
	}
}
