// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: a Seasons settings JSON file mapped onto Solstice settings. Night, torches, wood, meat and breeding are not Solstice settings.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ExtraPotions.Solstice.Calendar;

namespace ExtraPotions.Solstice.Config
{
    public readonly struct ImportChange
    {
        public ImportChange(string id, string text)
        {
            Id = id;
            Text = text;
        }

        public string Id { get; }
        public string Text { get; }
    }

    public sealed class ImportPlan
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = "";
        public List<ImportChange> Changes { get; } = new List<ImportChange>();
        public List<string> Unmapped { get; } = new List<string>();
        public List<string> Unknown { get; } = new List<string>();
    }

    /// <summary>
    /// Reads a JSON object of season names, or one season object with a season field, into Solstice setting ids.
    /// Apply a change only when the stored value is still the Solstice default (<see cref="ShouldApply"/>).
    /// </summary>
    public static class SeasonConfigImport
    {
        static readonly Dictionary<string, string> Fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["daysInSeason"] = "Calendar",
            ["plantsGrowthMultiplier"] = "Plants Growth Multiplier",
            ["beehiveProductionMultiplier"] = "Beehive Production Multiplier",
            ["foodDrainMultiplier"] = "Food Drain Multiplier",
            ["staminaDrainMultiplier"] = "Stamina Drain Multiplier",
            ["fireplaceDrainMultiplier"] = "Fireplace Drain Multiplier",
            ["sapCollectingSpeedMultiplier"] = "Sap Collecting Speed Multiplier",
            ["rainProtection"] = "Rain Protection",
            ["windIntensityMultiplier"] = "Wind Intensity Multiplier",
            ["restedBuffDurationMultiplier"] = "Rested Buff Duration Multiplier",
        };

        static readonly HashSet<string> OwnedElsewhere = new HashSet<string>(StringComparer.Ordinal)
        {
            "nightLength", "torchAsFiresource", "torchDurabilityDrain", "woodFromTreesMultiplier",
            "livestockProcreationMultiplier", "meatFromAnimalsMultiplier", "treesRegrowthChance", "overheatIn2WarmClothes",
        };

        public static bool ShouldApply(string? current, string? factoryDefault, string? proposed) =>
            current != null && factoryDefault != null && proposed != null
            && string.Equals(current, factoryDefault, StringComparison.Ordinal)
            && !string.Equals(proposed, factoryDefault, StringComparison.Ordinal);

        public static ImportPlan Plan(string? json)
        {
            var plan = new ImportPlan();
            if (string.IsNullOrWhiteSpace(json))
            {
                plan.Error = "Import file is empty.";
                return plan;
            }
            JsonValue root;
            try { root = JsonReader.Parse(json!); }
            catch (FormatException error)
            {
                plan.Error = error.Message;
                return plan;
            }
            if (root.Kind != JsonKind.Object)
            {
                plan.Error = "Import file must be a JSON object.";
                return plan;
            }
            plan.Ok = true;
            if (root.Object!.ContainsKey("spring") || root.Object.ContainsKey("summer") || root.Object.ContainsKey("fall") || root.Object.ContainsKey("winter"))
            {
                foreach (var season in Seasons.All) ReadSeason(plan, season, Child(root, season.ToString().ToLowerInvariant()));
                return plan;
            }
            if (!TrySeason(Child(root, "season"), out var single))
            {
                plan.Ok = false;
                plan.Error = "Import file needs spring, summer, fall and winter objects, or a season field.";
                return plan;
            }
            ReadSeason(plan, single, root);
            return plan;
        }

        static void ReadSeason(ImportPlan plan, Season season, JsonValue? body)
        {
            if (body == null) return;
            if (body.Kind != JsonKind.Object)
            {
                plan.Unknown.Add(season.ToString().ToLowerInvariant());
                return;
            }
            foreach (var pair in body.Object!)
            {
                if (pair.Key == "season") continue;
                if (OwnedElsewhere.Contains(pair.Key))
                {
                    plan.Unmapped.Add(season.ToString().ToLowerInvariant() + "." + pair.Key);
                    continue;
                }
                if (!Fields.TryGetValue(pair.Key, out var key))
                {
                    plan.Unknown.Add(season.ToString().ToLowerInvariant() + "." + pair.Key);
                    continue;
                }
                if (!TryText(pair.Value, out var text))
                {
                    plan.Unknown.Add(season.ToString().ToLowerInvariant() + "." + pair.Key);
                    continue;
                }
                string id = key == "Calendar" ? "Calendar.Days In " + season : season + "." + key;
                plan.Changes.Add(new ImportChange(id, text));
            }
        }

        static bool TryText(JsonValue value, out string text)
        {
            text = "";
            switch (value.Kind)
            {
                case JsonKind.Bool:
                    text = value.Bool ? "true" : "false";
                    return true;
                case JsonKind.Number:
                    text = value.Number.ToString("0.###", CultureInfo.InvariantCulture);
                    return true;
                case JsonKind.String:
                    text = value.Text ?? "";
                    return true;
                default:
                    return false;
            }
        }

        static JsonValue? Child(JsonValue root, string name) =>
            root.Object != null && root.Object.TryGetValue(name, out var value) ? value : null;

        static bool TrySeason(JsonValue? value, out Season season)
        {
            season = Season.Spring;
            if (value == null || value.Kind != JsonKind.String || value.Text == null) return false;
            return Enum.TryParse(value.Text, true, out season) && Enum.IsDefined(typeof(Season), season);
        }
    }

    enum JsonKind { Null, Bool, Number, String, Object, Array }

    sealed class JsonValue
    {
        public JsonKind Kind;
        public bool Bool;
        public double Number;
        public string? Text;
        public Dictionary<string, JsonValue>? Object;
    }

    sealed class JsonReader
    {
        readonly string text;
        int index;

        JsonReader(string text) => this.text = text;

        public static JsonValue Parse(string text)
        {
            var reader = new JsonReader(text);
            var value = reader.ReadValue();
            reader.Skip();
            if (reader.index != reader.text.Length) throw new FormatException("Import file has trailing text.");
            return value;
        }

        JsonValue ReadValue()
        {
            Skip();
            if (index >= text.Length) throw new FormatException("Import file ended early.");
            char c = text[index];
            if (c == '{') return ReadObject();
            if (c == '[') return ReadArray();
            if (c == '"') return new JsonValue { Kind = JsonKind.String, Text = ReadString() };
            if (c == 't' || c == 'f') return ReadBool();
            if (c == 'n') return ReadNull();
            if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
            throw new FormatException("Import file has a value Solstice does not read.");
        }

        JsonValue ReadObject()
        {
            index++;
            var map = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            Skip();
            if (index < text.Length && text[index] == '}')
            {
                index++;
                return new JsonValue { Kind = JsonKind.Object, Object = map };
            }
            while (true)
            {
                Skip();
                if (index >= text.Length || text[index] != '"') throw new FormatException("Import file has an object key that is not text.");
                string key = ReadString();
                Skip();
                if (index >= text.Length || text[index] != ':') throw new FormatException("Import file is missing a colon.");
                index++;
                map[key] = ReadValue();
                Skip();
                if (index >= text.Length) throw new FormatException("Import file ended inside an object.");
                if (text[index] == ',') { index++; continue; }
                if (text[index] == '}') { index++; break; }
                throw new FormatException("Import file has a broken object.");
            }
            return new JsonValue { Kind = JsonKind.Object, Object = map };
        }

        JsonValue ReadArray()
        {
            index++;
            Skip();
            if (index < text.Length && text[index] == ']')
            {
                index++;
                return new JsonValue { Kind = JsonKind.Array };
            }
            while (true)
            {
                ReadValue();
                Skip();
                if (index >= text.Length) throw new FormatException("Import file ended inside a list.");
                if (text[index] == ',') { index++; continue; }
                if (text[index] == ']') { index++; break; }
                throw new FormatException("Import file has a broken list.");
            }
            return new JsonValue { Kind = JsonKind.Array };
        }

        JsonValue ReadBool()
        {
            if (Match("true")) return new JsonValue { Kind = JsonKind.Bool, Bool = true };
            if (Match("false")) return new JsonValue { Kind = JsonKind.Bool, Bool = false };
            throw new FormatException("Import file has a broken boolean.");
        }

        JsonValue ReadNull()
        {
            if (!Match("null")) throw new FormatException("Import file has a broken null.");
            return new JsonValue { Kind = JsonKind.Null };
        }

        JsonValue ReadNumber()
        {
            int start = index;
            if (text[index] == '-') index++;
            while (index < text.Length && text[index] >= '0' && text[index] <= '9') index++;
            if (index < text.Length && text[index] == '.')
            {
                index++;
                while (index < text.Length && text[index] >= '0' && text[index] <= '9') index++;
            }
            if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
            {
                index++;
                if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++;
                while (index < text.Length && text[index] >= '0' && text[index] <= '9') index++;
            }
            var slice = text.Substring(start, index - start);
            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                throw new FormatException("Import file has a broken number.");
            return new JsonValue { Kind = JsonKind.Number, Number = number };
        }

        string ReadString()
        {
            index++;
            var builder = new StringBuilder();
            while (index < text.Length)
            {
                char c = text[index++];
                if (c == '"') return builder.ToString();
                if (c != '\\') { builder.Append(c); continue; }
                if (index >= text.Length) break;
                char esc = text[index++];
                switch (esc)
                {
                    case '"':
                    case '\\':
                    case '/': builder.Append(esc); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > text.Length) throw new FormatException("Import file has a broken text escape.");
                        builder.Append((char)int.Parse(text.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        index += 4;
                        break;
                    default: throw new FormatException("Import file has a broken text escape.");
                }
            }
            throw new FormatException("Import file has an unfinished text value.");
        }

        bool Match(string word)
        {
            if (index + word.Length > text.Length || text.Substring(index, word.Length) != word) return false;
            index += word.Length;
            return true;
        }

        void Skip()
        {
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        }
    }
}
