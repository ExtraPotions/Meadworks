using System;
using System.Reflection;
using HarmonyLib;

namespace ExtraPotions.Mantle.Embedded.Charter;

/// <summary>
/// Shows a stored refusal message on the game's connection-failed panel once, the next time the main menu
/// reports the failed connection. The label's type is not referenced; its text property is set by name.
/// </summary>
internal static class RefusalScreen
{
	private static readonly FieldInfo? LabelField = AccessTools.Field(typeof(FejdStartup), "m_connectionFailedError");

	public static string? Pending { get; set; }

	public static void Show(FejdStartup startup)
	{
		string? text = Pending;
		if (text == null)
		{
			return;
		}
		Pending = null;
		try
		{
			startup.m_connectionFailedPanel?.SetActive(true);
			object? label = LabelField?.GetValue(startup);
			PropertyInfo? property = label == null ? null : AccessTools.Property(label.GetType(), "text");
			property?.SetValue(label, text);
		}
		catch (Exception e)
		{
			GameHooks.LeadJournal?.Error($"showing the refusal failed: {e.Message}");
		}
	}
}
