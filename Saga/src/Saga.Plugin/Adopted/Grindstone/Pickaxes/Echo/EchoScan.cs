// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Echo/EchoScan.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Finds the Echo's target on the miner's own client: the nearest loaded ore deposit within a radius.
    /// <list type="bullet">
    /// <item><b>Search:</b> a walk over the objects this client has loaded (ZNetScene.m_instances, one entry per
    /// networked object), with a squared-distance check on each ZDO's position, collects the ore deposits within the
    /// radius plus <see cref="Margin"/> metres. Objects in reach are classified (<see cref="Rock.Find"/>, a cached lookup
    /// per prefab), and prefabs that are no rock, or a rock that is no ore or has a Beacon, are remembered by prefab hash
    /// (until "Plain Stone Items" changes), so later walks pass them with one set lookup.</item>
    /// <item><b>Few walks:</b> a swing only picks the nearest target out of the last walk's deposits. A new walk happens
    /// once that list is <see cref="Refresh"/> seconds old, the miner moved more than <see cref="Margin"/> metres from
    /// where it was made, or the radius changed, so mining one spot with nothing in range walks the loaded objects every
    /// few seconds rather than on every swing. Nothing per frame.</item>
    /// <item><b>A target:</b> an ore deposit (<see cref="Rock.IsOre"/>) that still exists here, without a Beacon (buried
    /// silver and beacon mud piles stay the Wishbone's job), with at least one intact chunk when it breaks chunk by
    /// chunk, and not one of the rocks the swing just hit.</item>
    /// <item><b>One deposit:</b> an intact deposit and its fractured form are the same deposit (same kind, same spot),
    /// so a fractured form the swing's own hit just spawned is skipped with the intact deposit it replaced.</item>
    /// </list>
    /// </summary>
    internal static class EchoScan
    {
        /// <summary>How close two rocks of one kind stand when they are one deposit: a fractured form spawns at the intact deposit's exact spot.</summary>
        private const float SameSpot = 1f;

        /// <summary>Metres beyond the radius a walk collects: its list serves while the miner stays this near where it was made.</summary>
        private const float Margin = 8f;

        /// <summary>Seconds a walk's list serves, so deposits loaded since then are seen soon.</summary>
        private const float Refresh = 5f;

        /// <summary>Prefabs that are never a target: no rock, no ore, or a Beacon's.</summary>
        private static readonly HashSet<int> NeverTargets = new HashSet<int>();

        private static readonly List<Rock> Deposits = new List<Rock>();
        private static string skippedFor;
        private static ZNetScene walkedScene;
        private static Vector3 walkedFrom;
        private static float walkedRadius;
        private static float walkedAt = float.NegativeInfinity;

        /// <summary>The nearest target within <paramref name="radius"/> of <paramref name="from"/>, skipping <paramref name="hit"/>; null when there is none.</summary>
        public static Rock Nearest(Vector3 from, float radius, IReadOnlyList<Rock> hit)
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null)
                return null;
            if (Stale(scene, from, radius))
                Walk(scene, from, radius);
            Rock best = null;
            float bestSqr = radius * radius;
            foreach (Rock rock in Deposits)
            {
                float sqr = rock.IsValid ? (rock.Position - from).sqrMagnitude : float.PositiveInfinity;
                if (sqr <= bestSqr && IsTarget(rock, hit))
                {
                    best = rock;
                    bestSqr = sqr;
                }
            }
            return best;
        }

        private static bool Stale(ZNetScene scene, Vector3 from, float radius) =>
            !ReferenceEquals(scene, walkedScene) || radius != walkedRadius || Time.time - walkedAt >= Refresh
            || (from - walkedFrom).sqrMagnitude > Margin * Margin;

        /// <summary>Collects the ore deposits loaded within the radius plus the margin of <paramref name="from"/>.</summary>
        private static void Walk(ZNetScene scene, Vector3 from, float radius)
        {
            ForgetSkipsOnChange();
            Deposits.Clear();
            float reach = (radius + Margin) * (radius + Margin);
            foreach (KeyValuePair<ZDO, ZNetView> entry in scene.m_instances)
            {
                if ((entry.Key.GetPosition() - from).sqrMagnitude > reach)
                    continue;
                Rock rock = Classify(entry.Key, entry.Value);
                if (rock != null)
                    Deposits.Add(rock);
            }
            walkedScene = scene;
            walkedFrom = from;
            walkedRadius = radius;
            walkedAt = Time.time;
        }

        /// <summary>Which rocks are ore follows "Plain Stone Items" (<see cref="RockCatalog"/>): skips are learnt again when it changes.</summary>
        private static void ForgetSkipsOnChange()
        {
            string plainStone = PickaxeSettings.PlainStoneItems.Value ?? "";
            if (plainStone == skippedFor)
                return;
            NeverTargets.Clear();
            skippedFor = plainStone;
        }

        /// <summary>The ore deposit a loaded object is, without a Beacon; null for anything else, remembered per prefab.</summary>
        private static Rock Classify(ZDO zdo, ZNetView view)
        {
            int prefab = zdo.GetPrefab();
            if (view == null || NeverTargets.Contains(prefab))
                return null;
            Rock rock = Rock.Find(view.gameObject);
            if (rock != null && rock.IsOre && !rock.HasBeacon)
                return rock;
            NeverTargets.Add(prefab);
            return null;
        }

        private static bool IsTarget(Rock rock, IReadOnlyList<Rock> hit) =>
            rock.IsOre && !rock.HasBeacon && !JustHit(rock, hit) && rock.IsValid && HasIntact(rock);

        /// <summary>A rock that breaks chunk by chunk has a chunk left; a single piece or an intact deposit is whole while it exists.</summary>
        private static bool HasIntact(Rock rock) => !rock.HasChunks || RockChunks.Left(rock) > 0;

        private static bool JustHit(Rock rock, IReadOnlyList<Rock> hit)
        {
            for (int i = 0; i < hit.Count; i++)
            {
                if (hit[i] != null && SameDeposit(rock, hit[i]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The same rock, or an intact deposit and its fractured form: same kind, standing on the same spot. A rock the
        /// swing destroyed this frame (an intact deposit replaced by its fractured form) still has its transform.
        /// </summary>
        private static bool SameDeposit(Rock rock, Rock other)
        {
            if (ReferenceEquals(rock.Target, other.Target))
                return true;
            if (other.Target == null || rock.Kind != other.Kind)
                return false;
            return (rock.Position - other.Position).sqrMagnitude < SameSpot * SameSpot;
        }
    }
}
