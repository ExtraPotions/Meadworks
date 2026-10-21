using System;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Burrow.Embedded.YamlConfig;

/// <summary>
/// Parses scalar text for the typed <see cref="YamlNode"/> readers: int and float in the invariant culture, bool
/// as true/false, yes/no, on/off or 1/0, string as written, enum members by name; names are case-insensitive.
/// Also words the type descriptions used in messages.
/// </summary>
internal static class YamlScalarParser
{
	private static readonly string[] TrueWords = { "true", "yes", "on", "1" };
	private static readonly string[] FalseWords = { "false", "no", "off", "0" };

	public static bool TryParse(string text, Type type, out object? result)
	{
		string trimmed = text.Trim();
		result = null;
		if (type == typeof(string))
		{
			result = text;
			return true;
		}
		if (type == typeof(int))
		{
			bool ok = int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i);
			result = i;
			return ok;
		}
		if (type == typeof(float))
		{
			bool ok = float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float f);
			result = f;
			return ok;
		}
		return type == typeof(bool) ? TryParseBool(trimmed, out result) : TryParseEnum(trimmed, type, out result);
	}

	/// <summary>What one value of the type is called in a message: "an integer", "a number", "true or false", "one of A, B" or "text".</summary>
	public static string Describe(Type type)
	{
		if (type == typeof(int))
		{
			return "an integer";
		}
		if (type == typeof(float))
		{
			return "a number";
		}
		if (type == typeof(bool))
		{
			return "true or false";
		}
		return type.IsEnum ? "one of " + string.Join(", ", Enum.GetNames(type)) : "text";
	}

	/// <summary>What several values of the type are called in a list message.</summary>
	public static string Plural<T>() => typeof(T) == typeof(int) ? "integers"
		: typeof(T) == typeof(float) ? "numbers"
		: typeof(T) == typeof(bool) ? "true/false values"
		: typeof(T).IsEnum ? "names (" + string.Join(", ", Enum.GetNames(typeof(T))) + ")"
		: "text values";

	private static bool TryParseBool(string text, out object? result)
	{
		if (TrueWords.Contains(text, StringComparer.OrdinalIgnoreCase))
		{
			result = true;
			return true;
		}
		result = false;
		return FalseWords.Contains(text, StringComparer.OrdinalIgnoreCase);
	}

	private static bool TryParseEnum(string text, Type type, out object? result)
	{
		if (!type.IsEnum)
		{
			throw new NotSupportedException($"YamlNode cannot read values of type {type.Name}");
		}
		string? name = Enum.GetNames(type).FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
		result = name is null ? null : Enum.Parse(type, name);
		return name is not null;
	}
}
