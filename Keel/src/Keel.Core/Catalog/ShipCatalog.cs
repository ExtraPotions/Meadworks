// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the ship catalog of design 1.1 C. Behavior reference only: the ship list, shapes, top speeds,
// health, slot counts and recipes are the published numbers of the TheGreatestShips 0.9.10 README (DeathMonger, no
// license found; README pinned at 8aa408759078a666b1d485cdd28d7a2db5cb5d09), adopted as defaults by user decision Q3.
// No TheGreatestShips code was read or copied. Prefab names, color values, stripe counts and the cargo grid split are Keel's.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Keel.Catalog
{
    public enum KeelShip { FastKarve, FastLongship, Knarr, Busse, BigBusse, SmallByrding, Byrding, GreaterByrding }

    /// <summary>One Keel ship type: a runtime clone of a vanilla hull, shaped, tinted and tuned.</summary>
    public sealed class ShipType
    {
        internal ShipType(KeelShip ship, string name, string baseHull, float width, float length, float scale, float topSpeed, float health,
            int columns, int rows, int penCapacity, string recipe, string sailColor, string hullColor, int stripes)
        {
            Ship = ship;
            Name = name;
            BaseHull = baseHull;
            Width = width;
            Length = length;
            Scale = scale;
            TopSpeed = topSpeed;
            Health = health;
            CargoColumns = columns;
            CargoRows = rows;
            PenCapacity = penCapacity;
            Recipe = recipe;
            SailColor = sailColor;
            HullColor = hullColor;
            HullStripes = stripes;
        }

        public KeelShip Ship { get; }

        /// <summary>Keel's own prefab name (never TheGreatestShips'), for example <c>KeelFastKarve</c>.</summary>
        public string Prefab => "Keel" + Ship;

        /// <summary>English name; also the section name of the ship's catalog settings.</summary>
        public string Name { get; }

        /// <summary>C05 key of the ship name: <c>ship_fast_karve</c>.</summary>
        public string TextKey => "ship_" + string.Concat(Ship.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

        /// <summary>C05 key of the piece description: <c>ship_fast_karve_description</c>.</summary>
        public string DescriptionKey => TextKey + "_description";

        /// <summary>The vanilla prefab the hull is cloned from: <c>Karve</c> or <c>VikingShip</c> (the Longship).</summary>
        public string BaseHull { get; }

        /// <summary>Width and length relative to the base hull; <see cref="Scale"/> sizes the whole ship and the two multiply on top.</summary>
        public float Width { get; }
        public float Length { get; }
        public float Scale { get; }

        /// <summary>Measured top speed in m/s in full wind about 64 degrees off the stern (README).</summary>
        public float TopSpeed { get; }

        public float Health { get; }
        public int CargoColumns { get; }
        public int CargoRows { get; }

        /// <summary>Animals the deck pen holds (about one per 2 square meters); 0 for ships without a pen.</summary>
        public int PenCapacity { get; }

        public bool HasPen => PenCapacity > 0;

        /// <summary>Up to five <c>Prefab:Amount</c> pairs.</summary>
        public string Recipe { get; }

        /// <summary>Multiplicative sail tint, hex <c>RRGGBB</c> (white leaves it unchanged).</summary>
        public string SailColor { get; }

        /// <summary>Paint multiplied into the hull planks, hex <c>RRGGBB</c>; empty keeps the vanilla planks.</summary>
        public string HullColor { get; }

        /// <summary>Painted bands on the hull; 0 paints it solid.</summary>
        public int HullStripes { get; }

        /// <summary>
        /// The default per-ship SailForce: the base hull's value scaled by the square of the speed ratio, since the
        /// game's forward drag grows with the square of the speed (the same relation the helm speed perk uses).
        /// </summary>
        public float DefaultSailForce(float baseHullSailForce)
        {
            if (float.IsNaN(baseHullSailForce) || baseHullSailForce <= 0f) return 0f;
            var ratio = TopSpeed / ShipCatalog.VanillaTopSpeed(BaseHull);
            return baseHullSailForce * ratio * ratio;
        }

        /// <summary>
        /// Rudder swing speed relative to the base hull (user decision 2026-10-08: Knarr 0.9, Busse 0.8, Big Busse 0.7,
        /// Small Byrding 1.0, Byrding 0.95, Greater Byrding 0.85; the fast ships keep the hull's). The README only says
        /// "slower"; the numbers are Keel's.
        /// </summary>
        public float RudderSpeed
        {
            get
            {
                switch (Ship)
                {
                    case KeelShip.Knarr: return 0.9f;
                    case KeelShip.Busse: return 0.8f;
                    case KeelShip.BigBusse: return 0.7f;
                    case KeelShip.Byrding: return 0.95f;
                    case KeelShip.GreaterByrding: return 0.85f;
                    default: return 1f;
                }
            }
        }

        /// <summary>The default per-ship Rudder Speed: the base hull's times <see cref="RudderSpeed"/>; 0 for an invalid hull value.</summary>
        public float DefaultRudderSpeed(float baseHullRudderSpeed) =>
            float.IsNaN(baseHullRudderSpeed) || float.IsInfinity(baseHullRudderSpeed) || baseHullRudderSpeed <= 0f ? 0f : baseHullRudderSpeed * RudderSpeed;

        public override string ToString() => Name + " (" + Prefab + ")";
    }

    public static class ShipCatalog
    {
        public const string Karve = "Karve";
        public const string Longship = "VikingShip";

        // Keel's tints for the README's color words: blue sail, red stripes; amber sail, green stripes; white sail, brown hull.
        private const string Blue = "3A5FA8", Red = "B03A2E", Amber = "D9A441", Green = "3F7A3A", White = "FFFFFF", Brown = "7A5634";

        private static readonly ShipType[] all =
        {
            new ShipType(KeelShip.FastKarve, "Fast Karve", Karve, 0.85f, 1f, 1f, 10.1f, 400f, 2, 1, 0,
                "FineWood:30, ElderBark:20, BronzeNails:60, TrollHide:8, Resin:20", Blue, Red, 3),
            new ShipType(KeelShip.FastLongship, "Fast Longship", Longship, 0.85f, 1.1f, 1f, 13.0f, 800f, 6, 2, 0,
                "FineWood:40, ElderBark:40, IronNails:120, DeerHide:10, TrollHide:10", Blue, Red, 3),
            new ShipType(KeelShip.Knarr, "Knarr", Longship, 1.25f, 1f, 1f, 8.6f, 1100f, 8, 4, 0,
                "FineWood:40, ElderBark:50, IronNails:120, DeerHide:10, TrollHide:10", Amber, Green, 3),
            new ShipType(KeelShip.Busse, "Busse", Longship, 1.4f, 1.25f, 1f, 8.0f, 1500f, 10, 5, 0,
                "FineWood:50, ElderBark:70, IronNails:150, DeerHide:15, TrollHide:15", Amber, Green, 3),
            new ShipType(KeelShip.BigBusse, "Big Busse", Longship, 1.5f, 1.5f, 1f, 7.5f, 1900f, 11, 6, 0,
                "FineWood:60, ElderBark:90, IronNails:180, DeerHide:20, TrollHide:20", Amber, Green, 3),
            new ShipType(KeelShip.SmallByrding, "Small Byrding", Longship, 1f, 1f, 1f, 8.6f, 1000f, 6, 2, 3,
                "FineWood:40, ElderBark:40, IronNails:100, DeerHide:12, Wood:24", White, Brown, 0),
            new ShipType(KeelShip.Byrding, "Byrding", Longship, 1f, 1f, 1.25f, 8.3f, 1400f, 6, 3, 10,
                "FineWood:55, ElderBark:55, IronNails:150, DeerHide:15, Wood:36", White, Brown, 0),
            new ShipType(KeelShip.GreaterByrding, "Greater Byrding", Longship, 1f, 1f, 1.5f, 7.7f, 1600f, 8, 3, 15,
                "FineWood:65, ElderBark:65, IronNails:200, Wood:50, LoxPelt:20", White, Brown, 0),
        };

        /// <summary>The eight ships in design order (C1 to C8).</summary>
        public static IReadOnlyList<ShipType> All { get; } = Array.AsReadOnly(all);

        public static ShipType Get(KeelShip ship) => all[(int)ship];

        public static bool TryGetByPrefab(string? prefab, out ShipType type)
        {
            type = all.FirstOrDefault(t => t.Prefab == prefab)!;
            return type != null;
        }

        /// <summary>The README's measured vanilla top speeds: Karve 7.3 m/s, Longship 9.65 m/s.</summary>
        public static float VanillaTopSpeed(string hull)
        {
            switch (hull)
            {
                case Karve: return 7.3f;
                case Longship: return 9.65f;
                default: throw new ArgumentException("No measured top speed for " + hull + ".", nameof(hull));
            }
        }
    }
}