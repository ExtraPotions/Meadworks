// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: multi-user chests (several players use one open chest), kept free of the game for tests.
// Behavior inspired by MultiUserChest (MSchmoecker, MIT); no code reused.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Burrow
{
    /// <summary>What a chest another player has open does for the local player.</summary>
    public enum MultiUserOutcome
    {
        /// <summary>The Shared Chests setting decides (Off, View or Full).</summary>
        Configured,
        /// <summary>Every player may use the chest at once; changes go to the chest's owning client (Full).</summary>
        AllUsers,
        /// <summary>MultiUserChest is loaded and owns concurrent chest access; Burrow behaves as Shared Chests Off.</summary>
        SteppedAside,
        /// <summary>The container carries another mod's <c>MUC_Ignore</c> flag; Burrow behaves as Shared Chests Off for it.</summary>
        Ignored,
    }

    /// <summary>
    /// The Multi-User Chests switch. On, a chest another player has open is used through its owning client, so no
    /// change is ever applied by two clients to the same inventory. Steps aside while MultiUserChest is loaded.
    /// </summary>
    public static class MultiUserChests
    {
        public const string ModuleId = "multi_user_chests";
        public const string TextKey = "step_aside_" + ModuleId;

        /// <summary>MultiUserChest 0.6.2 (MSchmoecker), GUID from its BepInPlugin attribute.</summary>
        public static StowRowSource Source { get; } = new StowRowSource("com.maxsch.valheim.MultiUserChest", "MultiUserChest");

        /// <summary>
        /// The ZDO bool other mods set on a container's ZNetView to keep it out of multi-user chest handling, as
        /// MultiUserChest 0.6.0+ documents it (CHANGELOG: "MUC_Ignore ZDO bool flag").
        /// </summary>
        public const string IgnoreKey = "MUC_Ignore";

        public static MultiUserOutcome Resolve(bool enabled, bool steppedAside) => Resolve(enabled, steppedAside, false);

        /// <param name="ignored">The container's <see cref="IgnoreKey"/> flag; any shared handling of it is off.</param>
        public static MultiUserOutcome Resolve(bool enabled, bool steppedAside, bool ignored)
        {
            if (steppedAside) return MultiUserOutcome.SteppedAside;
            if (ignored) return MultiUserOutcome.Ignored;
            return enabled ? MultiUserOutcome.AllUsers : MultiUserOutcome.Configured;
        }

        /// <summary>MultiUserChest when its GUID is among the loaded plugin GUIDs (ordinal, as BepInEx keys them), else null.</summary>
        public static StowRowSource? StepAsideMatch(IEnumerable<string> loadedGuids)
        {
            if (loadedGuids == null) throw new ArgumentNullException(nameof(loadedGuids));
            foreach (string guid in loadedGuids)
                if (string.Equals(guid, Source.Guid, StringComparison.Ordinal)) return Source;
            return null;
        }
    }

    /// <summary>
    /// The owning client's admission of a shared chest request. A request is built from the chest view the requester
    /// had when it clicked, which another player may have changed since; the owner applies requests one at a time on
    /// its live chest and grants only what the slot holds now. A unit is therefore handed out at most once, whatever
    /// the arrival order.
    /// </summary>
    public static class SharedChestArbiter
    {
        /// <summary>A single take: all requested units when the slot still holds that kind with enough units, else none.</summary>
        public static int ExactTake(string? slotName, int slotStack, string requestedName, int requestedAmount)
        {
            if (!SameKind(slotName, requestedName) || requestedAmount <= 0 || slotStack < requestedAmount)
                return 0;
            return requestedAmount;
        }

        /// <summary>One entry of a take all: at most what the slot still holds of that kind.</summary>
        public static int PartialTake(string? slotName, int slotStack, string requestedName, int requestedAmount)
        {
            if (!SameKind(slotName, requestedName) || requestedAmount <= 0 || slotStack <= 0)
                return 0;
            return Math.Min(requestedAmount, slotStack);
        }

        /// <summary>The slot holds an item, and it is the kind the requester saw (null: not checked).</summary>
        public static bool SameKind(string? slotName, string? expectedName)
        {
            return slotName != null && (expectedName == null || string.Equals(slotName, expectedName, StringComparison.Ordinal));
        }
    }
}
