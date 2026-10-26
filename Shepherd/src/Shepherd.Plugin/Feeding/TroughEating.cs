// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Companions/FeederEating.cs
// and OpenKeep/OpenKeep/src/Homestead/PetEating.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: troughs and optional containers in one search (portable FeedSearch); wild feeding,
// disallow lists, Require Move To Feed, Move Proximity and Give Up Seconds from Crucible settings; the meal is
// taken through the owner-routed take protocol instead of an immediate local removal. 0.1.7: the eat spot moves round the
// source to the nearest side with a full path, remembered per source and re-checked every two seconds; an animal in a
// navmesh pocket with no full path to any side walks straight to its own side when it is close and the way is clear.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ExtraPotions.Shepherd.Feeding;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Feeding
{
    /// <summary>
    /// Hungry animals eat from troughs (and, when the server allows it, containers). The game's
    /// <c>MonsterAI.UpdateConsumeItem</c> runs on the creature's owner: every m_consumeSearchInterval it looks for food on
    /// the ground, walks there and eats it. Only when it found nothing (ground first, A3) a low-priority postfix does the
    /// same with feed sources:
    /// <list type="bullet">
    /// <item>At the game's search moment (its timer has just been reset to 0), an eligible animal (tamed, being tamed, or
    /// wild with Feed Wild Animals; not disallowed; hungry, rechecked so another feeding mod that already fed wins) picks a
    /// source holding food it eats: troughs within Trough Range first, then eligible containers within Container Range,
    /// nearest first, with a path to the source's near side (at most three path checks).</item>
    /// <item>While it has one, the postfix walks it there and returns true, as the game does while it walks to food. With
    /// Require Move To Feed off it eats from where it stands.</item>
    /// <item>Eating sends one owner-routed take (<see cref="TakeTransport"/>); the meal applies when the source owner's
    /// answer arrives. A source that empties or unloads, an animal that stops being hungry or finds ground food, or a
    /// walk longer than Give Up Seconds drops the target.</item>
    /// </list>
    /// </summary>
    public static class TroughEating
    {
        /// <summary>Degrees within which the creature must face the source to eat, as for food on the ground.</summary>
        private const float EatAngle = 20f;

        private static readonly ConditionalWeakTable<MonsterAI, Meal> Meals = new ConditionalWeakTable<MonsterAI, Meal>();

        private sealed class Meal
        {
            public Container Source = null!;
            public ItemDrop Food = null!;
            public float Since;
            public Vector3 Spot;
            public float Checked;
            /// <summary>No side had a full path: the animal walks straight to its own-side spot.</summary>
            public bool Direct;
        }

        /// <summary>The world bearing (degrees) of the side a source was last reached from, tried first by the next animal.</summary>
        private sealed class GoodSide
        {
            public float Bearing;
        }

        private static readonly ConditionalWeakTable<Container, GoodSide> Sides = new ConditionalWeakTable<Container, GoodSide>();

        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateConsumeItem))]
        private static class Consume
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Low)]
            private static void Postfix(MonsterAI __instance, Humanoid humanoid, float dt, ref bool __result)
            {
                if (__result)
                {
                    Meals.Remove(__instance);
                    return;
                }
                if (!NativeFeeding.FeedingActive || !Wants(__instance))
                    return;
                __result = NativeFeeding.Run("trough eating", static call => Update(call.ai, call.dt), (ai: __instance, dt), false);
            }
        }

        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.Awake))]
        private static class AnimalAwake
        {
            [HarmonyPostfix]
            private static void Postfix(MonsterAI __instance)
            {
                if (__instance.m_consumeItems != null && __instance.m_consumeItems.Count > 0)
                    NativeFeeding.Run("shepherd.meal", TakeTransport.RegisterAnimal, __instance);
            }
        }

        /// <summary>Re-registers meal handlers on animals that woke before the feeding patches were installed.</summary>
        public static void TrackLoaded()
        {
            foreach (MonsterAI ai in Object.FindObjectsByType<MonsterAI>(FindObjectsSortMode.None))
                if (ai.m_consumeItems != null && ai.m_consumeItems.Count > 0) TakeTransport.RegisterAnimal(ai);
        }

        /// <summary>Cheap test before any work: a creature with food it eats, at the search moment or with a target.</summary>
        private static bool Wants(MonsterAI ai) =>
            ai.m_consumeItems != null && ai.m_consumeItems.Count > 0
            && (ai.m_consumeSearchTimer == 0f || Meals.TryGetValue(ai, out _));

        private static bool Update(MonsterAI ai, float dt)
        {
            if (!Eligible(ai) || TakeTransport.HasPending(ai.m_nview))
            {
                Meals.Remove(ai);
                return false;
            }
            if (ai.m_consumeSearchTimer == 0f)
                Choose(ai);
            return Meals.TryGetValue(ai, out Meal meal) && Approach(ai, meal, dt);
        }

        private static bool Eligible(MonsterAI ai)
        {
            Tameable tameable = ai.m_tamable;
            Character character = ai.m_character;
            if (character == null || ai.m_nview == null || !ai.m_nview.IsValid() || !ai.m_nview.IsOwner()) return false;
            if (tameable == null)
            {
                bool tamed = character.IsTamed();
                var young = new AnimalFacts(tamed, false, false, YoungFeeding.Due(ai) && !(tamed && ShepherdSettings.NoHunger.Value), character.IsDead(),
                    NativeFeeding.AnimalDisallowed(Herd.PrefabName(character)));
                return FeedingRules.EligibleYoung(young, ShepherdSettings.FeedWild.Value, ai.GetComponent<Growup>() != null, YoungFeeding.Supports(ai));
            }
            var facts = new AnimalFacts(character.IsTamed(), Herd.IsBeingTamed(tameable), true, tameable.IsHungry(), character.IsDead(),
                NativeFeeding.AnimalDisallowed(Herd.PrefabName(character)));
            return FeedingRules.Eligible(facts, ShepherdSettings.FeedWild.Value);
        }

        private static void Choose(MonsterAI ai)
        {
            Meals.Remove(ai);
            Vector3 position = ai.transform.position;
            float troughRange = HusbandryCompanionSettings.FeederRange.Value;
            bool containers = ShepherdSettings.ContainerFeeding.Value;
            float containerRange = ShepherdSettings.ContainerRange.Value;
            var sources = new Dictionary<string, (Container Source, ItemDrop Food)>();
            var candidates = new List<FeedCandidate>();
            IReadOnlyList<Container> all = FeedSources.All;
            for (int index = 0; index < all.Count; index++)
            {
                Container source = all[index];
                if (!FeedSources.IsUsable(source)) continue;
                bool trough = TroughPieces.IsTrough(source);
                float range = trough ? troughRange : containerRange;
                float distance = (source.transform.position - position).sqrMagnitude;
                if (distance > range * range || (!trough && (!containers || !FeedSources.IsFeedContainer(source)))) continue;
                ItemDrop food = FoodIn(source, ai);
                if (food == null) continue;
                string id = index.ToString();
                sources[id] = (source, food);
                candidates.Add(new FeedCandidate(id, trough ? FeederKind.Trough : FeederKind.Container, distance, food.gameObject.name));
            }
            Vector3 spot = default;
            FeedCandidate? chosen = FeedSearch.Choose(candidates, candidate => ReachableSpot(ai, sources[candidate.Id].Source, out spot));
            bool direct = false;
            if (chosen == null)
            {
                // A navmesh pocket (tile seam) gives only partial paths: the nearest source counts when its own-side spot
                // is close and nothing solid stands between.
                candidates.Sort((a, b) => a.DistanceSquared.CompareTo(b.DistanceSquared));
                foreach (FeedCandidate candidate in candidates)
                {
                    var source = sources[candidate.Id].Source;
                    bool wallReach = WallSpot(ai, source, out spot);
                    if (!wallReach && !DirectSpot(ai, source, out spot)) continue;
                    chosen = candidate;
                    direct = !wallReach;
                    break;
                }
            }
            if (chosen != null)
                Meals.Add(ai, new Meal { Source = sources[chosen.Id].Source, Food = sources[chosen.Id].Food, Since = Time.time, Spot = spot, Checked = Time.time, Direct = direct });
        }

        private static readonly int SolidMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");

        private static bool WallSpot(MonsterAI ai, Container source, out Vector3 spot)
        {
            spot = Spot(ai, source);
            if (!ShepherdSettings.RequireMove.Value || !(ShepherdSettings.ReachThroughWalls.Value > 0f)) return false;
            bool Reach(Vector3 footing) => FeedMath.InReach(global::Utils.DistanceXZ(footing, source.transform.position),
                FeedSources.SourceRadius, ai.m_character.GetRadius(), ShepherdSettings.MoveProximity.Value,
                source.transform.position.y - footing.y) && FeedWalls.From(ai, source, footing, requireWall: true);
            if (Reach(ai.transform.position)) return true;
            var path = new List<Vector3>();
            if (Pathfinding.instance == null || !Pathfinding.instance.GetPath(ai.transform.position, spot, path, ai.m_pathAgentType)
                || path.Count == 0 || !Reach(path[path.Count - 1])) return false;
            // A partial native path ends on the accessible side of the wall, rather than at the food source.
            spot = path[path.Count - 1];
            return true;
        }

        /// <summary>The creature's own-side spot when it is within <see cref="FeedMath.DirectWalkMetres"/>, about level and the way is clear.</summary>
        private static bool DirectSpot(MonsterAI ai, Container source, out Vector3 spot)
        {
            spot = Spot(ai, source);
            Vector3 from = ai.transform.position;
            if (!FeedMath.MayWalkDirect(global::Utils.DistanceXZ(from, spot), spot.y - from.y)) return false;
            // Only a pocket: navmesh under both ends with a path that stops short. Where no navmesh is built yet (a fresh
            // area, the game's ground search also finds no path) the animal waits like the game does.
            if (Pathfinding.instance == null || !Pathfinding.instance.GetPath(from, spot, null, ai.m_pathAgentType)) return false;
            Vector3 lift = Vector3.up * 0.5f;
            Transform own = source.transform;
            foreach (RaycastHit hit in Physics.RaycastAll(from + lift, (spot + lift) - (from + lift), Vector3.Distance(from, spot), SolidMask, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(own) && hit.collider.GetComponentInParent<Character>() == null)
                    return false;
            return true;
        }

        /// <summary>The first of the creature's foods (its own order) the source holds and that is not disallowed.</summary>
        private static ItemDrop FoodIn(Container source, MonsterAI ai)
        {
            var byName = new Dictionary<string, ItemDrop>();
            var names = new List<string>();
            foreach (ItemDrop item in ai.m_consumeItems)
            {
                if (item == null || item.gameObject == null || item.m_itemData?.m_shared == null
                    || string.IsNullOrEmpty(item.m_itemData.m_shared.m_name) || byName.ContainsKey(item.gameObject.name)) continue;
                byName[item.gameObject.name] = item;
                names.Add(item.gameObject.name);
            }
            string? chosen = FeedingRules.FirstFood(names, name => FeedSources.Holds(source, byName[name].m_itemData.m_shared.m_name), NativeFeeding.DisallowedFeed);
            return chosen != null ? byName[chosen] : null!;
        }

        /// <summary>The point beside the source on the creature's side, where a creature of its size stands to eat.</summary>
        private static Vector3 Spot(MonsterAI ai, Container source)
        {
            Vector3 centre = source.transform.position;
            Vector3 away = ai.transform.position - centre;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = Vector3.forward;
            return centre + away.normalized * (float)FeedMath.StandOff(FeedSources.SourceRadius, ai.m_character.GetRadius());
        }

        /// <summary>
        /// A side of the source the creature has a full path to: the side last reached (any animal), then its own side and
        /// the others nearest first. Near a navmesh tile seam the sides facing the seam can stop short while the others work.
        /// </summary>
        private static bool ReachableSpot(MonsterAI ai, Container source, out Vector3 spot)
        {
            Vector3 centre = source.transform.position;
            Vector3 away = ai.transform.position - centre;
            away.y = 0f;
            float own = away.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
            float standOff = (float)FeedMath.StandOff(FeedSources.SourceRadius, ai.m_character.GetRadius());
            GoodSide side = Sides.GetOrCreateValue(source);
            bool known = side.Bearing != 0f;   // 0 = no side yet; a side at 0 is stored as 360
            if (known && ai.HavePath(spot = SpotAt(centre, side.Bearing, standOff)))
                return true;
            foreach (int step in FeedMath.RingOrder(FeedMath.RingBearings))
            {
                float bearing = own + step * 360f / FeedMath.RingBearings;
                if (ai.HavePath(spot = SpotAt(centre, bearing, standOff)))
                {
                    side.Bearing = bearing == 0f ? 360f : bearing;
                    return true;
                }
            }
            spot = default;
            return false;
        }

        private static Vector3 SpotAt(Vector3 centre, float bearing, float standOff) =>
            centre + Quaternion.Euler(0f, bearing, 0f) * Vector3.forward * standOff;

        private static bool Approach(MonsterAI ai, Meal meal, float dt)
        {
            if (meal.Source == null || meal.Food == null || meal.Food.m_itemData?.m_shared == null
                || string.IsNullOrEmpty(meal.Food.m_itemData.m_shared.m_name)
                || FeedMath.GaveUp(meal.Since, Time.time, ShepherdSettings.GiveUp.Value)
                || !FeedSources.Holds(meal.Source, meal.Food.m_itemData.m_shared.m_name))
            {
                Meals.Remove(ai);
                return false;
            }
            Vector3 centre = meal.Source.transform.position;
            float distance = global::Utils.DistanceXZ(ai.transform.position, centre);
            bool requireMove = ShepherdSettings.RequireMove.Value;
            float proximity = ShepherdSettings.MoveProximity.Value;
            float radius = ai.m_character.GetRadius();
            float range = TroughPieces.IsTrough(meal.Source) ? HusbandryCompanionSettings.FeederRange.Value : ShepherdSettings.ContainerRange.Value;
            if (!FeedMath.MayEatFrom(distance, FeedSources.SourceRadius, radius, proximity, requireMove, range, centre.y - ai.transform.position.y)
                || (requireMove && !FeedWalls.InReach(ai, meal.Source)))
            {
                if (Time.time - meal.Checked >= FeedMath.SpotRecheckSeconds)
                {
                    meal.Checked = Time.time;
                    if (ai.HavePath(meal.Spot) || ReachableSpot(ai, meal.Source, out meal.Spot))
                        meal.Direct = false;
                    else if (DirectSpot(ai, meal.Source, out meal.Spot))
                        meal.Direct = true;
                    else
                    {
                        Meals.Remove(ai);
                        return false;
                    }
                }
                if (meal.Direct)
                {
                    // Out of the pocket on foot: the game's path walk stands still on a partial path.
                    if (!ai.MoveAndAvoid(dt, meal.Spot, proximity, false))
                        return true;
                }
                else if (!ai.MoveTo(dt, meal.Spot, proximity, false))
                    return true;
                // MoveTo also reports arrival when it finds no path: only a creature really within reach eats.
                if (!FeedMath.InReach(global::Utils.DistanceXZ(ai.transform.position, centre), FeedSources.SourceRadius, radius, proximity,
                    centre.y - ai.transform.position.y))
                {
                    Meals.Remove(ai);
                    return false;
                }
            }
            if (requireMove && !FeedWalls.InReach(ai, meal.Source))
            {
                Meals.Remove(ai);
                return false;
            }
            ai.StopMoving();
            ai.LookAt(centre);
            if (ai.IsLookingAt(centre, EatAngle))
            {
                Meals.Remove(ai);
                TakeTransport.Request(ai, meal.Source, meal.Food);
            }
            return true;
        }
    }
}
