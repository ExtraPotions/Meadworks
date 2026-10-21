// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Experience/HerdWatch.cs and HerdSnapshot.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; ticked from Shepherd's plugin update instead of a Player.Update patch; the
// experience decision and the tame discovery list are in the portable ExtraPotions.Shepherd.Husbandry core; Shepherd's
// birth counter key; no work on a headless process.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Shepherd.Breeding;
using CoreHerd = ExtraPotions.Shepherd.Husbandry;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Husbandry experience for tending animals, earned on each keeper's own client with no network traffic. Every two
    /// seconds the local player's client snapshots each tameable creature within Keeper Range (from ZDO values the
    /// creature's owner writes and the game replicates) and credits what changed since the last snapshot
    /// (<see cref="CoreHerd.HerdExperience"/>), times the creature's tier. A creature seen for the first time earns
    /// nothing, so walking into a farm or loading one is no windfall; one out of range or unloaded is forgotten.
    /// Everyone near earns: tending a herd together trains everyone tending it. Feeding experience follows the game's
    /// feeding stamp, so trough and container meals (which go through <c>m_onConsumedItem</c>) count like ground food.
    /// </summary>
    public static class HerdWatch
    {
        private const float Interval = 2f;

        private static readonly Dictionary<ZDOID, CoreHerd.HerdSnapshot> seen = new Dictionary<ZDOID, CoreHerd.HerdSnapshot>();
        private static readonly Dictionary<ZDOID, CoreHerd.HerdSnapshot> next = new Dictionary<ZDOID, CoreHerd.HerdSnapshot>();
        private static float timer;

        /// <summary>Every frame from the plugin's update; does nothing without a local player or while Husbandry is off.</summary>
        public static void Tick()
        {
            Player player = Player.m_localPlayer;
            if (player == null || !Plugin.AllowsUi || !HusbandrySkill.Active)
            {
                Clear();
                return;
            }
            timer += Time.deltaTime;
            if (timer < Interval)
                return;
            timer = 0f;
            NativeHusbandry.Run("herd watch", Scan, player);
        }

        public static void Clear()
        {
            seen.Clear();
            next.Clear();
            timer = 0f;
        }

        private static void Scan(Player player)
        {
            next.Clear();
            float experience = 0f;
            var rates = new CoreHerd.ExperienceRates(HusbandryExperienceSettings.Taming.Value, HusbandryExperienceSettings.Tamed.Value,
                HusbandryExperienceSettings.Discovery.Value, HusbandryExperienceSettings.Feeding.Value, HusbandryExperienceSettings.Birth.Value);
            foreach (Character creature in Character.GetAllCharacters())
                experience += Watch(player, creature, rates);
            seen.Clear();
            foreach (KeyValuePair<ZDOID, CoreHerd.HerdSnapshot> entry in next)
                seen[entry.Key] = entry.Value;
            HusbandryXp.Raise(player, experience);
        }

        private static float Watch(Player player, Character creature, CoreHerd.ExperienceRates rates)
        {
            Tameable tameable = Herd.TameableOf(creature);
            float range = HusbandrySettings.KeeperRange.Value;
            if (tameable == null || tameable.m_nview == null || !tameable.m_nview.IsValid()
                || (creature.transform.position - player.transform.position).sqrMagnitude > range * range)
                return 0f;
            ZDOID id = creature.GetZDOID();
            CoreHerd.HerdSnapshot now = Snapshot(tameable);
            next[id] = now;
            if (!seen.TryGetValue(id, out CoreHerd.HerdSnapshot before))
                return 0f;
            string prefab = Herd.PrefabName(creature);
            double amount = CoreHerd.HerdExperience.Between(before, now, rates,
                () => CoreHerd.TameDiscovery.TryRecord(player.m_customData, prefab));
            return (float)amount * Herd.Tier(prefab);
        }

        private static CoreHerd.HerdSnapshot Snapshot(Tameable tameable)
        {
            ZDO zdo = tameable.m_nview.GetZDO();
            bool tamed = tameable.IsTamed();
            return new CoreHerd.HerdSnapshot(tamed ? 0f : zdo.GetFloat(ZDOVars.s_tameTimeLeft, tameable.m_tamingTime),
                tameable.m_tamingTime, tamed, zdo.GetLong(ZDOVars.s_tameLastFeeding, 0L), zdo.GetInt(BreedingKeys.Births, 0));
        }
    }
}
