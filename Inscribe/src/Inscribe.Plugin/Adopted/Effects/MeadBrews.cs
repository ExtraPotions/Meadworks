// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Text;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The Phase 3 mead affixes, on the drinking player's own client (the game runs a player's inventory and status
    /// effects on the machine that owns that player, so nothing is sent):
    /// <list type="bullet">
    /// <item>Potent Brew (<c>mead_potency</c>): a restoring mead's health, stamina and eitr, up front and over time, X% more.</item>
    /// <item>Long Brew (<c>mead_duration</c>): a mead whose status effect is the benefit itself (a resistance, Tasty,
    /// Lightfoot) lasts X% longer. A restoring mead's status is left alone: its length is the wait before the next one of
    /// its kind, which Brewer's Haste shortens (judgement call: making it longer would only hurt).</item>
    /// <item>Bottomless Flask (<c>mead_save</c>): X% chance the drink does not use the mead up.</item>
    /// </list>
    /// A mead is a consumable without food values whose status effect is an <see cref="SE_Stats"/> (not Bukeperries'
    /// <see cref="SE_Puke"/>). The changes go into the player's own clone of the status effect before its Setup, which
    /// turns the over-time amounts into ticks and applies the up-front part, so Swift Draught's burst sees the stronger heal.
    /// </summary>
    internal static class MeadBrews
    {
        private static int _drinking;
        private static ItemDrop.ItemData? _keep;
        private static bool _kept;

        /// <summary>Player.ConsumeItem prefix: remember the mead's status effect and roll Bottomless Flask.</summary>
        public static void BeginDrink(Player player, ItemDrop.ItemData? item)
        {
            Clear();
            if (item == null || !ReferenceEquals(player, Player.m_localPlayer) || !IsMead(item))
            {
                return;
            }
            _drinking = item.m_shared.m_consumeStatusEffect.NameHash();
            float save = AggregateHost.Current[EffectKind.MeadSave];
            if (save > 0f && Random.value < save)
            {
                _keep = item;
            }
        }

        /// <summary>Player.ConsumeItem postfix: tell the player when the flask stayed full.</summary>
        public static void EndDrink(Player player, bool drunk)
        {
            if (drunk && _kept)
            {
                player.Message(MessageHud.MessageType.TopLeft, Words.Localize("$ecf_fx_mead_kept"));
            }
            Clear();
        }

        /// <summary>True: the inventory keeps this item (its RemoveOneItem is skipped).</summary>
        public static bool Keep(ItemDrop.ItemData item)
        {
            if (_keep == null || !ReferenceEquals(item, _keep))
            {
                return false;
            }
            _keep = null;
            _kept = true;
            return true;
        }

        /// <summary>SE_Stats.Setup prefix: the drunk mead's own clone, before Setup reads its amounts.</summary>
        public static void BeforeSetup(SE_Stats effect, Character character)
        {
            if (_drinking == 0 || effect.NameHash() != _drinking || !ReferenceEquals(character, Player.m_localPlayer))
            {
                return;
            }
            _drinking = 0;
            AggregateValues v = AggregateHost.Current;
            if (Restores(effect))
            {
                Strengthen(effect, 1f + v[EffectKind.MeadPotency]);
            }
            else if (effect.m_ttl > 0f)
            {
                effect.m_ttl *= 1f + v[EffectKind.MeadDuration];
            }
        }

        private static void Strengthen(SE_Stats effect, float factor)
        {
            if (factor == 1f)
            {
                return;
            }
            effect.m_healthUpFront *= factor;
            effect.m_healthOverTime *= factor;
            effect.m_staminaUpFront *= factor;
            effect.m_staminaOverTime *= factor;
            effect.m_eitrUpFront *= factor;
            effect.m_eitrOverTime *= factor;
        }

        private static bool IsMead(ItemDrop.ItemData item)
        {
            ItemDrop.ItemData.SharedData shared = item.m_shared;
            return shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable && shared.m_food <= 0f
                && shared.m_foodStamina <= 0f && shared.m_foodEitr <= 0f
                && shared.m_consumeStatusEffect is SE_Stats && !(shared.m_consumeStatusEffect is SE_Puke);
        }

        private static bool Restores(SE_Stats effect) =>
            effect.m_healthOverTime > 0f || effect.m_healthUpFront > 0f || effect.m_staminaOverTime > 0f
            || effect.m_staminaUpFront > 0f || effect.m_eitrOverTime > 0f || effect.m_eitrUpFront > 0f;

        private static void Clear()
        {
            _drinking = 0;
            _keep = null;
            _kept = false;
        }
    }

    /// <summary>Brackets the local player's drink for the mead affixes (runs on the drinker's own client).</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem))]
    internal static class MeadDrinkPatch
    {
        private static void Prefix(Player __instance, ItemDrop.ItemData item) => MeadBrews.BeginDrink(__instance, item);

        private static void Postfix(Player __instance, bool __result) => MeadBrews.EndDrink(__instance, __result);
    }

    /// <summary>Potent Brew and Long Brew: the clone SEMan.AddStatusEffect sets up for the drunk mead.</summary>
    [HarmonyPatch(typeof(SE_Stats), nameof(SE_Stats.Setup))]
    internal static class MeadSetupPatch
    {
        private static void Prefix(SE_Stats __instance, Character character) => MeadBrews.BeforeSetup(__instance, character);
    }

    /// <summary>Bottomless Flask: the one removal ConsumeItem makes is skipped for a kept mead.</summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveOneItem))]
    internal static class MeadKeepPatch
    {
        private static bool Prefix(ItemDrop.ItemData item, ref bool __result)
        {
            if (!MeadBrews.Keep(item))
            {
                return true;
            }
            __result = true;
            return false;
        }
    }
}
