using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>
/// Compares the server's and the player's family registries and words the result the way the player sees it.
/// Versions compare as dotted integers, prerelease suffixes ignored.
/// </summary>
internal static class FamilyCheck
{
	private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
	private static readonly Random Dice = new();

	/// <summary>One line per mismatch, empty when the two sides fit together.</summary>
	public static List<string> Compare(List<FamilyEntry> server, List<FamilyEntry> client)
	{
		List<string> lines = new();
		foreach (FamilyEntry mine in server)
		{
			FamilyEntry? theirs = client.FirstOrDefault(c => c.Guid == mine.Guid);
			if (theirs == null)
			{
				if (mine.Mandatory)
				{
					lines.Add(Outdated(mine, "no copy"));
				}
			}
			else if (Older(theirs.Version, mine.OldestAccepted) || Older(mine.Version, theirs.OldestAccepted))
			{
				lines.Add(Outdated(mine, theirs.Version));
			}
		}
		lines.AddRange(client.Where(c => c.Mandatory && server.All(s => s.Guid != c.Guid)).Select(NotRun));
		return lines;
	}

	/// <summary>True when one of the player's mandatory mods is not run by the server.</summary>
	public static bool ServerLacksMandatory(List<FamilyEntry> server, List<FamilyEntry> client)
	{
		return client.Any(c => c.Mandatory && server.All(s => s.Guid != c.Guid));
	}

	public static string Message(List<string> lines, string id)
	{
		return string.Join("\n", lines) + $"\nRefusal code {id}; the same code is in the server's log.";
	}

	/// <summary>Six characters from a-z0-9 that name one refusal in the server's and the player's log.</summary>
	public static string NewRefusalCode()
	{
		char[] id = new char[6];
		for (int i = 0; i < id.Length; i++)
		{
			id[i] = Alphabet[Dice.Next(Alphabet.Length)];
		}
		return new string(id);
	}

	/// <summary>True when version a is older than b, compared as dotted integers.</summary>
	public static bool Older(string a, string b)
	{
		int[] left = Parse(a);
		int[] right = Parse(b);
		for (int i = 0; i < Math.Max(left.Length, right.Length); i++)
		{
			int x = i < left.Length ? left[i] : 0;
			int y = i < right.Length ? right[i] : 0;
			if (x != y)
			{
				return x < y;
			}
		}
		return false;
	}

	private static int[] Parse(string version)
	{
		int cut = version.IndexOfAny(new[] { '-', '+' });
		string core = cut >= 0 ? version.Substring(0, cut) : version;
		return core.Split('.').Select(part => int.TryParse(part.Trim(), out int number) ? number : 0).ToArray();
	}

	private static string Outdated(FamilyEntry server, string have)
	{
		return $"This server runs {server.Title} {server.Version}; you have {have}. Update {server.Title}, or ask the host to.";
	}

	private static string NotRun(FamilyEntry client)
	{
		return $"This server does not run {client.Title}; remove it or ask the host to install it.";
	}
}
