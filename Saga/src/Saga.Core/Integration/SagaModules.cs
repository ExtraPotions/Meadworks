// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the feature modules and independent recovery source-ownership gates, each with its own master switch and step-aside rule.
using System;

namespace ExtraPotions.Saga.Integration
{
    /// <summary>The parts of Saga that switch off and step aside on their own (design 1.2 and 10.1).</summary>
    public enum SagaModule { Powers, Defense, Sneak, Woodcutting, Mining, Foraging, Fishing, Progression, CatchUp, WeaponMastery, Running, Jumping, Swimming, Riding, Dodging, Crafting }

    public static class SagaModules
    {
        /// <summary>Crucible C09 module id (lower case): <c>powers</c>, <c>defense</c>, and so on.</summary>
        public static string Id(SagaModule module)
        {
            if (!Enum.IsDefined(typeof(SagaModule), module)) throw new ArgumentException("Unknown module.", nameof(module));
            return module.ToString().ToLowerInvariant();
        }
    }
}
