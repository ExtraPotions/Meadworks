// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// Section 5: the Kraken. Whether it may appear and how rarely (a chance each time a zone's spawner rolls while a
    /// ship with a crew crosses the deep ocean, how often a zone rolls, how deep the water must be), how it hunts (its
    /// speed, how far it notices a ship), and the numbers of the fight: its health, how often it attacks and how many
    /// attacks make a phase, the tentacle slams, the bite, the ink and the ship smashes; and the bite of the shield made from
    /// its beak. Synced; read where they apply, so a reload takes effect.
    /// </summary>
    public static class KrakenSettings
    {
        public const string Section = "5 - Kraken";

        private static GatedEntry<bool> enabled = null!;
        private static GatedEntry<float> chance = null!, interval = null!, minDepth = null!;
        private static GatedEntry<float> health = null!, swimSpeed = null!, huntRange = null!;
        private static GatedEntry<float> slamDamage = null!, biteDamage = null!;
        private static GatedEntry<float> inkDamage = null!, inkBlind = null!, inkInterval = null!;
        private static GatedEntry<float> shipDamage = null!, shipHitInterval = null!;
        private static GatedEntry<float> attackGapMin = null!, attackGapMax = null!, gapSolo = null!, gapPair = null!;
        private static GatedEntry<float> tentacleGap = null!;
        private static GatedEntry<int> phaseAttacks = null!;
        private static GatedEntry<float> parryDamage = null!, parryDamagePerLevel = null!;

        /// <summary>Whether new krakens may appear. One already in the world stays.</summary>
        public static bool On => enabled.Value;

        /// <summary>Percent per zone roll.</summary>
        public static float Chance => chance.Value;

        public static float Interval => interval.Value;
        public static float MinDepth => minDepth.Value;
        public static float Health => health.Value;
        public static float SwimSpeed => swimSpeed.Value;
        public static float HuntRange => huntRange.Value;
        public static float SlamDamage => slamDamage.Value;
        public static float BiteDamage => biteDamage.Value;
        public static float InkDamage => inkDamage.Value;
        public static float InkBlind => inkBlind.Value;
        public static float InkInterval => inkInterval.Value;
        public static float ShipDamage => shipDamage.Value;
        public static float ShipHitInterval => shipHitInterval.Value;

        /// <summary>
        /// Seconds from one attack to the next with <paramref name="crew"/> players aboard: a fresh random time between the
        /// two each time, longer with one or two of them than with three or more.
        /// </summary>
        public static float AttackGap(int crew)
        {
            float gap = UnityEngine.Random.Range(attackGapMin.Value, Mathf.Max(attackGapMin.Value, attackGapMax.Value));
            return gap * (crew <= 1 ? gapSolo.Value : crew == 2 ? gapPair.Value : 1f);
        }

        /// <summary>The gap between slams under the ship: the attack gap, quickened.</summary>
        public static float SlamGap(int crew) => AttackGap(crew) * tentacleGap.Value;

        /// <summary>Attacks in each phase before it changes: six tentacle strikes, then as many from the head.</summary>
        public static int PhaseAttacks => phaseAttacks.Value;

        /// <summary>Pierce damage the Kraken shield's beak does to a creature whose blow it parries, at the shield's quality.</summary>
        public static float ParryDamage(int quality) => parryDamage.Value + parryDamagePerLevel.Value * Mathf.Max(0, quality - 1);

        public static void Initialize(NativeConfiguration config)
        {
            BindSpawn(config);
            BindTentacles(config);
            BindHead(config);
            BindLoot(config);
        }

        private static void BindSpawn(NativeConfiguration config)
        {
            enabled = config.Bind(Section, "Enabled", true,
                "Krakens: a boss of the deep ocean that hunts ships. Outrun it under sail, or it grabs the ship from below and "
                + "attacks with six tentacles and then its head. Off: no new krakens; one already in the world stays.");
            chance = config.Bind(Section, "Chance", 0.3f,
                "Percent per zone roll while a ship with a crew crosses deep ocean. At 0.3 a crew sailing the open sea meets one "
                + "about every hour or two.", acceptableValues: Settings.Range(0f, 100f));
            interval = config.Bind(Section, "Interval", 3600f, "Seconds between a zone's rolls.",
                acceptableValues: Settings.Range(60f, 86400f));
            minDepth = config.Bind(Section, "Min Depth", 25f,
                "Meters of water it needs: it only spawns over water this deep and never follows a ship into shallower water.",
                acceptableValues: Settings.Range(6f, 200f));
            health = config.Bind(Section, "Health", 3000f,
                "Its health (Bonemass: 5000, a serpent: 400). The game adds 30% for every other player within 100 m, as for any creature.",
                acceptableValues: Settings.Range(1f, 1000000f));
            swimSpeed = config.Bind(Section, "Swim Speed", 3.5f,
                "Meters a second as it chases. Rowing (about 2) never escapes it; a longship or karve under sail with a fair wind does.",
                acceptableValues: Settings.Range(0.5f, 20f));
            huntRange = config.Bind(Section, "Hunt Range", 90f,
                "Meters at which it notices a ship. It gives up once the ship is further than this.", acceptableValues: Settings.Range(20f, 300f));
        }

        private static void BindTentacles(NativeConfiguration config)
        {
            attackGapMin = config.Bind(Section, "Attack Gap Min", 2.5f,
                "Seconds from one attack to the next at the least. It attacks one thing at a time: a slam, a bite, ink or a smash.",
                acceptableValues: Settings.Range(0.5f, 30f));
            attackGapMax = config.Bind(Section, "Attack Gap Max", 4f,
                "Seconds from one attack to the next at the most; each gap is a random time between the two.",
                acceptableValues: Settings.Range(0.5f, 30f));
            gapSolo = config.Bind(Section, "Gap Factor Solo", 1.1f,
                "The gap between attacks is this many times longer with one player aboard (three or more: the gap as set).",
                acceptableValues: Settings.Range(0.5f, 5f));
            gapPair = config.Bind(Section, "Gap Factor Pair", 1.2f,
                "The gap between attacks is this many times longer with two players aboard.", acceptableValues: Settings.Range(0.5f, 5f));
            tentacleGap = config.Bind(Section, "Tentacle Gap Factor", 0.7f,
                "Under the ship the slams come quicker: the gap between them is this many times the attack gap.",
                acceptableValues: Settings.Range(0.2f, 3f));
            phaseAttacks = config.Bind(Section, "Phase Attacks", 6,
                "Attacks in each phase: this many tentacle slams under the ship, then this many from the head beside it, and round again.",
                acceptableValues: new AcceptableValueRange<int>(1, 30));
            slamDamage = config.Bind(Section, "Slam Damage", 60f, "Blunt damage of a tentacle slamming across the deck.",
                acceptableValues: Settings.Range(0f, 1000f));
            shipDamage = config.Bind(Section, "Ship Damage", 40f,
                "Damage to the ship each time a tentacle smashes it in the head phase (a longship has 1000 health, a karve 500).",
                acceptableValues: Settings.Range(0f, 10000f));
        }

        private static void BindHead(NativeConfiguration config)
        {
            shipHitInterval = config.Bind(Section, "Ship Hit Interval", 10f,
                "Seconds between tentacle smashes on the ship while the head is up; a smash takes the next attack's turn.",
                acceptableValues: Settings.Range(2f, 120f));
            biteDamage = config.Bind(Section, "Bite Damage", 80f, "Pierce damage of its beak, at players near the rail.",
                acceptableValues: Settings.Range(0f, 1000f));
            inkDamage = config.Bind(Section, "Ink Damage", 15f, "Blunt damage of the ink it spits.",
                acceptableValues: Settings.Range(0f, 1000f));
            inkBlind = config.Bind(Section, "Ink Blind", 1.5f,
                "Seconds the ink covers the screen of a player it hits (dodge or block it and it doesn't), before it fades.",
                acceptableValues: Settings.Range(0f, 10f));
            inkInterval = config.Bind(Section, "Ink Interval", 6f, "Seconds between its ink spits at the least.",
                acceptableValues: Settings.Range(1f, 60f));
        }

        private static void BindLoot(NativeConfiguration config)
        {
            parryDamage = config.Bind(Section, "Shield Parry Damage", 60f,
                "Pierce damage a creature takes when you parry its blow with the Kraken shield (made from the kraken's beak), "
                + "at quality 1. Players are never bitten.", acceptableValues: Settings.Range(0f, 10000f));
            parryDamagePerLevel = config.Bind(Section, "Shield Parry Damage Per Level", 10f,
                "Added to the parry damage for each quality level of the shield above the first.",
                acceptableValues: Settings.Range(0f, 1000f));
        }
    }
}
