// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Original native-young meal pacing from the public behavior description; no FeedingTrough source copied.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ExtraPotions.Shepherd.Feeding;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;

namespace ExtraPotions.Shepherd.Runtime.Feeding
{
    public static class YoungFeeding
    {
        public const string LastMealKey = "shepherd.young_last_meal";
        private static ConditionalWeakTable<MonsterAI, Action<ItemDrop>> callbacks = new ConditionalWeakTable<MonsterAI, Action<ItemDrop>>();
        private static readonly List<WeakReference<MonsterAI>> tracked = new List<WeakReference<MonsterAI>>();

        public static bool Supports(MonsterAI ai) => ai != null && ai.m_tamable == null && ai.GetComponent<Tameable>() == null
            && ai.GetComponent<Growup>() != null && ai.m_consumeItems != null && ai.m_consumeItems.Exists(food => food != null);

        public static double Interval(MonsterAI ai)
        {
            Growup grow = ai != null ? ai.GetComponent<Growup>() : null;
            Tameable adult = grow != null && grow.m_grownPrefab != null ? grow.m_grownPrefab.GetComponent<Tameable>() : null;
            return YoungMealPacing.Interval(adult != null ? adult.m_fedDuration : 0);
        }

        public static bool Due(MonsterAI ai) => Supports(ai) && ai.m_nview != null && ai.m_nview.IsValid()
            && YoungMealPacing.Due(Herd.Now.Ticks, ai.m_nview.GetZDO().GetLong(LastMealKey, 0L), Interval(ai));

        public static void Stamp(MonsterAI ai)
        {
            if (!Supports(ai) || ai.m_nview == null || !ai.m_nview.IsValid() || !ai.m_nview.IsOwner()) return;
            long now = Herd.Now.Ticks;
            if (ai.m_nview.GetZDO().GetLong(LastMealKey, 0L) != now) ai.m_nview.GetZDO().Set(LastMealKey, now);
        }

        internal static void Track(MonsterAI ai)
        {
            if (!Supports(ai) || callbacks.TryGetValue(ai, out _)) return;
            Action<ItemDrop> callback = food => { if (food != null) NativeFeeding.Run("young meal stamp", () => Stamp(ai)); };
            callbacks.Add(ai, callback); ai.m_onConsumedItem += callback;
            tracked.RemoveAll(reference => !reference.TryGetTarget(out MonsterAI previous) || previous == null);
            tracked.Add(new WeakReference<MonsterAI>(ai));
        }

        internal static void Release()
        {
            foreach (var reference in tracked)
                if (reference.TryGetTarget(out MonsterAI ai) && ai != null && callbacks.TryGetValue(ai, out var callback)) ai.m_onConsumedItem -= callback;
            tracked.Clear(); callbacks = new ConditionalWeakTable<MonsterAI, Action<ItemDrop>>();
        }
    }
}
