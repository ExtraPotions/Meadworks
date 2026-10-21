// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Trophy Taker and Hoardfinder (affixes.md, effects <c>find_trophy</c>, <c>find_coins</c>): the dying creature's own
    /// vanilla drop chances for its trophy, and for coins and valuables, times <c>1 + X/100</c> of the killer's total.
    /// The game rolls a creature's loot in <c>CharacterDrop.GenerateDropList</c>, called during <c>Character.OnDeath</c>
    /// on the creature's owner - directly, or through the ragdoll, which stores the list for later (verified in the
    /// decompile). The death hook opens a window for that one creature (<see cref="Begin"/> / <see cref="End"/>); while
    /// it is open, <see cref="GenerateDropListPatch"/> scales the matching rows' <c>m_chance</c> and puts them back right
    /// after. Nothing else about vanilla loot changes: amounts, the level multiplier and the game's pseudo-random
    /// smoothing apply as usual (the smoothing restarts its count when the chance it sees changes, as it does for any
    /// changed chance). A trophy is a drop whose item type is Trophy; coins and valuables are the <c>Coins</c> item and
    /// any item the trader buys (item value above 0). Owner only; main thread.
    /// </summary>
    internal static class VanillaDropBoost
    {
        private const string CoinsPrefab = "Coins";

        private static readonly List<KeyValuePair<CharacterDrop.Drop, float>> Saved = new List<KeyValuePair<CharacterDrop.Drop, float>>();
        private static GameObject? _creature;
        private static float _trophy = 1f;
        private static float _coins = 1f;

        /// <summary>Opens the window for one death. Only a creature that is not a player's own ally is an enemy.</summary>
        public static void Begin(Character creature, LootModifiers modifiers)
        {
            _creature = null;
            if (!modifiers.BoostsVanillaDrops || LootKeys.IsPlayerAlly(creature))
            {
                return;
            }
            _creature = creature.gameObject;
            _trophy = modifiers.TrophyFactor;
            _coins = modifiers.CoinsFactor;
        }

        public static void End() => _creature = null;

        /// <summary>Scales the matching rows of this creature's drop table; returns whether anything was scaled.</summary>
        public static bool Apply(CharacterDrop drops)
        {
            if (_creature == null || drops == null || !ReferenceEquals(drops.gameObject, _creature))
            {
                return false;
            }
            Saved.Clear();
            foreach (CharacterDrop.Drop drop in drops.m_drops)
            {
                float factor = FactorFor(drop);
                if (factor != 1f)
                {
                    Saved.Add(new KeyValuePair<CharacterDrop.Drop, float>(drop, drop.m_chance));
                    drop.m_chance *= factor;
                }
            }
            return Saved.Count > 0;
        }

        /// <summary>Puts the saved chances back (the rows belong to the creature instance; nothing may keep our change).</summary>
        public static void Restore()
        {
            foreach (KeyValuePair<CharacterDrop.Drop, float> pair in Saved)
            {
                pair.Key.m_chance = pair.Value;
            }
            Saved.Clear();
        }

        private static float FactorFor(CharacterDrop.Drop drop)
        {
            ItemDrop? item = drop?.m_prefab != null ? drop.m_prefab.GetComponent<ItemDrop>() : null;
            if (item == null)
            {
                return 1f;
            }
            ItemDrop.ItemData.SharedData shared = item.m_itemData.m_shared;
            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy)
            {
                return _trophy;
            }
            return drop!.m_prefab.name == CoinsPrefab || shared.m_value > 0 ? _coins : 1f;
        }
    }

    /// <summary>
    /// The scaling window inside the game's drop-list roll (see <see cref="VanillaDropBoost"/>). A prefix scales, a
    /// finalizer restores even when the game's method throws. Runs wherever the game calls it; it acts only for the
    /// creature whose death is being handled on this peer (the owner).
    /// </summary>
    [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
    internal static class GenerateDropListPatch
    {
        private static void Prefix(CharacterDrop __instance, out bool __state)
        {
            __state = VanillaDropBoost.Apply(__instance);
        }

        private static void Finalizer(bool __state)
        {
            if (__state)
            {
                VanillaDropBoost.Restore();
            }
        }
    }
}
