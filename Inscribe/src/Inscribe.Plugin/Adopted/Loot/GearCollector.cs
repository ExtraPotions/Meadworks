// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; the thin-base
// note is information (0.10.0 Magic needs two inscriptions, so tools never drop; nothing for the player to do).
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// One collection of the drop-eligible bases (<see cref="GearBases"/>) against one rules snapshot, fed one item
    /// prefab at a time: all at once by <see cref="GearBases.Collect"/>, or a slice a frame by <see cref="GearPoolWarmup"/>.
    /// A base that cannot fill the lowest magic rarity's minimum is left out, and named in one information line at the end.
    /// Main thread only.
    /// </summary>
    internal sealed class GearCollector
    {
        private readonly GearDropRules _gear;
        private readonly HashSet<string> _exclude;
        private readonly RarityDef? _lowest;
        private readonly int _minimum;
        private readonly List<GearBase> _bases = new List<GearBase>();
        private readonly List<string> _thin = new List<string>();

        public GearCollector(RuleSet rules)
        {
            Rules = rules;
            _gear = rules.Economy.Drops.Gear;
            _exclude = new HashSet<string>(_gear.Exclude, StringComparer.Ordinal);
            _lowest = GearBases.LowestMagic(rules.Economy);
            _minimum = _lowest == null ? 1 : Math.Max(1, _lowest.MinAffixes);
        }

        /// <summary>The rules snapshot the bases are judged against.</summary>
        public RuleSet Rules { get; }

        public void Add(GameObject? prefab)
        {
            GearBase? found = prefab == null ? null : GearBases.TryBase(prefab, _gear, _exclude, Rules);
            if (found != null && _lowest != null && found.CapacityFor(_lowest) < _minimum)
            {
                _thin.Add(found.Name);
            }
            else if (found != null)
            {
                _bases.Add(found);
            }
        }

        /// <summary>The bases collected, after the one warning about the bases left out.</summary>
        public List<GearBase> Finish()
        {
            if (_thin.Count > 0)
            {
                Log.Info($"gear drops: {_thin.Count} bases cannot fill {_minimum} inscriptions at their item level and never drop: {string.Join(", ", _thin)}");
            }
            return _bases;
        }
    }
}
