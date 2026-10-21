// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables
{
    /// <summary>
    /// One essence of the Rune Table (rune-table.md section 3): banked by sacrificing trophies, spent to steer a rune's
    /// roll toward its own inscriptions. Its icon is a game item's (<see cref="IconItem"/>), tinted nowhere: the essences
    /// look like the game's own materials.
    /// </summary>
    internal sealed class Essence
    {
        public Essence(int index, string id, string iconItem, Color color, params string[] inscriptions)
        {
            Index = index;
            Id = id;
            IconItem = iconItem;
            Color = color;
            Inscriptions = new HashSet<string>(inscriptions);
        }

        /// <summary>Its place in <see cref="Essences.All"/>, the order the table shows them in.</summary>
        public int Index { get; }

        public string Id { get; }

        /// <summary>The game item whose icon stands for it.</summary>
        public string IconItem { get; }

        /// <summary>Its colour in text (the name in the description and the pool lines).</summary>
        public Color Color { get; }

        /// <summary>The inscription ids it steers toward; ids the rules do not define are simply never drawn.</summary>
        public ISet<string> Inscriptions { get; }

        /// <summary>The localization key of its name.</summary>
        public string NameKey => "$ecf_essence_" + Id;
    }
}
