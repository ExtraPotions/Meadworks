using BepInEx.Logging;

namespace ExtraPotions.Mantle.Embedded.Charter;

/// <summary>Log lines prefixed with "Charter [Title]:", filtered by the copy-wide <see cref="Charter.Verbosity"/>.</summary>
internal sealed class Journal
{
	private static readonly ManualLogSource Source = Logger.CreateLogSource("Charter");
	private readonly string prefix;

	public Journal(string title) => prefix = $"Charter [{title}]: ";

	/// <summary>Always written.</summary>
	public void Error(string text) => Source.LogError(prefix + text);

	/// <summary>Normal and Trace.</summary>
	public void Warning(string text)
	{
		if (Charter.Verbosity != Verbosity.Quiet)
		{
			Source.LogWarning(prefix + text);
		}
	}

	/// <summary>Normal and Trace.</summary>
	public void Info(string text)
	{
		if (Charter.Verbosity != Verbosity.Quiet)
		{
			Source.LogInfo(prefix + text);
		}
	}

	/// <summary>Trace only.</summary>
	public void Trace(string text)
	{
		if (Charter.Verbosity == Verbosity.Trace)
		{
			Source.LogInfo(prefix + text);
		}
	}
}
