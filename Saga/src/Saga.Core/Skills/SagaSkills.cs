// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the six skills Saga owns (design 1.1 B, C, D, E, F, G and 1.3), their CS1 identities and numbers.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Saga.Integration;

namespace ExtraPotions.Saga.Skills
{
    public enum SagaSkill { Defense, Sneak, WoodCutting, Pickaxes, Foraging, Fishing }

    /// <summary>
    /// One Saga skill. Custom skills (Defense, Foraging) register through Crucible's skill registry CS1 under a
    /// product-independent Meadworks identity and import the GrindstoneSkills level once per character; the game's own
    /// skills (Sneak, WoodCutting, Pickaxes, Fishing) keep their numbers, so nothing is imported for them.
    /// </summary>
    public sealed class SkillInfo
    {
        internal SkillInfo(SagaSkill skill, string name, int gameType, string identity, string importFrom, SagaModule module, string icon, string description)
        {
            Skill = skill;
            Name = name;
            GameType = gameType;
            Identity = identity;
            ImportFrom = importFrom;
            Module = module;
            IconResource = icon;
            Description = description;
        }

        public SagaSkill Skill { get; }

        /// <summary>English name; CS1 derives the console command name from it.</summary>
        public string Name { get; }

        /// <summary>The game's <c>Skills.SkillType</c> number (pinned for custom skills, checked against CS1 at run time).</summary>
        public int GameType { get; }

        public bool IsCustom => Identity.Length > 0;

        /// <summary>CS1 identity of a custom skill; empty for the game's skills.</summary>
        public string Identity { get; }

        /// <summary>CS1 default publish key (<c>&lt;identity&gt;_level</c>) on the player ZDO; empty for the game's skills.</summary>
        public string PublishKey => IsCustom ? Identity + "_level" : "";

        /// <summary>GrindstoneSkills identity imported once per character; empty when nothing is imported.</summary>
        public string ImportFrom { get; }

        public int ImportType => ImportFrom.Length > 0 ? SkillHash.CustomType(ImportFrom) : 0;

        /// <summary>C05 key of a custom skill's name (CS1 <c>textKey</c>; the description key appends <c>_description</c>); empty otherwise.</summary>
        public string TextKey => IsCustom ? "skill_" + Skill.ToString().ToLowerInvariant() : "";

        /// <summary>The module whose master switch and step-aside turn this skill's perks off.</summary>
        public SagaModule Module { get; }

        /// <summary>Embedded icon resource name in Saga.dll; empty when the game's icon stays.</summary>
        public string IconResource { get; }

        public string Description { get; }

        public override string ToString() => Name + " (" + GameType + ")";
    }

    public static class SagaSkills
    {
        public const int DefenseType = 210799442;
        public const int ForagingType = 1181927485;

        private static readonly SkillInfo[] skills =
        {
            new SkillInfo(SagaSkill.Defense, "Defense", DefenseType, "meadworks_defense", "grindstone_defense", SagaModule.Defense, "Saga.skill_defense.png",
                "Toughness: more health and food health, less damage taken, recovery out of combat, poise, a wider parry window and cheaper blocks and dodges. Trained by blocking and by taking hits."),
            new SkillInfo(SagaSkill.Sneak, "Sneak", 101, "", "", SagaModule.Sneak, "",
                "Moving unseen: faster crouched movement and lower visibility as the skill grows. Trained by sneaking and by backstabs."),
            new SkillInfo(SagaSkill.WoodCutting, "Woodcutting", 13, "", "", SagaModule.Woodcutting, "Saga.skill_woodcutting.png",
                "Felling and chopping: stronger falls, safer logs, domino falls, clean fells and splits, replanting and finds in felled trees."),
            new SkillInfo(SagaSkill.Pickaxes, "Pickaxes", 12, "", "", SagaModule.Mining, "Saga.skill_pickaxes.png",
                "Mining: seams and clean strikes, starred veins, extra ore, splash damage and finds in the rock."),
            new SkillInfo(SagaSkill.Foraging, "Foraging", ForagingType, "meadworks_foraging", "grindstone_foraging", SagaModule.Foraging, "Saga.skill_foraging.png",
                "Gathering wild plants: extra yield, best-time picks and sweep picking around the plant. Trained by every wild pick."),
            new SkillInfo(SagaSkill.Fishing, "Fishing", 104, "", "", SagaModule.Fishing, "Saga.skill_fishing.png",
                "Fishing: the line fight, better bites, big and legendary fish, bonus catches, bait saving and longer casts."),
        };

        /// <summary>The six skills in design order.</summary>
        public static IReadOnlyList<SkillInfo> All { get; } = Array.AsReadOnly(skills);

        public static SkillInfo Get(SagaSkill skill) => skills.FirstOrDefault(s => s.Skill == skill) ?? throw new ArgumentException("Unknown skill.", nameof(skill));

        public static bool TryGetByType(int gameType, out SkillInfo info)
        {
            info = skills.FirstOrDefault(s => s.GameType == gameType)!;
            return info != null;
        }
    }
}
