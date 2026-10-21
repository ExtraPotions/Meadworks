// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Item lookup (collider to ItemDrop through FloatingTerrainDummy), the wanted check, the fit check and the pull step are
// adapted from OpenKeep src/BuildCamera/CameraPickup.cs (ItemOf, Wanted, Fits, Pull).
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later. Reference ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: body-centered radius from Magpie settings, scan scheduler and budgets, smooth magnet speed,
// ignore list, take-time owner re-check, effect skip and grouped messages.
using System.Collections.Generic;
using ExtraPotions.Magpie.Config;
using ExtraPotions.Magpie.Pickup;
using ExtraPotions.Magpie.Runtime.Settings;
using ExtraPotions.Magpie.StepAside;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Magpie.Runtime.Pickup
{
    /// <summary>
    /// Design A1 to A12 (G04): replaces the game's <c>Player.AutoPickup</c> loop for the local player while the Pickup
    /// module is on. The game calls <c>ItemDrop.Load()</c> for every item in range every physics frame; Magpie searches
    /// once per Scan Interval, loads only items it is about to pull, and takes at most Max Pickups Per Scan items per
    /// search and one per frame, always through <c>Humanoid.Pickup</c> (so Mantle routing and the game's statistics stay).
    /// </summary>
    internal static class PickupEngine
    {
        private const float FallbackBaseRange = 2f;

        private static readonly Collider[] Hits = new Collider[128];
        private static readonly List<Tracked> Items = new List<Tracked>();
        private static readonly HashSet<ItemDrop> Seen = new HashSet<ItemDrop>();
        private static readonly HashSet<ItemDrop> PulledNow = new HashSet<ItemDrop>();
        private static readonly PickupScheduler Scheduler = new PickupScheduler();
        private static readonly PickupMessageGroup<ItemDrop.ItemData> Messages = new PickupMessageGroup<ItemDrop.ItemData>();
        private static readonly EffectList NoEffects = new EffectList();
        private static float baseRange = -1f;
        private static bool taking;
        private static bool flushing;

        private struct Tracked
        {
            public ItemDrop Item;
            public FloatingTerrainDummy? Dummy;
        }

        /// <summary>Magpie runs the auto pickup loop for this player now.</summary>
        public static bool Replaces(Player player) =>
            player != null && player == Player.m_localPlayer && MagpieRuntime.ModuleOn(MagpieModule.Pickup);

        /// <summary>The magnet moved this item during the last pickup step; merging leaves it alone (B7).</summary>
        public static bool IsPulled(ItemDrop item) => PulledNow.Contains(item);

        /// <summary>One physics step of Magpie pickup, called instead of the game's loop.</summary>
        public static void Step(Player player, float dt)
        {
            PulledNow.Clear();
            var settings = MagpieRuntime.Settings!;
            if (!Player.m_enableAutoPickup || player.IsTeleporting() || player.IsDead())
            {
                Items.Clear();
                return;
            }
            var center = player.transform.position + Vector3.up;
            var radius = Radius(player);
            if (Scheduler.ScanDue(Time.time, settings.Get<float>(MagpieSettings.ScanInterval)))
            {
                Collect(player, center, radius, settings.List(MagpieSettings.IgnoreItems));
                Scheduler.BeginScan(settings.Get<int>(MagpieSettings.MaxPickupsPerScan));
            }
            var speed = settings.Get<float>(MagpieSettings.MagnetSpeed);
            for (var i = Items.Count - 1; i >= 0; i--)
            {
                var tracked = Items[i];
                if (tracked.Item == null || !Advance(player, tracked, center, radius, speed, dt, settings))
                    Items.RemoveAt(i);
            }
        }

        /// <summary>Shows grouped pickup lines that are due (A11); called from the plugin's Update.</summary>
        public static void FlushMessages()
        {
            var player = Player.m_localPlayer;
            var due = Messages.Flush(Time.time);
            if (player == null || due.Count == 0) return;
            flushing = true;
            try
            {
                foreach (var line in due)
                    player.ShowPickupMessage(line.Payload, line.Amount);
            }
            finally
            {
                flushing = false;
            }
        }

        public static void Reset()
        {
            Items.Clear();
            PulledNow.Clear();
            Messages.Clear();
            Scheduler.Reset();
        }

        /// <summary>
        /// Message hook for <c>Character.ShowPickupMessage</c>: during a Magpie take the line is hidden (Show Pickup Messages
        /// off) or grouped; everything else passes through unchanged.
        /// </summary>
        public static bool AllowMessage(ItemDrop.ItemData item, int amount)
        {
            if (!taking || flushing || item == null) return true;
            var settings = MagpieRuntime.Settings;
            if (settings == null) return true;
            if (!settings.Flag(MagpieSettings.ShowPickupMessages)) return false;
            if (!settings.Flag(MagpieSettings.GroupPickupMessages)) return true;
            Messages.Add(item.m_shared.m_name, amount, Time.time, item, grouping: true);
            return false;
        }

        private static void Collect(Player player, Vector3 center, float radius, NameList ignore)
        {
            Items.Clear();
            Seen.Clear();
            var count = Physics.OverlapSphereNonAlloc(center, radius, Hits, player.m_autoPickupMask);
            for (var i = 0; i < count; i++)
            {
                var item = ItemOf(Hits[i], out var dummy);
                if (item == null || !Seen.Add(item)) continue;
                var facts = Facts(player, item, ignore, fits: true);
                if (PickupRules.Decide(facts) == PickupAction.Skip) continue;
                Items.Add(new Tracked { Item = item, Dummy = dummy });
            }
        }

        /// <summary>One frame for one item: own it, check room, take it when close, else glide it in. False drops it from the list.</summary>
        private static bool Advance(Player player, Tracked tracked, Vector3 center, float radius, float speed, float dt, MagpieConfig settings)
        {
            var item = tracked.Item;
            var facts = Facts(player, item, settings.List(MagpieSettings.IgnoreItems), fits: true);
            switch (PickupRules.Decide(facts))
            {
                case PickupAction.Skip:
                    return false;
                case PickupAction.RequestOwnership:
                    item.RequestOwn();
                    return true;
                case PickupAction.Hold:
                    return true;
            }
            item.Load();
            if (!Fits(player, item)) return true;
            var step = Magnet.Step(Vector3.Distance(item.transform.position, center), radius, speed, dt);
            switch (step.Action)
            {
                case MagnetAction.Outside:
                    return false;
                case MagnetAction.Take:
                    if (!Scheduler.TryTake(Time.frameCount)) return true;
                    Take(player, item, settings);
                    return false;
                default:
                    var move = (center - item.transform.position).normalized * step.Move;
                    item.transform.position += move;
                    if (tracked.Dummy != null) tracked.Dummy.transform.position += move;
                    PulledNow.Add(item);
                    return true;
            }
        }

        internal static float Radius(Player player)
        {
            var settings = MagpieRuntime.Settings!;
            return PickupRadius.Effective(settings.Get<float>(MagpieSettings.PickupRange), settings.Get<float>(MagpieSettings.MaxPickupRange),
                player.m_autoPickupRange, BaseRange());
        }

        internal static bool Wanted(Player player, ItemDrop item) => item != null && item.m_itemData != null &&
            PickupRules.Decide(Facts(player, item, MagpieRuntime.Settings!.List(MagpieSettings.IgnoreItems), fits: true)) != PickupAction.Skip;

        internal static bool TryTakeExternal(Player player, ItemDrop item, System.Func<bool> current)
        {
            if (taking || !current() || item == null || item.m_itemData == null) return false;
            var settings = MagpieRuntime.Settings!;
            if (PickupRules.Decide(Facts(player, item, settings.List(MagpieSettings.IgnoreItems), fits: true)) != PickupAction.Pull) return false;
            item.Load();
            if (!Fits(player, item) || !current() || !Scheduler.TryTake(Time.frameCount)) return false;
            return Take(player, item, settings);
        }

        private static bool Take(Player player, ItemDrop item, MagpieConfig settings)
        {
            var view = item.m_nview;
            if (!PickupRules.CanTakeNow(view != null && view.IsValid(), item.CanPickup()))
                return false;
            var skipEffects = settings.Flag(MagpieSettings.SkipPickupEffects) || !Plugin.AllowsUi;
            var effects = player.m_pickupEffects;
            if (skipEffects) player.m_pickupEffects = NoEffects;
            taking = true;
            bool picked;
            try
            {
                picked = player.Pickup(item.gameObject, true, true);
                if (picked && !skipEffects)
                    player.m_autopickupEffects.Create(player.transform.position, Quaternion.identity);
            }
            finally
            {
                taking = false;
                player.m_pickupEffects = effects;
            }
            MagpieRuntime.Log?.Debug("Took " + item.m_itemData?.m_shared?.m_name + ".");
            return picked;
        }

        private static PickupFacts Facts(Player player, ItemDrop item, NameList ignore, bool fits)
        {
            var view = item.m_nview;
            var valid = view != null && view.IsValid();
            return new PickupFacts(item.m_autoPickup, item.IsPiece(), valid,
                item.m_itemData != null && player.HaveUniqueKey(item.m_itemData.m_shared.m_name),
                ignore.Contains(Utils.GetPrefabName(item.gameObject)), valid && item.CanPickup(), valid && item.InTar(), fits);
        }

        private static bool Fits(Player player, ItemDrop item)
        {
            var inventory = player.GetInventory();
            return inventory.CanAddItem(item.m_itemData)
                && item.m_itemData.GetWeight() + inventory.GetTotalWeight() <= player.GetMaxCarryWeight();
        }

        private static ItemDrop? ItemOf(Collider collider, out FloatingTerrainDummy? dummy)
        {
            dummy = null;
            var body = collider != null ? collider.attachedRigidbody : null;
            if (body == null) return null;
            var item = body.GetComponent<ItemDrop>();
            if (item != null) return item;
            dummy = body.GetComponent<FloatingTerrainDummy>();
            return dummy != null && dummy.m_parent != null ? dummy.m_parent.GetComponent<ItemDrop>() : null;
        }

        /// <summary>The player prefab's own auto pickup range, so another mod's change to the live value is a factor (A12).</summary>
        private static float BaseRange()
        {
            if (baseRange > 0f) return baseRange;
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Player") : null;
            var template = prefab != null ? prefab.GetComponent<Player>() : null;
            if (template == null) return FallbackBaseRange;
            baseRange = template.m_autoPickupRange > 0f ? template.m_autoPickupRange : FallbackBaseRange;
            return baseRange;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.AutoPickup))]
    internal static class AutoPickupPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Player __instance, float dt)
        {
            if (!PickupEngine.Replaces(__instance)) return true;
            PickupEngine.Step(__instance, dt);
            return false;
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.ShowPickupMessage))]
    internal static class PickupMessagePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemDrop.ItemData item, int amount) => PickupEngine.AllowMessage(item, amount);
    }
}
