// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Native compatibility facade: the suite's existing CS1 owns the custom skill and level publication.
using UnityEngine;
namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    public static class HusbandrySkill
    {
        public const float MaxLevel = 100f;
        public static bool Active => NativeHusbandry.Active;
        public static global::Skills.SkillType Type => Plugin.Husbandry!.Type;
        public static float Local() => Plugin.Husbandry?.Local() ?? 0f;
        public static float Of(Player player) => Plugin.Husbandry?.Of(player) ?? 0f;
        public static float Share(float percentAt100, float level) =>
            (float)ExtraPotions.Shepherd.Breeding.HusbandryShare.Share(percentAt100, level);
        public static bool Reaches(float level, float milestone) => milestone <= MaxLevel && level >= milestone;
    }
    public static class Keys
    {
        public const string CalmBrokenBy = "shepherd.calm_broken_by";
        public const string CalmBrokenAt = "shepherd.calm_broken_at";
        public const string ContentUntil = ExtraPotions.Shepherd.Breeding.BreedingKeys.ContentUntil;
        public const string RpcPet = "shepherd.pet";
    }
}
