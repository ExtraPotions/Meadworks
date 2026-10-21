using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>
/// The <c>charter</c> console command, registered once per process by the lead copy: status, diff, versions
/// and trace. Each copy answers through the delegates it put into the family registry.
/// </summary>
internal static class ConsoleCommands
{
	private const string Usage = "charter status | charter diff [title] | charter versions | charter trace on|off";

	private static bool registered;

	public static void Register()
	{
		if (registered)
		{
			return;
		}
		registered = true;
		new Terminal.ConsoleCommand("charter", "Charter: " + Usage, (Terminal.ConsoleEvent)Run);
	}

	private static void Run(Terminal.ConsoleEventArgs args)
	{
		string verb = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
		string rest = args.Length > 2 ? string.Join(" ", args.Args.Skip(2)) : "";
		IEnumerable<string> lines = verb switch
		{
			"status" => Family.Entries().Select(Status),
			"diff" => Family.Entries().Where(e => Matches(e, rest)).Select(Diff),
			"versions" => Versions(),
			"trace" => Trace(rest),
			_ => new[] { Usage },
		};
		foreach (string line in lines.SelectMany(l => l.Split('\n')))
		{
			args.Context.AddString(line);
			GameHooks.LeadJournal?.Info(line);
		}
	}

	private static bool Matches(FamilyEntry entry, string title)
	{
		return title.Length == 0 || entry.Title.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static string Status(FamilyEntry entry) => Family.Status(entry.Guid)?.Invoke() ?? $"{entry.Title} {entry.Version}: no status";

	private static string Diff(FamilyEntry entry) => Family.Diff(entry.Guid)?.Invoke() ?? $"{entry.Title}: no diff";

	private static IEnumerable<string> Versions()
	{
		yield return "This side:";
		foreach (FamilyEntry entry in Family.Entries())
		{
			yield return "  " + entry.Describe();
		}
		yield return FamilyRpc.ServerRegistry == null ? "Server: not received (not connected, or the server runs no Charter mod)" : "Server, as received at join:";
		foreach (FamilyEntry entry in FamilyRpc.ServerRegistry ?? new List<FamilyEntry>())
		{
			yield return "  " + entry.Describe();
		}
	}

	private static IEnumerable<string> Trace(string mode)
	{
		if (mode != "on" && mode != "off")
		{
			return new[] { Usage };
		}
		int level = (int)(mode == "on" ? Verbosity.Trace : Verbosity.Normal);
		foreach (FamilyEntry entry in Family.Entries())
		{
			Family.Verbosity(entry.Guid)?.Invoke(level);
		}
		return new[] { $"Charter verbosity is now {(Verbosity)level} for every mod" };
	}
}
