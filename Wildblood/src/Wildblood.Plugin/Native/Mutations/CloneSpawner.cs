// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Aspects;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Makes a Cloning creature's decoy, on the creature's owner, at its exact place and facing: another of its own prefab
    /// that looks the same in every way. It is born already resolved with the creature's stars and mutations (so its name
    /// and stars read the same), its biome, its devoured health and its current health (so its bar matches), and the same
    /// seed for the game's random gear (so it holds the same weapon). It is marked as the creature's decoy in its ZDO in
    /// the frame it is made, before its own controller wakes, so every machine knows it for one from the start
    /// (<see cref="CreatureTraits.Decoy"/>). It is not persistent: never saved, and gone with its owner or its zone, so a
    /// reload never finds one. It wakes fighting the creature's target, and the two bodies pass through each other, so
    /// neither shoves the other aside as they part.
    /// </summary>
    internal static class CloneSpawner
    {
        /// <summary>The new decoy's id, or None when none could be made.</summary>
        public static ZDOID Make(EliteController real, Character target)
        {
            ZDO realZdo = real.View.GetZDO();
            GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(realZdo.GetPrefab()) : null;
            if (prefab == null)
            {
                Log.Warn($"{real.name}: no prefab to make its Cloning decoy from");
                return ZDOID.None;
            }
            Transform at = real.transform;
            GameObject copy = Object.Instantiate(prefab, at.position, at.rotation);
            Character decoy = copy.GetComponent<Character>();
            ZNetView view = copy.GetComponent<ZNetView>();
            if (decoy == null || view == null || view.GetZDO() == null)
            {
                Object.Destroy(copy); // never left behind as an unmarked, full-strength second creature
                return ZDOID.None;
            }
            Mark(view.GetZDO(), realZdo, real);
            Arm(decoy, real.Creature, view.GetZDO());
            CloneBodies.Ignore(real.Creature, decoy);
            Wake(decoy, target);
            return view.GetZDO().m_uid;
        }

        private static void Mark(ZDO decoy, ZDO realZdo, EliteController real)
        {
            decoy.Persistent = false;
            CloneStore.MarkDecoy(decoy, realZdo.m_uid);
            TraitStore.Save(decoy, new CreatureTraits(real.Traits.Stars, real.Traits.Mask) { Tier = real.Traits.Tier });
            TraitStore.SetBiome(decoy, TraitStore.GetBiome(realZdo));
            decoy.Set(TraitKeys.DevouredHealth, TraitStore.GetDevouredHealth(realZdo));
            decoy.Set(ZDOVars.s_health, real.Creature.GetHealth());
        }

        /// <summary>
        /// The game picks a humanoid's random weapon, shield and armour from a seed its ZDO keeps, as it starts; the decoy
        /// has not started yet, so the creature's seed in its place gives it the very same gear on every machine.
        /// </summary>
        internal static void Arm(Character decoy, Character real, ZDO zdo)
        {
            if (decoy is Humanoid copy && real is Humanoid source)
            {
                copy.m_seed = source.m_seed;
                zdo.Set(ZDOVars.s_seed, source.m_seed);
            }
        }

        /// <summary>Awake (a prefab that starts asleep is roused, as a Phantom copy is), alerted and on the same target.</summary>
        private static void Wake(Character decoy, Character target)
        {
            PhantomSpawner.Rouse(decoy);
            if (decoy.GetBaseAI() is MonsterAI ai)
            {
                AlertQuietly(ai);
                ai.SetTarget(target);
            }
        }

        /// <summary>
        /// Alerted the quiet way - the flag, its animation and its ZDO key, which every other machine copies - without the
        /// alert cry the game's own path plays: the creature it stands in for is already in the fight, and a second cry
        /// at the swap would give the trick away.
        /// </summary>
        private static void AlertQuietly(MonsterAI ai)
        {
            if (ai.m_alerted || !ai.m_canBeAlerted || ai.m_nview == null || !ai.m_nview.IsValid())
            {
                return;
            }
            ai.m_alerted = true;
            ai.m_animator.SetBool("alert", true);
            ai.m_nview.GetZDO().Set(ZDOVars.s_alert, true);
        }
    }
}
