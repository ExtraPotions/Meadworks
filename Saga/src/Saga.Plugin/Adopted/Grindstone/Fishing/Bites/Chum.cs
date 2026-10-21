// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Bites/Chum.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Chum: items named in Chum Items (entrails and blood bags by default) dropped in the water draw fish. Nothing new is
    /// made; the dropped item floats as any item does.
    /// <list type="bullet">
    /// <item><b>Bites:</b> a float within Chum Radius of floating chum gets Chum Bite Bonus (<see cref="Factor"/>, read by
    /// the fish's owner in <see cref="BiteChance"/>). The floating chum near a machine is listed at most once a second
    /// from the game's own list of dropped items.</item>
    /// <item><b>Dissolving:</b> on the item's owner, every 10 s (ItemDrop.SlowUpdate), floating chum is stamped with the
    /// world time it was first seen floating (<see cref="Keys.ChumSince"/>, in its ZDO, so an owner change keeps it), and
    /// is removed after Chum Duration. One dropped stack is one piece of chum.</item>
    /// </list>
    /// </summary>
    public static class Chum
    {
        private static readonly int ChumSinceHash = Keys.ChumSince.GetStableHashCode();
        private const float ListEvery = 1f;

        private static readonly List<Vector3> floating = new List<Vector3>();
        private static float listed = float.MinValue;
        private static string namesFrom;
        private static HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.SlowUpdate))]
        private static class Dissolve
        {
            [HarmonyPostfix]
            private static void Postfix(ItemDrop __instance)
            {
                if (FishSkill.Active)
                    HookGuard.Run("chum", static item => Age(item), __instance);
            }
        }

        /// <summary>The bite factor for a float at <paramref name="position"/>: Chum Bite Bonus with chum near, else 1.</summary>
        public static float Factor(Vector3 position) =>
            Near(position) ? 1f + FishSkill.Percent(FishingBiteSettings.ChumBonus.Value) : 1f;

        public static bool Near(Vector3 position)
        {
            List();
            float radius = FishingBiteSettings.ChumRadius.Value;
            foreach (Vector3 spot in floating)
            {
                if ((spot - position).sqrMagnitude <= radius * radius)
                    return true;
            }
            return false;
        }

        private static void List()
        {
            if (Time.time - listed < ListEvery)
                return;
            listed = Time.time;
            floating.Clear();
            if (Names().Count == 0)
                return;
            foreach (ItemDrop drop in ItemDrop.s_instances)
            {
                if (IsChum(drop) && IsFloating(drop))
                    floating.Add(drop.transform.position);
            }
        }

        private static void Age(ItemDrop drop)
        {
            if (drop.m_nview == null || !drop.m_nview.IsValid() || !drop.m_nview.IsOwner() || !IsChum(drop) || !IsFloating(drop))
                return;
            ZDO zdo = drop.m_nview.GetZDO();
            long now = ZNet.instance.GetTime().Ticks;
            long since = zdo.GetLong(ChumSinceHash, 0L);
            if (since == 0L)
                zdo.Set(Keys.ChumSince, now);
            else if (TimeSpan.FromTicks(now - since).TotalSeconds >= FishingBiteSettings.ChumDuration.Value)
                drop.m_nview.Destroy();
        }

        private static bool IsChum(ItemDrop drop) =>
            drop != null && drop.m_itemData?.m_dropPrefab != null && Names().Contains(drop.m_itemData.m_dropPrefab.name);

        private static bool IsFloating(ItemDrop drop) => drop.m_floating != null && drop.m_floating.HaveLiquidLevel();

        /// <summary>The Chum Items setting as a set of prefab names, parsed again only when it changes.</summary>
        private static HashSet<string> Names()
        {
            string value = FishingBiteSettings.ChumItems.Value ?? "";
            if (value == namesFrom)
                return names;
            namesFrom = value;
            names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in value.Split(','))
            {
                if (part.Trim().Length > 0)
                    names.Add(part.Trim());
            }
            return names;
        }
    }
}
