// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Companions/Feeders.cs and FeederLife.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: every loaded container is tracked (troughs and optional container feeding); the owner
// take moved behind the Shepherd take protocol (TakeTransport); feed-only trough inventories; Burrow feed_source
// capability read through Crucible with a safe fallback to Shepherd's own mark; source-owned container feeding yields.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ExtraPotions.Shepherd.Feeding;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using ExtraPotions.Shepherd.Runtime.Integration;
using ExtraPotions.Shepherd.Troughs;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Shepherd.Runtime.Feeding
{
    /// <summary>
    /// The containers loaded on this machine and what they hold.
    /// <list type="bullet">
    /// <item>Every placed container (one with a ZDO, not the build ghost) adds itself in a Container.Awake postfix and
    /// drops out when destroyed (<see cref="FeedSourceLife"/>); the list empties when the world's net scene goes.</item>
    /// <item>A container's inventory is changed only by its owner, which saves it to the ZDO; other machines reload it
    /// within a second. That copy is what an animal's owner reads to choose a source; the take itself runs on the
    /// container's owner (<see cref="TakeTransport"/>).</item>
    /// <item>Trough inventories accept only food some creature eats (design 11.1). Loading saved contents is never
    /// filtered, so nothing already stored is lost.</item>
    /// </list>
    /// </summary>
    public static class FeedSources
    {
        public const string BurrowFeedSource = "meadworks.containers.feed_source";
        public const float SourceRadius = 0.6f;

        private static readonly List<Container> Loaded = new List<Container>();
        private static readonly ConditionalWeakTable<Inventory, Container> TroughInventories = new ConditionalWeakTable<Inventory, Container>();
        private static readonly HashSet<string> EdibleShared = new HashSet<string>(StringComparer.Ordinal);
        private static ZNetScene? edibleScene;
        private static readonly System.Version ContractVersion = new System.Version(1, 0);
        [ThreadStatic] private static int loading;

        public static IReadOnlyList<Container> All
        {
            get
            {
                Loaded.RemoveAll(container => container == null);
                return Loaded;
            }
        }

        [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
        private static class Register
        {
            [HarmonyPostfix]
            private static void Postfix(Container __instance) => NativeFeeding.Run("feed source", Track, __instance);
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.OnDestroy))]
        private static class WorldExit
        {
            [HarmonyPostfix]
            private static void Postfix() { Loaded.Clear(); EdibleShared.Clear(); edibleScene = null; }
        }

        private static void Track(Container container)
        {
            ZNetView view = container.m_nview;
            if (view == null || view.GetZDO() == null) return;
            if (TroughPieces.IsTroughHash(view.GetZDO().GetPrefab()) && container.GetInventory() != null)
                TroughInventories.AddOrUpdate(container.GetInventory(), container);
            TakeTransport.RegisterSource(container);
            if (container.gameObject.GetComponent<FeedSourceLife>() == null)
                container.gameObject.AddComponent<FeedSourceLife>().Source = container;
            if (!Loaded.Contains(container)) Loaded.Add(container);
        }

        /// <summary>Takes a destroyed container out of the list (<see cref="FeedSourceLife"/>).</summary>
        public static void Forget(Container container) => Loaded.Remove(container);

        /// <summary>Re-tracks containers that woke before the feeding patches were installed.</summary>
        public static void TrackLoaded()
        {
            foreach (Container container in UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None)) Track(container);
        }

        public static bool IsUsable(Container container) =>
            container != null && container.m_nview != null && container.m_nview.IsValid() && container.GetInventory() != null;

        /// <summary>The trough or container still holds one item of this shared name (any world level).</summary>
        public static bool Holds(Container container, string sharedName) =>
            IsUsable(container) && !string.IsNullOrEmpty(sharedName) && container.GetInventory().HaveItem(sharedName, false);

        /// <summary>Container feeding (design 4.2, 8.1): an eligible non-trough container this machine sees.</summary>
        public static bool IsFeedContainer(Container container)
        {
            if (!IsUsable(container) || TroughPieces.IsTrough(container) || ShepherdSources.ContainersOwnedElsewhere) return false;
            ZDO zdo = container.m_nview.GetZDO();
            Piece piece = container.GetComponentInParent<Piece>();
            var facts = new ContainerFacts(false, container.m_privacy == Container.PrivacySetting.Private,
                piece != null && piece.IsPlacedByPlayer() && container.GetComponentInParent<TombStone>() == null,
                BurrowSays(container), zdo.GetBool(ContainerEligibility.MarkKey), zdo.GetBool(ContainerEligibility.DenyKey));
            return ContainerEligibility.IsFeedSource(facts, Selection());
        }

        private static ContainerSelectionMode Selection() =>
            ShepherdSettings.Selection.Value == ContainerSelection.MarkedOnly ? ContainerSelectionMode.MarkedOnly : ContainerSelectionMode.AllExceptDenied;

        /// <summary>
        /// Burrow's <c>meadworks.containers.feed_source</c> (<c>Func&lt;object,bool&gt;</c>, 1.0) through Crucible
        /// capabilities; null when Burrow does not provide it or the query fails, so Shepherd's own mark decides.
        /// </summary>
        private static bool? BurrowSays(Container container)
        {
            try
            {
                return CruciblePlugin.Services.Capabilities.TryResolve<Func<object, bool>>(BurrowFeedSource, ContractVersion, out var query) && query != null
                    ? query(container)
                    : (bool?)null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Shared names of every item some loaded creature prefab eats (MonsterAI.m_consumeItems).</summary>
        public static ISet<string> Edible()
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null) return EdibleShared;
            if (edibleScene == scene && EdibleShared.Count > 0) return EdibleShared;
            EdibleShared.Clear();
            edibleScene = scene;
            foreach (GameObject prefab in scene.m_prefabs)
            {
                MonsterAI ai = prefab != null ? prefab.GetComponent<MonsterAI>() : null!;
                if (ai == null || ai.m_consumeItems == null) continue;
                foreach (ItemDrop item in ai.m_consumeItems)
                    if (item != null) EdibleShared.Add(item.m_itemData.m_shared.m_name);
            }
            return EdibleShared;
        }

        /// <summary>A trough refuses an item no creature eats (feed-only filter).</summary>
        private static bool Refuses(Inventory inventory, ItemDrop.ItemData item)
        {
            if (loading > 0 || inventory == null || item == null || !TroughInventories.TryGetValue(inventory, out _)) return false;
            return !TroughFilter.Accepts(item.m_shared.m_name, Edible());
        }

        /// <summary>
        /// Saved contents are never filtered: Inventory.Load re-adds every saved item through the positioned add the filter
        /// guards (game 1.0.17), so the filter stands aside for the whole load, nested or not. Both Load overloads.
        /// </summary>
        [HarmonyPatch]
        private static class Loading
        {
            private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.Load), new[] { typeof(ZPackage) });
                yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.Load), new[] { typeof(ZPackage), typeof(bool) });
            }

            [HarmonyPrefix]
            private static void Prefix() => loading++;

            [HarmonyFinalizer]
            private static void Finalizer() => loading--;
        }

        private static void Tell()
        {
            if (Player.m_localPlayer != null && Plugin.AllowsUi)
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, ShepherdLocalization.Text("trough_food_only"));
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem), typeof(ItemDrop.ItemData), typeof(int))]
        private static class CanAdd
        {
            [HarmonyPrefix]
            private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (!NativeFeeding.Run("trough filter", static c => Refuses(c.inventory, c.item), (inventory: __instance, item), false)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData))]
        private static class Add
        {
            [HarmonyPrefix]
            private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (!NativeFeeding.Run("trough filter", static c => Refuses(c.inventory, c.item), (inventory: __instance, item), false)) return true;
                __result = false;
                Tell();
                return false;
            }
        }

        // Game 1.0.17: the positioned add (drag, split, move to slot) is AddItem(item, amount, x, y, skipValidPositionCheck).
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool))]
        private static class AddAt
        {
            [HarmonyPrefix]
            private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (!NativeFeeding.Run("trough filter", static c => Refuses(c.inventory, c.item), (inventory: __instance, item), false)) return true;
                __result = false;
                Tell();
                return false;
            }
        }
    }

    /// <summary>Added to every tracked container: when its object is destroyed, it leaves the loaded list.</summary>
    public class FeedSourceLife : MonoBehaviour
    {
        public Container? Source;

        private void OnDestroy()
        {
            if (Source != null) FeedSources.Forget(Source);
        }
    }
}
