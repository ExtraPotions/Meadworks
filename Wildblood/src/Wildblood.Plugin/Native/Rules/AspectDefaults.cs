// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
// Changed by expDARE, 2026: Kall Fimbulbringer summons (FrozenKing, FrozenKing_p3).
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The boss-aspect defaults expressed in code: the baseline a parsed `aspects:` block overlays onto, and the value a
    /// single missing field falls back to. The written rule file carries the same numbers with their comments. Every
    /// number here that did not come with the request is a judgement call and says so in `features/boss-aspects.md`.
    /// </summary>
    internal static class AspectDefaults
    {
        public static AspectRules Build()
        {
            AspectRules rules = new AspectRules { Enabled = true, ShiftSeconds = 15f }; // the user's 15 s, 2026-10-04
            rules.Chances[Aspect.None] = 42f; // about one fight in five is the boss as the game ships it
            foreach (Aspect aspect in AspectCatalog.InOrder)
            {
                rules.Chances[aspect] = 10f;
                rules.Power[aspect] = new Dictionary<string, float>(FieldsOf(aspect));
            }
            foreach (KeyValuePair<Aspect, float> pair in LootTable)
            {
                rules.Loot[pair.Key] = pair.Value;
            }
            SeedBosses(rules);
            return rules;
        }

        // The per-boss entries: what each vanilla boss summons, and the rotations narrowed for other mods' bosses.
        private static void SeedBosses(AspectRules rules)
        {
            foreach (KeyValuePair<string, string[]> pair in SummonTable)
            {
                rules.Bosses[pair.Key] = new BossAspectRule { Summons = new List<string>(pair.Value) };
            }
            foreach (KeyValuePair<string, Aspect[]> pair in LeftOutTable)
            {
                List<Aspect> rotation = new List<Aspect>(AspectCatalog.InOrder);
                rotation.RemoveAll(aspect => System.Array.IndexOf(pair.Value, aspect) >= 0);
                rules.Bosses[pair.Key] = new BossAspectRule { Rotation = rotation };
            }
        }

        public static float Power(Aspect aspect, string field) =>
            FieldsOf(aspect).TryGetValue(field, out float value) ? value : 0f;

        private static Dictionary<string, float> FieldsOf(Aspect aspect) =>
            PowerTable.TryGetValue(aspect, out Dictionary<string, float> fields) ? fields : new Dictionary<string, float>();

        /// <summary>A list field's default; shared, so callers read it and never change it.</summary>
        public static float[] List(Aspect aspect, string field) =>
            ListTable.TryGetValue(aspect, out Dictionary<string, float[]> fields)
                && fields.TryGetValue(field, out float[] value) ? value : new float[0];

        // Gentlest first. Twin pays 1.0 per boss because both twins drop full loot: the fight already pays double.
        private static readonly Dictionary<Aspect, float> LootTable = new Dictionary<Aspect, float>
        {
            [Aspect.None] = 1f, [Aspect.Twin] = 1f, [Aspect.Shielded] = 1.1f, [Aspect.Enraged] = 1.2f,
            [Aspect.Elementalist] = 1.2f, [Aspect.Mending] = 1.3f, [Aspect.Phantom] = 1.3f,
            [Aspect.Reflective] = 1.4f, [Aspect.Summoner] = 1.5f,
            [Aspect.Stormbound] = 1.2f, [Aspect.Colossal] = 1.2f, [Aspect.Adaptive] = 1.3f, [Aspect.Fixated] = 1.3f,
            [Aspect.Gravitic] = 1.3f, [Aspect.Tethered] = 1f, [Aspect.Bountiful] = 2f, [Aspect.Portalbound] = 1.2f,
            [Aspect.Nightfall] = 1.3f, [Aspect.Brutal] = 1.2f, [Aspect.Echoing] = 1.3f,
        };

        private static readonly Dictionary<Aspect, Dictionary<string, float>> PowerTable =
            new Dictionary<Aspect, Dictionary<string, float>>
            {
                [Aspect.Reflective] = new Dictionary<string, float> { [Fields.Reflect] = 15f },
                [Aspect.Shielded] = new Dictionary<string, float> { [Fields.ArrowReduction] = 30f },
                [Aspect.Mending] = new Dictionary<string, float> { [Fields.Regen] = 0.2f },
                [Aspect.Summoner] = new Dictionary<string, float>
                    { [Fields.Every] = 33f, [Fields.Count] = 2f, [Fields.Stars] = 2f },
                [Aspect.Elementalist] = new Dictionary<string, float> { [Fields.ElementalBonus] = 20f },
                [Aspect.Enraged] = new Dictionary<string, float> { [Fields.PhysicalBonus] = 20f },
                [Aspect.Twin] = new Dictionary<string, float>
                    { [Fields.LessHealth] = 25f, [Fields.LessDamage] = 25f },
                [Aspect.Phantom] = new Dictionary<string, float>
                    { [Fields.PerPlayer] = 1f, [Fields.HealthPerTier] = 25f, [Fields.LessDamage] = 50f },
                [Aspect.Adaptive] = new Dictionary<string, float> { [Fields.Resist] = 50f, [Fields.Window] = 15f },
                [Aspect.Fixated] = new Dictionary<string, float>
                    { [Fields.MarkedBonus] = 50f, [Fields.OthersLess] = 30f, [Fields.Every] = 30f },
                [Aspect.Stormbound] = new Dictionary<string, float>
                    { [Fields.Every] = 20f, [Fields.TellTime] = 2f, [Fields.Radius] = 2.5f, [Fields.Damage] = 8f,
                      [Fields.Range] = 40f },
                [Aspect.Gravitic] = new Dictionary<string, float>
                    { [Fields.Every] = 20f, [Fields.Range] = 30f, [Fields.PullTime] = 1.5f, [Fields.PullSpeed] = 6f,
                      [Fields.SlamRadius] = 6f, [Fields.SlamDamage] = 10f },
                [Aspect.Colossal] = new Dictionary<string, float>
                    { [Fields.Bigger] = 40f, [Fields.MoreHealth] = 15f, [Fields.Slower] = 15f,
                      [Fields.ShockwaveRadius] = 8f },
                [Aspect.Tethered] = new Dictionary<string, float>
                    { [Fields.LessHealth] = 25f, [Fields.LessDamage] = 25f, [Fields.AttackSpeed] = 50f,
                      [Fields.Armour] = 50f, [Fields.FullGap] = 50f },
                [Aspect.Bountiful] = new Dictionary<string, float> { [Fields.ExtraAspects] = 2f },
                [Aspect.Portalbound] = new Dictionary<string, float>
                    { [Fields.MinHeight] = 5f, [Fields.Clearance] = 2f, [Fields.Range] = 20f },
                [Aspect.Nightfall] = new Dictionary<string, float>
                    { [Fields.Range] = 60f, [Fields.Every] = 18f, [Fields.EveryMax] = 28f, [Fields.Life] = 10f,
                      [Fields.FormTime] = 1.5f, [Fields.TornadoSpeed] = 40f, [Fields.Damage] = 25f, [Fields.TopWidth] = 9f,
                      [Fields.BaseWidth] = 1.5f, [Fields.Height] = 14f, [Fields.TossDistance] = 10f },
                [Aspect.Brutal] = new Dictionary<string, float> { [Fields.Launch] = 20f, [Fields.Lift] = 3f },
                [Aspect.Echoing] = new Dictionary<string, float> { [Fields.Delay] = 15f }, // the user's 15 s, 2026-10-06
            };

        // Phantom splits as its health falls past each mark, in percent of its maximum health left (decided 2026-09-26).
        private static readonly Dictionary<Aspect, Dictionary<string, float[]>> ListTable =
            new Dictionary<Aspect, Dictionary<string, float[]>>
            {
                [Aspect.Phantom] = new Dictionary<string, float[]> { [Fields.SplitAt] = new[] { 66f, 33f } },
            };

        // Each vanilla boss calls creatures of its own biome, by prefab name (checked against the game's prefabs).
        private static readonly Dictionary<string, string[]> SummonTable = new Dictionary<string, string[]>
        {
            ["Eikthyr"] = new[] { "Boar", "Neck" },
            ["gd_king"] = new[] { "Greydwarf_Elite", "Greydwarf_Shaman" },
            ["Bonemass"] = new[] { "Draugr_Elite", "BlobElite" },
            ["Dragon"] = new[] { "Hatchling" },
            ["GoblinKing"] = new[] { "GoblinBrute", "GoblinShaman" },
            ["SeekerQueen"] = new[] { "SeekerBrute", "Seeker" },
            ["Fader"] = new[] { "Charred_Melee", "Charred_Archer" },
            // Kall Fimbulbringer calls the Jotun army (Krigen, Hexen). Each phase is its own prefab and rolls on its own.
            ["FrozenKing"] = new[] { "JotunWarrior", "JotunWitch" },
            ["FrozenKing_p3"] = new[] { "JotunWarrior", "JotunWitch" },
        };

        // Bosses from other mods whose fight an aspect would break, by prefab name (only the name crosses; without that
        // mod the entry is never matched). Elite Creatures Pack's kraken holds one ship with one health bar: a Twin or a Tethered pair puts
        // two of it on the same hull, and Phantom copies would swarm the deck. Decided with the user, 2026-09-28.
        // Brutal throws no one standing on a deck, so it would be a plain fight paying 1.2x (2026-10-04).
        private static readonly Dictionary<string, Aspect[]> LeftOutTable = new Dictionary<string, Aspect[]>
        {
            ["ECP_Kraken"] = new[] { Aspect.Twin, Aspect.Phantom, Aspect.Tethered, Aspect.Brutal, Aspect.Echoing },
        };
    }
}
