// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The kraken: a copy of the game's sea serpent, so it keeps a sea creature's swimming, network sync, sounds, hit
    /// effects and resistances, with the serpent's own body hidden (its animator still runs for the game's creature code)
    /// and the kraken's model in its place (<see cref="KrakenModel"/>). A boss (the big health bar) of the sea monsters'
    /// faction with `Health` before stars; the serpent's AI is replaced by <see cref="KrakenBrain"/>, and it can never be
    /// alerted the game's way (its brain shows the boss bar itself). Its capsule is on the game's character layer that
    /// meets players but neither terrain nor ships, so it never shoves the ship it holds. It dies into a
    /// <see cref="KrakenCorpse"/> and drops chitin, its beak, its meat and coins.
    /// </summary>
    public static class KrakenCreature
    {
        public const string Name = "$enemy_ecp_kraken";

        public static GameObject Build(GameObject serpent, AssetBundle bundle, Material skin, GameObject corpse, ZNetScene scene)
        {
            GameObject kraken = PrefabBench.Copy(serpent, KrakenPrefabs.Creature);
            var character = kraken.GetComponent<Character>();
            Boss(character);
            Mind(kraken.GetComponent<MonsterAI>());
            int layer = HideSerpent(kraken);
            KrakenModel.Build(kraken.transform, bundle, skin, layer, hitboxes: true);
            Body(kraken);
            Die(character, corpse);
            Loot(kraken.GetComponent<CharacterDrop>(), scene);
            kraken.AddComponent<KrakenBody>();
            kraken.AddComponent<KrakenAttacks>();
            kraken.AddComponent<KrakenBrain>();
            return kraken;
        }

        private static void Boss(Character character)
        {
            character.m_name = Name;
            character.m_boss = true;
            character.m_health = KrakenSettings.Health;
            character.m_faction = Character.Faction.SeaMonsters;
            character.m_swimDepth = 2f;
            character.m_swimSpeed = KrakenSettings.SwimSpeed;
            character.m_swimTurnSpeed = 60f;
            character.m_staggerWhenBlocked = false;
        }

        private static void Mind(MonsterAI ai)
        {
            ai.m_canBeAlerted = false;
            ai.m_enableHuntPlayer = false;
            ai.m_sleeping = false;
            ai.m_alertedEffects.m_effectPrefabs = new EffectList.EffectData[0];
        }

        /// <summary>
        /// The serpent's renderers off, its hit boxes (which would follow its hidden body) and star looks gone; returns
        /// the layer its body was drawn on, for the kraken's.
        /// </summary>
        private static int HideSerpent(GameObject kraken)
        {
            Renderer[] renderers = kraken.GetComponentsInChildren<Renderer>(true);
            int layer = renderers.Length > 0 ? renderers[0].gameObject.layer : 0;
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = false;
            }
            foreach (Collider collider in kraken.GetComponentsInChildren<Collider>(true).Where(c => c.gameObject != kraken))
            {
                Object.DestroyImmediate(collider);
            }
            foreach (Component looks in kraken.GetComponentsInChildren<LevelEffects>(true))
            {
                Object.DestroyImmediate(looks);
            }
            return layer;
        }

        /// <summary>A capsule round the head's lower part, on the layer that meets players but not the ship or the sea floor.</summary>
        private static void Body(GameObject kraken)
        {
            var capsule = kraken.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                (capsule.radius, capsule.height, capsule.direction, capsule.center) = (1.2f, 3.5f, 1, new Vector3(0f, 1.5f, 0f));
                capsule.gameObject.layer = LayerMask.NameToLayer("character_noenv");
            }
        }

        /// <summary>The serpent's death sounds, then the kraken's corpse, facing as the kraken was, at its own size.</summary>
        private static void Die(Character character, GameObject corpse)
        {
            List<EffectList.EffectData> effects = character.m_deathEffects.m_effectPrefabs
                .Where(effect => effect.m_prefab != null && effect.m_prefab.name.StartsWith("sfx_")).ToList();
            effects.Add(new EffectList.EffectData { m_prefab = corpse, m_inheritParentRotation = true });
            character.m_deathEffects.m_effectPrefabs = effects.ToArray();
        }

        /// <summary>
        /// Chitin from its suckers, its beak (exactly one, whatever the world's resource rate), 5 to 7 cuts of its arms
        /// (<see cref="KrakenLoot"/>), and the coins of the ships it sank. The game's roll never reaches a drop's maximum.
        /// </summary>
        private static void Loot(CharacterDrop drops, ZNetScene scene)
        {
            drops.m_drops = new List<CharacterDrop.Drop>();
            Add(drops, scene.GetPrefab("Chitin"), 20, 30);
            Add(drops, KrakenLoot.MeatItem, 5, 8);
            Add(drops, scene.GetPrefab("Coins"), 150, 300);
            if (KrakenLoot.BeakItem != null)
            {
                drops.m_drops.Add(new CharacterDrop.Drop { m_prefab = KrakenLoot.BeakItem, m_amountMin = 1, m_amountMax = 2, m_chance = 1f, m_dontScale = true });
            }
        }

        private static void Add(CharacterDrop drops, GameObject? item, int min, int max)
        {
            if (item != null)
            {
                drops.m_drops.Add(new CharacterDrop.Drop { m_prefab = item, m_amountMin = min, m_amountMax = max, m_chance = 1f, m_levelMultiplier = true });
            }
        }
    }
}
