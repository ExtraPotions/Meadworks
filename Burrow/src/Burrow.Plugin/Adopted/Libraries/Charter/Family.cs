using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Burrow.Embedded.Charter;

/// <summary>
/// The process-wide registry every copy of the library shares through <see cref="AppDomain"/> data: which
/// Charter-using mods are loaded, which copy leads (registers the family RPC, runs the join check, owns the
/// console command), and the status, diff and verbosity delegates each copy exposes to the lead.
/// </summary>
internal static class Family
{
	private const string RegistryKey = "Charter.Family";
	private const string LeadKey = "Charter.Family.Lead";

	public static void Register(FamilyEntry entry) => Registry()[entry.Guid] = entry.Format();

	/// <summary>True when this guid is, or just became, the lead of the family.</summary>
	public static bool ClaimLead(string guid)
	{
		if (AppDomain.CurrentDomain.GetData(LeadKey) is string lead)
		{
			return lead == guid;
		}
		AppDomain.CurrentDomain.SetData(LeadKey, guid);
		return true;
	}

	public static List<FamilyEntry> Entries()
	{
		return Registry().Select(pair => FamilyEntry.Parse(pair.Key, pair.Value)).OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase).ToList();
	}

	public static void SetStatus(string guid, Func<string> report) => AppDomain.CurrentDomain.SetData("Charter.Status." + guid, report);

	public static void SetDiff(string guid, Func<string> report) => AppDomain.CurrentDomain.SetData("Charter.Diff." + guid, report);

	public static void SetVerbosity(string guid, Action<int> set) => AppDomain.CurrentDomain.SetData("Charter.Verbosity." + guid, set);

	public static Func<string>? Status(string guid) => AppDomain.CurrentDomain.GetData("Charter.Status." + guid) as Func<string>;

	public static Func<string>? Diff(string guid) => AppDomain.CurrentDomain.GetData("Charter.Diff." + guid) as Func<string>;

	public static Action<int>? Verbosity(string guid) => AppDomain.CurrentDomain.GetData("Charter.Verbosity." + guid) as Action<int>;

	private static Dictionary<string, string> Registry()
	{
		if (AppDomain.CurrentDomain.GetData(RegistryKey) is Dictionary<string, string> registry)
		{
			return registry;
		}
		registry = new Dictionary<string, string>(StringComparer.Ordinal);
		AppDomain.CurrentDomain.SetData(RegistryKey, registry);
		return registry;
	}
}
