using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>The status and diff lines one charter contributes to the console command.</summary>
internal static class Reports
{
	public static string Status(Charter charter)
	{
		string push = charter.LastPush == null ? "none" : $"{charter.LastPush.Value:HH:mm:ss} ({charter.PushedBytes} bytes)";
		return $"{charter.Title} {charter.Version}: bound {YesNo(charter.IsBound)}, author {YesNo(charter.IsAuthor)}, steward {YesNo(charter.IsSteward)}, " +
			$"last push {push}, {charter.Ledger.Clauses.Count} clause(s), {charter.Ledger.ArticleCount} article(s)";
	}

	public static string Diff(Charter charter)
	{
		if (charter.IsAuthor)
		{
			return $"{charter.Title}: you are the author";
		}
		List<string> lines = charter.Ledger.Pushable
			.Where(c => c.AuthorToml != c.OwnToml)
			.Select(c => $"  {c.Section}.{c.Key}: yours = {c.OwnToml}, server = {c.AuthorToml}")
			.ToList();
		return lines.Count == 0 ? $"{charter.Title}: no differences" : $"{charter.Title}:\n" + string.Join("\n", lines);
	}

	private static string YesNo(bool value) => value ? "yes" : "no";
}
