// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Shepherd.Feeding;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using ExtraPotions.Shepherd.Take;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Feeding
{
    /// <summary>
    /// The native side of the owner-routed take (design 3.3), built on the portable <see cref="TakeRequester"/> and
    /// <see cref="TakeServer"/>:
    /// <list type="number">
    /// <item>The animal owner sends <c>shepherd.take</c> on the source's view; the game routes it to the source owner.</item>
    /// <item>The source owner decides, and removes one item only when the meal can be sent. It then answers
    /// <c>shepherd.meal</c> routed to whoever owns the animal now. A grant that cannot be sent leaves the food in
    /// the trough. A request already paid for is sent again and does not take a second item.</item>
    /// <item>That machine applies the meal once per request id through <c>m_onConsumedItem</c>, the consume effects
    /// and the <c>consume</c> trigger. After 10 s the same request is sent again.</item>
    /// </list>
    /// Shepherd never claims ownership of a source or an animal. Handlers are registered only where no foreign handler
    /// exists and are removed when the feeding patches stop.
    /// </summary>
    public static class TakeTransport
    {
        private static readonly int TakeHash = TakeProtocol.TakeRpc.GetStableHashCode();
        private static readonly int MealHash = TakeProtocol.MealRpc.GetStableHashCode();
        private static readonly List<Lease> Leases = new List<Lease>();
        private static readonly Dictionary<TakeRequestId, WeakReference<Container>> Sources = new Dictionary<TakeRequestId, WeakReference<Container>>();
        private static readonly TakeServer Server = new TakeServer();
        private static TakeRequester? requester;
        private static long requesterSession;

        private static double Now => Time.realtimeSinceStartup;

        private static long LocalPeer => ZDOMan.GetSessionID();

        private static TakeRequester Requester
        {
            get
            {
                long session = LocalPeer;
                if (requester == null || requesterSession != session)
                {
                    requester = new TakeRequester(session);
                    requesterSession = session;
                }
                return requester;
            }
        }

        public static NetObjectId IdOf(ZNetView view)
        {
            ZDOID id = view.GetZDO().m_uid;
            return new NetObjectId(id.UserID, id.ID);
        }

        public static bool HasPending(ZNetView animal) => animal != null && animal.IsValid() && Requester.IsBlocking(IdOf(animal), Now);

        /// <summary>On a container that woke: answer take requests while this machine owns it.</summary>
        public static void RegisterSource(Container container) =>
            RegisterOn(container.m_nview, TakeProtocol.TakeRpc, TakeHash, (long sender, string payload) => NativeFeeding.Run("shepherd.take", () => ReceiveTake(container, payload)));

        /// <summary>On an animal that woke: apply meals while this machine owns it.</summary>
        public static void RegisterAnimal(MonsterAI ai)
        {
            RegisterOn(ai.m_nview, TakeProtocol.MealRpc, MealHash, (long sender, string payload) => NativeFeeding.Run("shepherd.meal", () => ReceiveMeal(ai, payload)));
            YoungFeeding.Track(ai);
        }

        private static void RegisterOn(ZNetView view, string name, int hash, Action<long, string> handler)
        {
            if (view == null || !view.IsValid() || view.m_functions.ContainsKey(hash)) return;
            Leases.RemoveAll(lease => !lease.View.TryGetTarget(out ZNetView prior) || prior == null);
            view.Register<string>(name, handler);
            Leases.Add(new Lease(view, hash, view.m_functions[hash]));
        }

        /// <summary>Step 1: the animal owner asks for one item. Returns false when a request is still pending.</summary>
        public static bool Request(MonsterAI ai, Container source, ItemDrop food)
        {
            ZNetView animal = ai.m_nview;
            if (animal == null || !animal.IsValid() || !animal.IsOwner() || !FeedSources.IsUsable(source)) return false;
            TakeRequest? request = Requester.TryBegin(IdOf(animal), food.gameObject.name, Now);
            if (request == null) return false;
            Container target = source;
            if (Sources.TryGetValue(request.Id, out WeakReference<Container>? remembered) && remembered != null
                && remembered.TryGetTarget(out Container? prior) && prior != null)
                target = prior;
            else
                Sources[request.Id] = new WeakReference<Container>(source);
            ZNetView? view = target.m_nview;
            if (view == null)
            {
                NativeFeeding.Verbose("take not sent: source has no view");
                return true;
            }
            switch (TakeRouting.ForTake(view.IsValid(), view.GetZDO().GetOwner(), LocalPeer))
            {
                case TakeRoute.Local:
                    Serve(target, request);
                    break;
                case TakeRoute.Remote:
                    view.InvokeRPC(TakeProtocol.TakeRpc, TakeCodec.Encode(request));
                    break;
                default:
                    NativeFeeding.Verbose("take not sent: source has no owner");
                    break;
            }
            return true;
        }

        private static void ReceiveTake(Container source, string payload)
        {
            if (TakeCodec.TryDecodeTake(payload, out TakeRequest? request) && request != null) Serve(source, request);
        }

        /// <summary>Steps 3 and 4 on the source owner. Food leaves the trough only when the meal can be sent.</summary>
        private static void Serve(Container source, TakeRequest request)
        {
            ItemDrop food = FoodPrefab(request.ItemName);
            string shared = food != null ? food.m_itemData.m_shared.m_name : "";
            bool exists = FeedSources.IsUsable(source);
            bool owner = exists && source.m_nview.IsOwner();
            bool enabled = exists && NativeFeeding.FeedingActive
                && (TroughPieces.IsTrough(source) || (ShepherdSettings.ContainerFeeding.Value && FeedSources.IsFeedContainer(source)));
            var facts = new TroughFacts(exists, owner, enabled, exists && FeedSources.Holds(source, shared), NativeFeeding.FoodDisallowed(request.ItemName));
            TakeVerdict verdict = Server.Decide(request, facts, Now);
            if (verdict == TakeVerdict.Resend)
            {
                if (!Dispatch(request))
                    NativeFeeding.Verbose("meal " + request.Id + " resend waiting: animal not reachable");
                return;
            }
            if (!TakeVerdicts.RemovesItem(verdict))
            {
                NativeFeeding.Verbose("take " + request.Id + " refused: " + verdict);
                return;
            }
            if (!Reachable(request))
            {
                Server.Release(request.Id);
                NativeFeeding.Verbose("take " + request.Id + " kept: animal not reachable");
                return;
            }
            Inventory? inventory = source.GetInventory();
            int before = Stacks(inventory, shared);
            if (inventory == null || before < 1)
            {
                Server.Release(request.Id);
                NativeFeeding.Verbose("take " + request.Id + " kept: food was not removed");
                return;
            }
            inventory.RemoveItem(shared, 1, -1, false);
            if (Stacks(inventory, shared) >= before)
            {
                Server.Release(request.Id);
                NativeFeeding.Verbose("take " + request.Id + " kept: food was not removed");
                return;
            }
            Server.Commit(request.Id);
            NativeFeeding.Verbose("take " + request.Id + " granted: one " + request.ItemName + " from " + global::Utils.GetPrefabName(source.gameObject));
            if (!Dispatch(request))
            {
                Server.Release(request.Id);
                if (food != null) Refund(source, food, request);
                else NativeFeeding.Warn("take " + request.Id + " refund failed; one " + request.ItemName + " was removed");
                return;
            }
            // Informational source history records dispatch, not an unacknowledged remote animal meal.
            try { source.m_nview.GetZDO().Set(UI.TroughHeader.LastFeed, Herd.Now.Ticks); }
            catch (Exception error) { NativeFeeding.Warn("feed display timestamp: " + error.GetType().Name); }
        }

        private static bool Reachable(TakeRequest request)
        {
            var id = new ZDOID(request.Animal.User, request.Animal.Id);
            ZDO? zdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(id) : null;
            TakeRoute route = TakeRouting.ForMeal(zdo != null, zdo != null ? zdo.GetOwner() : 0, LocalPeer);
            if (route == TakeRoute.Remote) return true;
            if (route != TakeRoute.Local || zdo == null || ZNetScene.instance == null) return false;
            ZNetView? view = ZNetScene.instance.FindInstance(zdo);
            return view != null && view.GetComponent<MonsterAI>() != null;
        }

        private static int Stacks(Inventory inventory, string shared)
        {
            if (inventory == null || string.IsNullOrEmpty(shared)) return 0;
            int count = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                if (item != null && item.m_shared != null && item.m_shared.m_name == shared) count += item.m_stack;
            return count;
        }

        /// <summary>Sends the meal. False when the animal is gone, ownerless, or not loaded on this machine.</summary>
        private static bool Dispatch(TakeRequest request)
        {
            var meal = new MealMessage(request.Id, request.ItemName);
            var id = new ZDOID(request.Animal.User, request.Animal.Id);
            ZDO? zdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(id) : null;
            switch (TakeRouting.ForMeal(zdo != null, zdo != null ? zdo.GetOwner() : 0, LocalPeer))
            {
                case TakeRoute.Local:
                    ZNetView? view = zdo != null && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(zdo) : null;
                    MonsterAI? ai = view != null ? view.GetComponent<MonsterAI>() : null;
                    if (ai == null) return false;
                    Apply(ai, meal);
                    return true;
                case TakeRoute.Remote:
                    ZRoutedRpc.instance.InvokeRoutedRPC(zdo!.GetOwner(), id, TakeProtocol.MealRpc, TakeCodec.Encode(meal));
                    return true;
                default:
                    return false;
            }
        }

        private static void Refund(Container source, ItemDrop food, TakeRequest request)
        {
            Inventory? inventory = source != null ? source.GetInventory() : null;
            if (inventory == null || food == null || food.m_itemData == null) return;
            ItemDrop.ItemData clone = food.m_itemData.Clone();
            clone.m_dropPrefab = food.gameObject;
            clone.m_stack = 1;
            if (inventory.AddItem(clone))
                NativeFeeding.Verbose("take " + request.Id + " refunded: animal was not reachable");
            else
                NativeFeeding.Warn("take " + request.Id + " refund failed; one " + request.ItemName + " was removed");
        }

        private static void ReceiveMeal(MonsterAI ai, string payload)
        {
            if (TakeCodec.TryDecodeMeal(payload, out MealMessage? meal) && meal != null) Apply(ai, meal);
        }

        /// <summary>Step 4 on the animal owner: at most once per request id on this machine.</summary>
        private static void Apply(MonsterAI ai, MealMessage meal)
        {
            if (ai == null) return;
            ZNetView view = ai.m_nview;
            if (view == null || !view.IsValid()) return;
            if (Requester.OnMeal(meal, IdOf(view), view.IsOwner(), Now) != MealDecision.Apply) return;
            Sources.Remove(meal.Id);
            ItemDrop food = FoodPrefab(meal.ItemName);
            if (food == null) return;
            // An already-paid late/new-owner receipt still applies; pacing only gates the next request.
            YoungFeeding.Stamp(ai);
            ai.m_onConsumedItem?.Invoke(food);
            if (ai.m_character is Humanoid humanoid)
                humanoid.m_consumeItemEffects.Create(ai.transform.position, Quaternion.identity);
            if (ai.m_animator != null) ai.m_animator.SetTrigger("consume");
            NativeFeeding.Verbose(global::Utils.GetPrefabName(ai.gameObject) + " ate one " + meal.ItemName);
        }

        public static ItemDrop FoodPrefab(string prefabName)
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null!;
            return prefab != null ? prefab.GetComponent<ItemDrop>() : null!;
        }

        /// <summary>The reply window does not drop a request. The next ask sends the same id.</summary>
        public static void Expire()
        {
            if (requester == null) return;
            requester.Expire(Now);
        }

        public static void Release()
        {
            YoungFeeding.Release();
            foreach (Lease lease in Leases)
                if (lease.View.TryGetTarget(out ZNetView view) && view != null && view.m_functions.TryGetValue(lease.Hash, out var handler)
                    && ReferenceEquals(handler, lease.Handler)) view.m_functions.Remove(lease.Hash);
            Leases.Clear();
            Sources.Clear();
            requester = null;
        }

        private sealed class Lease
        {
            internal readonly WeakReference<ZNetView> View;
            internal readonly int Hash;
            internal readonly object Handler;
            internal Lease(ZNetView view, int hash, object handler) { View = new WeakReference<ZNetView>(view); Hash = hash; Handler = handler; }
        }
    }
}
