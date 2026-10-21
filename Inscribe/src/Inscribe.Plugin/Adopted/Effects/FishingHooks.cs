// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Text;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    // The fishing effects. The float is spawned by the caster's client and runs its line, hook, reeling and catch on
    // its owner (FishingFloat.FixedUpdate returns elsewhere), the caster; each effect acts only when the rod's owner is
    // the local player, reads the local totals and changes the local inventory, stamina or the hooked fish, which that
    // client owns once hooked (Fish.OnHooked claims it). Nothing is sent beyond the game's own traffic.

    /// <summary>
    /// Patient Line (<c>bait_save</c>): the game takes a bait when the cast is thrown and gives it back when the line
    /// comes in empty; it is lost for good once a fish takes it (<c>SetCatch</c> marks it consumed). At that moment
    /// the bait comes back to the inventory with X% chance, once per cast however often fish are hooked and lost.
    /// </summary>
    [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.SetCatch))]
    internal static class BaitSavePatch
    {
        private static void Prefix(FishingFloat __instance, out bool __state) => __state = __instance.m_baitConsumed;

        private static void Postfix(FishingFloat __instance, Fish fish, bool __state)
        {
            if (fish == null || __state || !__instance.m_baitConsumed)
            {
                return;
            }
            Player? player = FishingOwner.LocalRod(__instance);
            float chance = player != null ? AggregateHost.Current[EffectKind.BaitSave] : 0f;
            if (chance <= 0f || Random.value >= chance)
            {
                return;
            }
            ItemDrop.ItemData? bait = ItemGiver.FromPrefab(BaitPrefab(__instance), 1);
            if (bait != null)
            {
                ItemGiver.Give(player!, bait, 1);
                player!.Message(MessageHud.MessageType.TopLeft, Words.Localize("$ecf_fx_bait_kept"));
            }
        }

        // The float keeps the bait's prefab name in its ZDO (set when it was cast).
        private static GameObject? BaitPrefab(FishingFloat line)
        {
            string? name = line.GetBait();
            return string.IsNullOrEmpty(name) || ZNetScene.instance == null ? null : ZNetScene.instance.GetPrefab(name);
        }
    }

    /// <summary>
    /// Big Catch (<c>fish_size</c>): X% better odds of a bigger fish. A fish's size is its level (item quality), rolled
    /// when it spawns: level 1, then one more per success of the spawn's level-up chance. At the catch the roll is
    /// redone at the chance times 1 + X: the step where the spawn roll stopped succeeds with
    /// (better - base) / (1 - base) (the share of failed rolls the better chance would have passed), and every step
    /// after it with the better chance, up to level 5. That is exactly the size spread of the better odds. The base
    /// chance is the game's own level-up chance where the fish is (the spawn table's override is not known there:
    /// judgement call). The new level is saved on the fish's ZDO before the game picks it up.
    /// </summary>
    [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.Catch))]
    internal static class FishSizePatch
    {
        private const int MaxLevel = 5;

        private static void Prefix(Fish fish, Character owner)
        {
            if (fish == null || owner == null || !ReferenceEquals(owner, Player.m_localPlayer))
            {
                return;
            }
            float better = AggregateHost.Current[EffectKind.FishSize];
            ItemDrop? drop = fish.m_itemDrop;
            if (better > 0f && drop != null && drop.m_nview != null && drop.m_nview.IsValid() && drop.m_nview.IsOwner())
            {
                Grow(drop, fish.transform.position, better);
            }
        }

        private static void Grow(ItemDrop drop, Vector3 at, float better)
        {
            drop.Load();
            float chance = Mathf.Clamp(SpawnSystem.GetLevelUpChance(at) / 100f, 0f, 0.9f);
            float improved = Mathf.Min(0.95f, chance * (1f + better));
            int level = drop.m_itemData.m_quality;
            if (improved <= chance || level >= MaxLevel || Random.value >= (improved - chance) / (1f - chance))
            {
                return;
            }
            level++;
            while (level < MaxLevel && Random.value < improved)
            {
                level++;
            }
            drop.SetQuality(level);
            drop.Save();
        }
    }

    /// <summary>
    /// Steady Reel (<c>reel_stamina</c>): reeling costs X% less stamina. Every stamina cost of fishing is spent inside
    /// the float's <c>FixedUpdate</c> (the drain while a fish is on the hook, and pulling the line in); while it runs,
    /// the local player's <c>UseStamina</c> is scaled by 1 - X.
    /// </summary>
    [HarmonyPatch]
    internal static class ReelStaminaPatch
    {
        private static bool _reeling;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.FixedUpdate))]
        private static void Open() => _reeling = true;

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.FixedUpdate))]
        private static void Close() => _reeling = false;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.UseStamina))]
        private static void Cheaper(Player __instance, ref float v)
        {
            if (_reeling && ReferenceEquals(__instance, Player.m_localPlayer))
            {
                v *= Mathf.Max(0f, 1f - AggregateHost.Current[EffectKind.ReelStamina]);
            }
        }
    }

    /// <summary>The player a float's rod belongs to, when that is the local player.</summary>
    internal static class FishingOwner
    {
        public static Player? LocalRod(FishingFloat line)
        {
            Player? local = Player.m_localPlayer;
            return local != null && ReferenceEquals(line.GetOwner(), local) ? local : null;
        }
    }
}
