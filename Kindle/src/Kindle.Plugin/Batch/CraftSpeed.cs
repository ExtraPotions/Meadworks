// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from OpenKeep 3.0.0 ad278ecf323901c3645eb1f9f6fac6833a0f8cb1,
// OpenKeep/OpenKeep/src/Batch/CraftSpeed.cs. Copyright (C) 2026 Gerald Glasgow.
// GNU GPL version 3 or later; full notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle namespace, effective scoped settings, owned lifecycle and Instant Crafting.
using ExtraPotions.Kindle.Config;

namespace ExtraPotions.Kindle.Runtime
{
    /// <summary>
    /// Craft Speed: the game's craft bar durations (a craft, a multi-craft, an upgrader's base and per level time) are
    /// remembered at InventoryGui.Awake and divided by the setting before every UpdateRecipe, the one place the game
    /// reads them, so the bar, the craft and every mod reading the fields see the same time. The game's skill
    /// reduction is applied by the game on top. Instant Crafting sets every duration to 0: the game finishes a craft (a
    /// single one or a whole batch, which is one multi-craft) on the first update after Craft is pressed.
    /// </summary>
    public static class CraftSpeed
    {
        private static float craft = 2f;
        private static float multi = 6f;
        private static float upgrader = 8f;
        private static float perLevel = 1f;

        public static void Remember(InventoryGui gui)
        {
            craft = gui.m_craftDuration;
            multi = gui.m_multiCraftDuration;
            upgrader = gui.m_upgraderDuration;
            perLevel = gui.m_upgraderDurationPerLevel;
        }

        public static void Apply(InventoryGui gui)
        {
            float speed = KindleBatch.Config.CraftSpeed;
            bool instant = KindleBatch.Config.InstantCrafting;
            gui.m_craftDuration = KindleCheats.CraftDuration(craft, speed, instant);
            gui.m_multiCraftDuration = KindleCheats.CraftDuration(multi, speed, instant);
            gui.m_upgraderDuration = KindleCheats.CraftDuration(upgrader, speed, instant);
            gui.m_upgraderDurationPerLevel = KindleCheats.CraftDuration(perLevel, speed, instant);
        }
        internal static void ApplySafe(InventoryGui gui)
        {
            gui.m_craftDuration=craft;gui.m_multiCraftDuration=multi;
            gui.m_upgraderDuration=upgrader;gui.m_upgraderDurationPerLevel=perLevel;
        }
    }
}
