// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later, adapted from
// EliteCreaturesReborn/Traits/BodySize.cs and Traits/MutationBars.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Wildblood namespace; the capsule measurement as a pure function (the plugin reads the prefab's
// collider and health); kind bars extended with the design B2 defaults for drakes and bats.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Traits
{
    /// <summary>
    /// Which creatures are large (trolls, bears, lox, golems, Fuling berserkers): the movement capsule of the prefab, at the
    /// prefab's own scale, is large when its radius is a metre or more or it is 3.5 m or more long, on a creature with at
    /// least 300 base health. A large kind never rolls or inherits Gilded or Relentless and is never Devouring prey.
    /// </summary>
    public static class BodySize
    {
        public const float LargeRadius = 1f;
        public const float LargeLength = 3.5f;
        public const float MinHealth = 300f;

        // A centimetre of slack, so a capsule built at exactly a metre or 3.5 m counts whatever the float rounding of its scale.
        private const float Slack = 0.01f;

        /// <summary>What a large kind never takes: Gilded and Relentless.</summary>
        public static int LargeBars => Mutations.Bit(Mutation.Gilded) | Mutations.Bit(Mutation.Relentless);

        /// <summary><paramref name="radius"/> and <paramref name="length"/> are already scaled (radius by the larger cross axis).</summary>
        public static bool IsLarge(float radius, float length, float baseHealth) =>
            baseHealth >= MinHealth && (radius >= LargeRadius - Slack || length >= LargeLength - Slack);
    }

    /// <summary>
    /// The mutations a creature never takes, whatever the rule file says: what its body rules out and what its kind does
    /// (design B2): drakes never Cloaked; bats never Mad or Cloaked; deathsquitos never Cloaked.
    /// </summary>
    public static class MutationBars
    {
        private static readonly Dictionary<string, int> KindBars = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Deathsquito"] = Mutations.Bit(Mutation.Cloaked),
            ["Hatchling"] = Mutations.Bit(Mutation.Cloaked),
            ["Bat"] = Mutations.Bit(Mutation.Mad) | Mutations.Bit(Mutation.Cloaked),
        };

        public static int Of(string prefab, bool large) => (large ? BodySize.LargeBars : 0) | OfKind(prefab);

        public static int OfKind(string? prefab) => prefab != null && KindBars.TryGetValue(prefab, out int bars) ? bars : 0;
    }
}
