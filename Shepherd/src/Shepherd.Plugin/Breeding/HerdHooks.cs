// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Lore/BreedingLore.cs (herd line and count) and
// Husbandry/Lore/LoreHover.cs (hover postfix and refresh cache).
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: limit, strict hold and Unlimited Herd decided by the portable HerdLimit; Herd module and
// cheat gates; native pen-scoped crowding; the single S10 lore hover owns display and refresh; C05 localization.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Shepherd.Breeding;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using ExtraPotions.Shepherd.Runtime.Integration;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Breeding
{
    /// <summary>
    /// Herd limits (design 10.5, S08), on the parent's owner inside <c>Procreation.Procreate</c>:
    /// <list type="bullet">
    /// <item>Limit: <c>m_maxCreatures</c> plus the keeper's Herd Size bonus (Herd module and Husbandry on), or no limit
    /// under the Unlimited Herd cheat (C02 gate, safe value off), applied as a temporary field edit restored by the
    /// Procreate finalizer (<see cref="ExtraMax"/>).</item>
    /// <item>Strict Herd Limit: a due birth waits while the parent's kind and its young within <c>m_totalCheckRange</c>
    /// are at or over the limit (<see cref="Holds"/>).</item>
    /// <item>Hover: a tamed breeding animal shows "Herd room: n of m", "Waiting for room" or unlimited, once the local
    /// player reaches Animal Lore Level (graphical clients only).</item>
    /// </list>
    /// </summary>
    public static class HerdHooks
    {
        public static void Clear() { LoreHover.Clear(); }

        private static bool Unlimited => ShepherdSettings.UnlimitedHerd.Value;

        private static HerdVerdict Evaluate(Procreation parent, float level, bool counted)
        {
            bool herd = NativeBreeding.HerdOn;
            double size = herd && NativeHusbandry.Active ? HusbandryBreedingSettings.HerdSize.Value : 0;
            int count = counted ? Count(parent) : 0;
            return HerdLimit.Evaluate(new HerdInput(LivestockPens.Maximum(parent, parent.m_maxCreatures), count, size, level, Unlimited, herd && ShepherdSettings.StrictHerd.Value));
        }

        /// <summary>Room to add to <c>m_maxCreatures</c> for this call (0 when neither the bonus nor the cheat applies).</summary>
        public static int ExtraMax(Procreation parent, float level) => Evaluate(parent, level, counted: false).ExtraMax;

        public static int Maximum(Procreation parent, float level) => Evaluate(parent, level, counted: false).Limit;

        /// <summary>True when Strict Herd Limit holds this parent's due birth; the Procreate call is then skipped.</summary>
        public static bool Holds(Procreation parent)
        {
            if (!NativeBreeding.HerdOn || !ShepherdSettings.StrictHerd.Value || Unlimited || !parent.IsPregnant() || !parent.IsDue())
                return false;
            float level = NativeHusbandry.Active ? Keeper.BestLevel(parent.transform.position) : 0f;
            bool holds = HerdLimit.HoldsBirth(Evaluate(parent, level, counted: true), true, true);
            if (holds) NativeBreeding.Verbose("strict herd limit: birth waits for room at " + global::Utils.GetPrefabName(parent.gameObject));
            return holds;
        }

        /// <summary>The parent's own kind plus its offspring kind within the game's crowding range, as the game counts them.</summary>
        private static int Count(Procreation parent)
        {
            Vector3 position = parent.transform.position;
            float range = parent.m_totalCheckRange;
            GameObject? own = ZNetScene.instance != null && parent.m_nview != null && parent.m_nview.IsValid()
                ? ZNetScene.instance.GetPrefab(parent.m_nview.GetZDO().GetPrefab()) : null;
            return Count(own, position, range) + Count(parent.m_offspring, position, range);
        }

        private static int Count(GameObject? prefab, Vector3 position, float range)
        {
            GameObject? named = prefab != null && ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(global::Utils.GetPrefabName(prefab)) : null;
            return named != null ? LivestockPens.Count(named, position) ?? SpawnSystem.GetNrOfInstances(named, position, range) : 0;
        }


        [HarmonyPatch(typeof(SpawnSystem), nameof(SpawnSystem.GetNrOfInstances), new[] { typeof(GameObject), typeof(Vector3), typeof(float), typeof(bool), typeof(bool) })]
        private static class PenCount
        {
            [HarmonyPrefix]
            private static bool Prefix(GameObject prefab, Vector3 center, float maxRange, bool eventCreaturesOnly, bool procreationOnly, ref int __result)
            {
                if (!LivestockPens.TryNativeCount(prefab, center, maxRange, eventCreaturesOnly, procreationOnly, out var count)) return true;
                __result=count;return false;
            }
        }

        public static string Line(Tameable tameable)
        {
            Procreation procreation = tameable.GetComponent<Procreation>();
            if (procreation == null || !tameable.IsTamed() || tameable.m_nview == null || !tameable.m_nview.IsValid()) return "";
            if (!LoreText.Visible || !(NativeBreeding.HerdOn || Unlimited)) return "";
            float level = Keeper.BestLevel(tameable.transform.position);
            HerdLine line = HerdLimit.Describe(Evaluate(procreation, level, counted: true), procreation.IsPregnant(), procreation.IsDue());
            return "<color=#c0a060>" + ShepherdLocalization.Format(line.Key, line.Room, line.Limit) + "</color>";
        }
    }
}
