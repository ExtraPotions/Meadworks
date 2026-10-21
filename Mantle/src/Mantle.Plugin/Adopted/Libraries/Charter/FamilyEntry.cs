using System.Collections.Generic;

namespace ExtraPotions.Mantle.Embedded.Charter;

/// <summary>One mod in the family: what the registry stores and what the join check exchanges.</summary>
internal sealed class FamilyEntry
{
	public FamilyEntry(string guid, string title, string version, string oldestAccepted, bool mandatory)
	{
		Guid = guid;
		Title = title.Replace('|', '/');
		Version = version;
		OldestAccepted = oldestAccepted;
		Mandatory = mandatory;
	}

	public string Guid { get; }
	public string Title { get; }
	public string Version { get; }
	public string OldestAccepted { get; }
	public bool Mandatory { get; }

	/// <summary>The registry value: title|version|oldestAccepted|mandatory.</summary>
	public string Format() => $"{Title}|{Version}|{OldestAccepted}|{(Mandatory ? "true" : "false")}";

	public static FamilyEntry Parse(string guid, string value)
	{
		string[] parts = value.Split('|');
		string title = parts.Length > 0 ? parts[0] : guid;
		string version = parts.Length > 1 ? parts[1] : "0";
		string oldest = parts.Length > 2 ? parts[2] : version;
		bool mandatory = parts.Length > 3 && parts[3] == "true";
		return new FamilyEntry(guid, title, version, oldest, mandatory);
	}

	public string Describe() => $"{Title} {Version} (accepts {OldestAccepted} and newer, {(Mandatory ? "mandatory" : "optional")})";

	public static void Write(ZPackage pkg, List<FamilyEntry> entries)
	{
		pkg.Write(Fragmenter.Protocol);
		pkg.Write(entries.Count);
		foreach (FamilyEntry entry in entries)
		{
			pkg.Write(entry.Guid);
			pkg.Write(entry.Title);
			pkg.Write(entry.Version);
			pkg.Write(entry.OldestAccepted);
			pkg.Write(entry.Mandatory);
		}
	}

	/// <summary>Null when the package speaks another protocol.</summary>
	public static List<FamilyEntry>? Read(ZPackage pkg)
	{
		if (pkg.ReadInt() != Fragmenter.Protocol)
		{
			return null;
		}
		int count = pkg.ReadInt();
		List<FamilyEntry> entries = new(count);
		for (int i = 0; i < count; i++)
		{
			string guid = pkg.ReadString();
			string title = pkg.ReadString();
			string version = pkg.ReadString();
			string oldest = pkg.ReadString();
			entries.Add(new FamilyEntry(guid, title, version, oldest, pkg.ReadBool()));
		}
		return entries;
	}
}
