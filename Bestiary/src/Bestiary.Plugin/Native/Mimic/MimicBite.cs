// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Mimic
{
    /// <summary>
    /// The mimic's one weapon: a copy of the Black Forest skeleton's sword, its damage replaced by the settings' `Bite
    /// Damage` (20 slash by default; the sword's own is 25), stripped of the sword's model and turned into the lunge:
    /// the "lunge" animation (wind-up, a leap forward carried by root motion, the snap), dodgeable but not blockable, used every 4 seconds at most. While the bite recharges
    /// the AI has no attack to use, so it hops after its target instead.
    /// </summary>
    public static class MimicBite
    {
        public const string Animation = "lunge";

        /// <summary>The registered bite's shared data: the ambush bites with the same damage and push.</summary>
        public static ItemDrop.ItemData.SharedData Shared { get; private set; } = null!;

        public static GameObject Build(GameObject sword)
        {
            GameObject bite = PrefabBench.Copy(sword, MimicPrefabs.Bite);
            foreach (string model in new[] { "attach", "attach_skin", "attach_back" })
            {
                Transform part = bite.transform.Find(model);
                if (part != null)
                {
                    Object.DestroyImmediate(part.gameObject);
                }
            }
            Shared = bite.GetComponent<ItemDrop>().m_itemData.m_shared;
            Configure(Shared);
            ApplySettings();
            return bite;
        }

        /// <summary>
        /// The live mimic settings on the bite every mimic shares (a creature's weapon keeps the prefab's shared data):
        /// its recharge and its damage, slash only. Run at build and whenever a mimic wakes, so a rule reload reaches
        /// every mimic.
        /// </summary>
        public static void ApplySettings()
        {
            if (Shared == null)
            {
                return;
            }
            Shared.m_aiAttackInterval = MimicSettings.BiteCooldown;
            Shared.m_damages = new HitData.DamageTypes { m_slash = MimicSettings.BiteDamage };
        }

        private static void Configure(ItemDrop.ItemData.SharedData shared)
        {
            shared.m_name = "$item_ecp_cryptmimic_bite";
            shared.m_blockable = false;          // a shield can't stop it
            shared.m_dodgeable = true;           // a dodge's invincibility can
            shared.m_aiAttackRange = 3.3f;       // starts the lunge from about where the leap lands on the target
            shared.m_aiAttackRangeMin = 0f;
            shared.m_aiAttackMaxAngle = 20f;
            shared.m_aiPrioritized = true;
            shared.m_secondaryAttack = new Attack();
            Attack lunge = shared.m_attack;
            lunge.m_attackAnimation = Animation;
            lunge.m_attackType = Attack.AttackType.Horizontal;
            lunge.m_attackRange = 1.9f;          // from its centre when the jaws close: the lid's reach
            lunge.m_attackHeight = 0.45f;
            lunge.m_attackAngle = 110f;
            lunge.m_attackStamina = 0f;
            lunge.m_speedFactor = 0f;            // the leap is the animation's own root motion
            lunge.m_hitTerrain = false;
        }
    }
}
