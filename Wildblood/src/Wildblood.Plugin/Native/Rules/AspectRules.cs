// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The boss-aspect rules from the `aspects:` block under `bosses:` - the switch, the altar shift interval, the roll
    /// weights, the loot multipliers, each aspect's numbers and the per-boss lists. A plain data holder the parser
    /// fills over <see cref="AspectDefaults"/>; the aspects read it live from <see cref="RuleState.Active"/>, so an edit
    /// retunes a fight already in progress without changing which aspect a boss already has.
    /// </summary>
    public sealed class AspectRules
    {
        /// <summary>Off: no altar shows an aspect and no boss rolls one. Boss stars do not depend on it.</summary>
        public bool Enabled = true;

        /// <summary>Real seconds between altar shifts; 0 fixes every altar's stars and aspect once they are first rolled.</summary>
        public float ShiftSeconds = 15f;

        /// <summary>An older file's `shift hours`, in in-game hours: set only when the file names it and not `shift seconds`,
        /// and then the interval in place of <see cref="ShiftSeconds"/>. Null otherwise.</summary>
        public float? ShiftHours;

        /// <summary>Real seconds between altar shifts, from whichever field the file named; 0 when every altar is fixed.</summary>
        public double ShiftInterval(double hourSeconds) => ShiftHours is float hours ? hours * hourSeconds : ShiftSeconds;

        public readonly Dictionary<Aspect, float> Chances = new Dictionary<Aspect, float>();
        public readonly Dictionary<Aspect, float> Loot = new Dictionary<Aspect, float>();
        public readonly Dictionary<Aspect, Dictionary<string, float>> Power = new Dictionary<Aspect, Dictionary<string, float>>();

        /// <summary>The power fields that hold a list rather than one number - Phantom's `split at` marks.</summary>
        public readonly Dictionary<Aspect, Dictionary<string, float[]>> Lists = new Dictionary<Aspect, Dictionary<string, float[]>>();

        public readonly Dictionary<string, BossAspectRule> Bosses =
            new Dictionary<string, BossAspectRule>(StringComparer.OrdinalIgnoreCase);

        public float ChanceOf(Aspect aspect) => Chances.TryGetValue(aspect, out float value) ? Mathf.Max(0f, value) : 0f;

        public float LootOf(Aspect aspect) => Loot.TryGetValue(aspect, out float value) && value > 0f ? value : 1f;

        /// <summary>A named number of one aspect, falling back to the built-in default when the file leaves it out.</summary>
        public float PowerOf(Aspect aspect, string field)
        {
            return Power.TryGetValue(aspect, out Dictionary<string, float> fields)
                && fields.TryGetValue(field, out float value) ? value : AspectDefaults.Power(aspect, field);
        }

        /// <summary>A named list of one aspect, falling back to the built-in default when the file leaves it out.</summary>
        public float[] ListOf(Aspect aspect, string field)
        {
            return Lists.TryGetValue(aspect, out Dictionary<string, float[]> fields)
                && fields.TryGetValue(field, out float[] value) ? value : AspectDefaults.List(aspect, field);
        }

        /// <summary>What Summoner calls for this boss prefab; empty when the file names nothing for it.</summary>
        public List<string> SummonsFor(string bossPrefab) =>
            Bosses.TryGetValue(bossPrefab, out BossAspectRule rule) ? rule.Summons : new List<string>();

        /// <summary>Whether this boss may roll this aspect: its own list if it has one, and Summoner only with summons.</summary>
        public bool InRotation(string bossPrefab, Aspect aspect)
        {
            if (ExtraPotions.Wildblood.Runtime.NativeCreatureRules.Find(bossPrefab)?.Excludes(aspect.ToString()) == true) return false;
            Bosses.TryGetValue(bossPrefab, out BossAspectRule? rule);
            if (aspect == Aspect.Summoner && (rule == null || rule.Summons.Count == 0))
            {
                return false; // a boss with nothing to call never rolls Summoner rather than rolling it and doing nothing
            }
            if (aspect == Aspect.Portalbound && !Aspects.PortalAttacks.Supports(bossPrefab))
            {
                return false; // only a boss with an attack the portals know how to carry: the Elder and Bonemass
            }
            return aspect == Aspect.None || rule?.Rotation == null || rule.Rotation.Contains(aspect);
        }

        public AspectRules Clone()
        {
            AspectRules copy = new AspectRules { Enabled = Enabled, ShiftSeconds = ShiftSeconds, ShiftHours = ShiftHours };
            foreach (KeyValuePair<Aspect, float> pair in Chances) { copy.Chances[pair.Key] = pair.Value; }
            foreach (KeyValuePair<Aspect, float> pair in Loot) { copy.Loot[pair.Key] = pair.Value; }
            foreach (KeyValuePair<Aspect, Dictionary<string, float>> pair in Power)
            {
                copy.Power[pair.Key] = new Dictionary<string, float>(pair.Value);
            }
            foreach (KeyValuePair<Aspect, Dictionary<string, float[]>> pair in Lists)
            {
                copy.Lists[pair.Key] = new Dictionary<string, float[]>(pair.Value);
            }
            foreach (KeyValuePair<string, BossAspectRule> pair in Bosses) { copy.Bosses[pair.Key] = pair.Value.Clone(); }
            return copy;
        }
    }

    /// <summary>One `per boss` entry: the boss's own rotation, if it narrows it, and the creatures Summoner calls.</summary>
    public sealed class BossAspectRule
    {
        /// <summary>The aspects this boss may roll; null means every aspect. `none` is governed by its weight alone.</summary>
        public List<Aspect>? Rotation;

        public List<string> Summons = new List<string>();

        public BossAspectRule Clone() => new BossAspectRule
        {
            Rotation = Rotation == null ? null : new List<Aspect>(Rotation), Summons = new List<string>(Summons),
        };
    }
}
