// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room: the Markers tab's map (design C5, CA07) on Crucible.UI KX09 KitMapView. Behavior from
// TimeWeatherHud 1.1.0's README (pins grouped by type next to a map; click to show or hide a pin on the compass); no code.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Cairn.Hud
{
    /// <summary>One of the player's map pins as the game holds it: pin type name, world x and z, and its label.</summary>
    public sealed class MapPinInfo
    {
        public MapPinInfo(string type, float x, float z, string name)
        {
            Type = (type ?? "").Trim();
            X = x;
            Z = z;
            Name = name ?? "";
        }

        public string Type { get; }
        public float X { get; }
        public float Z { get; }
        public string Name { get; }
    }

    /// <summary>Why a pin is on the compass: not at all, through its type entry, or through its own entry.</summary>
    public enum MarkerShown { Hidden, ByType, ByPin }

    public sealed class MarkerMapPin
    {
        internal MarkerMapPin(MapPinInfo pin, string entry, MarkerShown shown)
        {
            Pin = pin;
            Entry = entry;
            Shown = shown;
        }

        public MapPinInfo Pin { get; }

        /// <summary>The pin's own marker entry (<see cref="MarkerChoices.PinKey"/>).</summary>
        public string Entry { get; }

        public MarkerShown Shown { get; }
    }

    public sealed class MarkerTypeGroup
    {
        internal MarkerTypeGroup(string type, int count, bool chosen)
        {
            Type = type;
            Count = count;
            Chosen = chosen;
        }

        public string Type { get; }
        public int Count { get; }

        /// <summary>The whole type is on the compass (<c>type:&lt;Type&gt;</c>).</summary>
        public bool Chosen { get; }

        public string Entry => TypeEntry(Type);

        internal static string TypeEntry(string type) => "type:" + type;
    }

    /// <summary>The result of a pin click.</summary>
    public readonly struct MarkerToggle
    {
        public MarkerToggle(MarkerChoices choices, bool changed, bool shownByType)
        {
            Choices = choices;
            Changed = changed;
            ShownByType = shownByType;
        }

        public MarkerChoices Choices { get; }
        public bool Changed { get; }

        /// <summary>The pin stays on the compass because its whole type is chosen (the list's type toggle changes that).</summary>
        public bool ShownByType { get; }
    }

    /// <summary>The pins the Markers map shows, in the order the map view holds them: grouped by type (ordinal), then by
    /// name, then position. Pins with an unusable type name or a non-finite position are left out.</summary>
    public sealed class MarkerMapPlan
    {
        private MarkerMapPlan(IReadOnlyList<MarkerMapPin> pins, IReadOnlyList<MarkerTypeGroup> types)
        {
            Pins = pins;
            Types = types;
        }

        public IReadOnlyList<MarkerMapPin> Pins { get; }
        public IReadOnlyList<MarkerTypeGroup> Types { get; }

        public static MarkerMapPlan Build(IEnumerable<MapPinInfo> pins, MarkerChoices choices)
        {
            if (pins == null) throw new ArgumentNullException(nameof(pins));
            if (choices == null) throw new ArgumentNullException(nameof(choices));
            var usable = pins.Where(p => p != null && Finite(p.X) && Finite(p.Z) && MarkerChoices.IsEntry(MarkerTypeGroup.TypeEntry(p.Type)))
                .OrderBy(p => p.Type, StringComparer.Ordinal)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.X).ThenBy(p => p.Z)
                .ToList();
            var result = new List<MarkerMapPin>(usable.Count);
            foreach (var pin in usable)
            {
                var entry = MarkerChoices.PinKey(pin.X, pin.Z, pin.Type);
                var shown = choices.Contains(MarkerTypeGroup.TypeEntry(pin.Type)) ? MarkerShown.ByType
                    : choices.Contains(entry) ? MarkerShown.ByPin : MarkerShown.Hidden;
                result.Add(new MarkerMapPin(pin, entry, shown));
            }
            var types = usable.GroupBy(p => p.Type, StringComparer.Ordinal)
                .Select(g => new MarkerTypeGroup(g.Key, g.Count(), choices.Contains(MarkerTypeGroup.TypeEntry(g.Key))))
                .ToList();
            return new MarkerMapPlan(result.AsReadOnly(), types.AsReadOnly());
        }

        /// <summary>A click on pin <paramref name="index"/>: adds or removes its own entry. A pin whose type is chosen stays
        /// shown and the choices are unchanged; an index out of range changes nothing.</summary>
        public MarkerToggle Toggle(int index, MarkerChoices choices)
        {
            if (choices == null) throw new ArgumentNullException(nameof(choices));
            if (index < 0 || index >= Pins.Count) return new MarkerToggle(choices, false, false);
            var pin = Pins[index];
            if (choices.Contains(MarkerTypeGroup.TypeEntry(pin.Pin.Type))) return new MarkerToggle(choices, false, true);
            var next = choices.Contains(pin.Entry) ? choices.Without(pin.Entry) : choices.With(pin.Entry);
            return new MarkerToggle(next, true, false);
        }

        /// <summary>A click on a type in the list: shows or hides every pin of that type. An unusable type changes nothing.</summary>
        public MarkerChoices ToggleType(string type, MarkerChoices choices)
        {
            if (choices == null) throw new ArgumentNullException(nameof(choices));
            var entry = MarkerTypeGroup.TypeEntry((type ?? "").Trim());
            if (!MarkerChoices.IsEntry(entry)) return choices;
            return choices.Contains(entry) ? choices.Without(entry) : choices.With(entry);
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
