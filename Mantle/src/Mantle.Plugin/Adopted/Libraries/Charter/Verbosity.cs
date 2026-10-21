namespace ExtraPotions.Mantle.Embedded.Charter;

/// <summary>
/// How much Charter writes to the log. Quiet: errors and refusals only. Normal: plus one line per push and per
/// steward edit. Trace: plus every fragment and every applied clause value.
/// </summary>
public enum Verbosity
{
	Quiet,
	Normal,
	Trace,
}
