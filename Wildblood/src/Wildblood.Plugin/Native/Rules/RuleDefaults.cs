// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Flame visuals adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The specification defaults expressed in code, used two ways: as the baseline a parsed file overlays onto, and
    /// as the value a single missing field falls back to. The written file (see <see cref="RuleText"/>) carries the
    /// same numbers with their comments; these are the safety net when a value is edited away or the file cannot load.
    /// </summary>
    internal static class RuleDefaults
    {
        /// <summary>A full defaults block: every field populated, ready for a file to overlay its own values onto.</summary>
        public static BiomeRules Baseline()
        {
            BiomeRules rules = new BiomeRules
            {
                // `star chances` has no global default - it is per-biome only. This is the fallback an unlisted biome
                // uses (the Meadows row); the parser overwrites it from the file's own Meadows entry when there is one.
                StarChances = new[] { 73f, 10f, 10f, 5f, 1f, 1f },
                Star = BaselineStarPower(),
                LargeStarPower = 1f,
                MutationChance = new[] { 2.5f, 3.5f, 5f, 6f, 7.5f, 10f },
            };
            rules.MutationChances[Mutation.Devouring] = new[] { 0.6f, 0.9f, 1.2f, 1.5f, 1.8f, 2.4f };
            rules.MutationChances[Mutation.Gilded] = new[] { 0.2f, 0.25f, 0.35f, 0.45f, 0.55f, 0.7f };
            foreach (Mutation mutation in MutationCatalog.InOrder)
            {
                rules.MutationPower[mutation] = new Dictionary<string, float>(Power(mutation));
            }
            return rules;
        }

        /// <summary>A whole rule set of nothing but defaults, for the rare case the file cannot be written or read.</summary>
        public static RuleSet BuildDefault() =>
            new RuleSet { Defaults = Baseline(), Boss = BaselineBoss(), Respawn = new RespawnRules() };

        /// <summary>
        /// The boss table's defaults. Conservative: nine bosses in ten stay plain, because a boss is a set-piece a
        /// group has prepared for and a surprise five-star Bonemass is a wasted evening. Health and damage climb
        /// harder per star than a creature's, size climbs less - a boss already fills its arena and one scaled much
        /// past that clips through the terrain it stands on.
        /// </summary>
        public static BossRules BaselineBoss()
        {
            return new BossRules
            {
                Enabled = true,
                StarChances = new[] { 90f, 6f, 3f, 1f },
                Star = new StarPower
                {
                    Growth = new[] { 0f, 0.05f, 0.10f, 0.15f, 0.20f, 0.20f },
                    Hp = new[] { 1f, 1.5f, 2.25f, 3.4f, 5.1f, 7.6f },
                    Attack = new[] { 1f, 1.25f, 1.55f, 1.9f, 2.3f, 2.75f },
                    SwingSpeed = new[] { 1f },
                    Speed = new[] { 1f }, // no Drops: a boss's stars never multiply its drops (StarDrops)
                },
                Aspects = AspectDefaults.Build(),
            };
        }

        public static float Power(Mutation mutation, string field)
        {
            return Power(mutation).TryGetValue(field, out float value) ? value : 1f;
        }

        /// <summary>The default vanilla effect prefab a mutation's named field falls back to when the file omits it.</summary>
        public static string Prefab(Mutation mutation, string field)
        {
            return PrefabTable.TryGetValue(mutation, out Dictionary<string, string> fields)
                && fields.TryGetValue(field, out string value) ? value : "";
        }

        internal static StarPower BaselineStarPower()
        {
            return new StarPower
            {
                // 6-8 stars (Extreme only) continue each line by its 5th star's step; the speeds by +0.03 so
                // nothing outruns a player (features/difficulty.md section 6).
                Growth = new[] { 0.06f, 0.10f, 0.15f, 0.20f, 0.25f, 0.30f, 0.35f, 0.40f, 0.45f },
                Hp = new[] { 1f, 1.4f, 1.95f, 2.6f, 3.3f, 4.0f, 4.7f, 5.4f, 6.1f },
                Attack = new[] { 1f, 1.15f, 1.3f, 1.5f, 1.75f, 2f, 2.25f, 2.5f, 2.75f },
                SwingSpeed = new[] { 1f, 1.02f, 1.05f, 1.08f, 1.12f, 1.16f, 1.19f, 1.22f, 1.25f },
                Speed = new[] { 1f, 1f, 1.03f, 1.06f, 1.1f, 1.15f, 1.18f, 1.21f, 1.24f },
                Drops = new[] { 1f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f },
            };
        }

        private static readonly Dictionary<Mutation, Dictionary<string, float>> Table = BuildTable();

        // The vanilla effect prefabs each visible mutation clones and plays, each checked against the game's own prefab
        // list; the resolver (EffectResolver) falls back to the nearest effect by keyword if a name is missing, so a
        // wrong string self-heals rather than leaving an invisible hazard. Every one is a rule-file field a server can edit.
        private static readonly Dictionary<Mutation, Dictionary<string, string>> PrefabTable =
            new Dictionary<Mutation, Dictionary<string, string>>
            {
                [Mutation.Bloated] = new Dictionary<string, string>
                {
                    [Fields.BlastEffect] = "fx_dynamite_explosion", [Fields.BlastSound] = "sfx_bombdynamite_explosion",
                    [Fields.WarningEffect] = "vfx_Smoked",
                },
                [Mutation.Miasmic] = new Dictionary<string, string>
                    { [Fields.CloudEffect] = "vfx_blob_death", [Fields.BodyEffect] = "vfx_blob_death" },
                [Mutation.Gilded] = new Dictionary<string, string>
                    { [Fields.BonusItem] = "Coins", [Fields.GlitterEffect] = "vfx_Potion_stamina_medium" },
                [Mutation.Blinking] = new Dictionary<string, string>
                    {
                        [Fields.BlinkEffect] = "vfx_ghost_spawn", [Fields.TellEffect] = "vfx_WishbonePing",
                        [Fields.TellSound] = "sfx_WishbonePing_near",
                    },
                [Mutation.Screecher] = new Dictionary<string, string> { [Fields.ShriekSound] = "sfx_fallenvalkyrie_screech" },
                [Mutation.Frostbound] = new Dictionary<string, string>
                    { [Fields.AuraEffect] = "vfx_Frost", [Fields.TrailEffect] = "vfx_blob_frost_death" },
                [Mutation.Mudbound] = new Dictionary<string, string> { [Fields.TrailEffect] = "vfx_blobtar_death" },
                [Mutation.Flamebound] = new Dictionary<string, string> { [Fields.TrailEffect] = "vfx_blobtar_death" },
                [Mutation.Howling] = new Dictionary<string, string> { [Fields.HowlSound] = "sfx_fenring_howl" },
                [Mutation.Cloning] = new Dictionary<string, string>
                    {
                        [Fields.RevealEffect] = "vfx_spawn_small", [Fields.RevealSound] = "sfx_spawn",
                        [Fields.VanishEffect] = "vfx_odin_despawn",
                    },
            };

        private static Dictionary<string, float> Power(Mutation mutation)
        {
            return Table.TryGetValue(mutation, out Dictionary<string, float> fields)
                ? fields : new Dictionary<string, float>();
        }

        private static Dictionary<Mutation, Dictionary<string, float>> BuildTable()
        {
            Dictionary<Mutation, Dictionary<string, float>> table = new Dictionary<Mutation, Dictionary<string, float>>
            {
                [Mutation.Mad] = new Dictionary<string, float>
                    { [Fields.Move] = 1.6f, [Fields.AttackSpeed] = 1.5f, [Fields.Health] = 0.5f },
                [Mutation.Bloated] = new Dictionary<string, float>
                    { [Fields.Health] = 2.0f, [Fields.Delay] = 1.5f, [Fields.Damage] = 40f, [Fields.Radius] = 4f },
                [Mutation.Cloaked] = new Dictionary<string, float>
                    { [Fields.RevealDistance] = 10f, [Fields.FadeTime] = 0.5f, [Fields.FadeMargin] = 1f },
                [Mutation.Splintering] = new Dictionary<string, float>
                    { [Fields.Damage] = 0.6f, [Fields.MaxGenerations] = 0f, [Fields.MaxDescendants] = 0f },
                [Mutation.Leeching] = new Dictionary<string, float>
                    { [Fields.Regen] = 0.5f, [Fields.Lifesteal] = 10f, [Fields.RegenCap] = 20f,
                      [Fields.CombatCooldown] = 5f },
                [Mutation.Warding] = new Dictionary<string, float>
                    { [Fields.Reflect] = 30f, [Fields.Knockback] = 4f, [Fields.MaxReflect] = 7.5f },
            };
            AddLaterMutations(table);
            return table;
        }

        private static void AddLaterMutations(Dictionary<Mutation, Dictionary<string, float>> table)
        {
            table[Mutation.Plated] = new Dictionary<string, float>
                { [Fields.Armour] = 40f, [Fields.Damage] = 60f, [Fields.MaxReduction] = 55f };
            table[Mutation.Miasmic] = new Dictionary<string, float>
                { [Fields.CloudLife] = 6f, [Fields.CloudDamage] = 5f, [Fields.CloudsPerSecond] = 1f,
                  [Fields.CloudRadius] = 4f };
            table[Mutation.Devouring] = new Dictionary<string, float>
                { [Fields.Move] = 1f, [Fields.AbsorbHealth] = 50f, [Fields.AbsorbDamage] = 25f,
                  [Fields.SlowPer100Health] = 2f, [Fields.PlayerThreshold] = 0.333f, [Fields.DevourCooldown] = 60f,
                  [Fields.MaxPreyHealth] = 100f, [Fields.MinMeals] = 1f };
            table[Mutation.Thieving] = new Dictionary<string, float> { [Fields.MaxItems] = 1f };
            table[Mutation.Gilded] = new Dictionary<string, float>
                { [Fields.Loot] = 3f, [Fields.BonusAmount] = 20f, [Fields.FleeDistance] = 30f };
            table[Mutation.Blinking] = new Dictionary<string, float>
                { [Fields.Health] = 0.75f, [Fields.Every] = 30f, [Fields.Distance] = 4f, [Fields.TellTime] = 0.5f };
            table[Mutation.Relentless] = new Dictionary<string, float> { [Fields.ChaseDistance] = 150f };
            table[Mutation.Screecher] = new Dictionary<string, float>
                { [Fields.Threshold] = 15f, [Fields.Radius] = 20f, [Fields.MuteTime] = 4f, [Fields.Cooldown] = 15f };
            AddWinterMutations(table);
        }

        private static void AddWinterMutations(Dictionary<Mutation, Dictionary<string, float>> table)
        {
            table[Mutation.Frostbound] = new Dictionary<string, float>
                { [Fields.AuraRadius] = 6f, [Fields.StaminaRegen] = 50f, [Fields.FrostHeal] = 100f, [Fields.TrailLife] = 10f,
                  [Fields.PatchRadius] = 1.5f, [Fields.PatchSpacing] = 1.5f, [Fields.Grip] = 5f, [Fields.Slow] = 15f };
            table[Mutation.Mudbound] = new Dictionary<string, float>
                { [Fields.TrailLife] = 10f, [Fields.PatchRadius] = 1.5f, [Fields.PatchSpacing] = 1.5f, [Fields.Slow] = 40f };
            table[Mutation.Corrodent] = new Dictionary<string, float> { [Fields.Durability] = 3f };
            table[Mutation.Cloning] = new Dictionary<string, float>
                { [Fields.Times] = 1f, [Fields.Range] = 12f, [Fields.DecoyLife] = 20f, [Fields.Cooldown] = 30f };
            // EliteCreaturesReborn 3.22, behavior only: burning ground, 15% of armour ignored, up to 4 called, a 1 s root.
            table[Mutation.Flamebound] = new Dictionary<string, float>
                { [Fields.TrailLife] = ExtraPotions.Wildblood.Visuals.FlameVisualPolicy.DefaultLife, [Fields.PatchRadius] = ExtraPotions.Wildblood.Visuals.FlameVisualPolicy.DefaultRadius, [Fields.PatchSpacing] = 1.5f, [Fields.Slow] = 0f,
                  [Fields.Damage] = 6f };
            table[Mutation.Piercing] = new Dictionary<string, float> { [Fields.IgnoreArmour] = 15f };
            table[Mutation.Howling] = new Dictionary<string, float>
                { [Fields.Count] = 4f, [Fields.Radius] = 30f };
            table[Mutation.Binding] = new Dictionary<string, float> { [Fields.RootTime] = 1f, [Fields.Cooldown] = 5f };
        }
    }
}
