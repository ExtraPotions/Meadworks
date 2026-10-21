// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the one-time move of untouched door distances to the halved defaults (October 20, 2026 playtest).
using System.Collections.Generic;

namespace ExtraPotions.Ember.Doors
{
    /// <summary>
    /// Open Distance and Close Distance now default to half the earlier per-kind defaults (doors 1.5 / 2.25 m,
    /// drawbridges 3 / 4 m). A saved value that equals the kind's
    /// earlier default was never chosen by the player, so it moves to the new default once; any other saved value is
    /// kept. A hidden per-config marker makes the move run once, so a distance the player later sets back to the old
    /// default stays.
    /// </summary>
    public static class DoorDistanceDefaults
    {
        /// <summary>The marker value once the move has run.</summary>
        public const int Current = 1;

        /// <summary>The hidden marker entry (section, key).</summary>
        public const string MarkerSection = "Doors";
        public const string MarkerKey = "Distance Defaults Version";

        /// <summary>One saved distance, the earlier default it is compared with and the default it moves to.</summary>
        public readonly struct Saved
        {
            public Saved(string id, float value, float oldDefault, float newDefault)
            {
                Id = id;
                Value = value;
                OldDefault = oldDefault;
                NewDefault = newDefault;
            }

            public string Id { get; }
            public float Value { get; }
            public float OldDefault { get; }
            public float NewDefault { get; }
        }

        /// <summary>
        /// The entries whose saved value equals their earlier default exactly, or none once <paramref name="marker"/> has
        /// reached <see cref="Current"/>. Entries already at the new default are not listed.
        /// </summary>
        public static List<Saved> Plan(int marker, IEnumerable<Saved>? entries)
        {
            var moved = new List<Saved>();
            if (marker >= Current || entries == null)
                return moved;
            foreach (var entry in entries)
                if (entry.Value == entry.OldDefault && entry.Value != entry.NewDefault)
                    moved.Add(entry);
            return moved;
        }

        /// <summary>Every door distance setting with its earlier default, read through <paramref name="value"/>.</summary>
        public static IEnumerable<Saved> Entries(System.Func<string, float> value)
        {
            foreach (var kind in DoorKinds.All)
            {
                var settings = Config.EmberSettings.Door(kind);
                yield return new Saved(settings.OpenDistance.Id, value(settings.OpenDistance.Id), kind.PreviousOpenDistance, kind.DefaultOpenDistance);
                yield return new Saved(settings.CloseDistance.Id, value(settings.CloseDistance.Id), kind.PreviousCloseDistance, kind.DefaultCloseDistance);
            }
        }
    }
}
