// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The Elder's roots caught in a Nightfall tornado. On the boss's owner, which moves the tornadoes
    /// (<see cref="TornadoHunt"/>), a root (<see cref="RootPrefab"/>) a formed tornado passes over is picked up and
    /// thrown in a random direction, landing between <see cref="Shortest"/> of `toss distance` and all of it away on flat
    /// ground, <see cref="Lift"/> metres up at the top of its arc (0: never). A root takes no fall damage, as no creature
    /// does. One funnel passing over a root throws it once: a root is tossed at most once a wave. A root is moved by
    /// its own owner, so this machine takes it over first if it must - the Elder raises its roots on this same machine,
    /// so it rarely has to; the flight then reaches every client through the root's position, as any creature's does.
    /// </summary>
    internal sealed class TornadoToss
    {
        /// <summary>The Elder's root, and the name its prefab carries, checked first because it costs nothing.</summary>
        public const string RootPrefab = "TentaRoot";
        private const string RootName = "$enemy_root";

        /// <summary>Seconds between looks for roots under the funnels.</summary>
        private const float CheckEvery = 0.2f;

        /// <summary>How far past the funnel's foot a root's body still counts as under it, in metres.</summary>
        private const float Reach = 0.6f;

        /// <summary>How high a throw carries a root above where it was picked up, in metres.</summary>
        private const float Lift = 4f;

        /// <summary>The shortest throw, as a share of `toss distance`.</summary>
        private const float Shortest = 0.6f;

        private readonly HashSet<ZDOID> _tossed = new HashSet<ZDOID>();
        private float _timer;

        /// <summary>Each frame on the owner while the wave's tornadoes hunt.</summary>
        public void Tick(List<TornadoPath> paths, TornadoShape shape)
        {
            if ((_timer -= Time.deltaTime) > 0f)
            {
                return;
            }
            _timer = CheckEvery;
            float distance = AspectMath.Power(Aspect.Nightfall, Fields.TossDistance);
            if (distance <= 0f)
            {
                return;
            }
            foreach (Character character in Character.GetAllCharacters())
            {
                if (IsRoot(character) && Under(character, paths, shape) && _tossed.Add(character.GetZDOID()))
                {
                    Toss(character, distance);
                }
            }
        }

        /// <summary>The wave is over: its roots may be tossed again by the next one.</summary>
        public void Clear() => _tossed.Clear();

        private static bool IsRoot(Character character) =>
            character != null && character.m_name == RootName && !character.IsDead()
            && Utils.GetPrefabName(character.gameObject) == RootPrefab;

        private static bool Under(Character root, List<TornadoPath> paths, TornadoShape shape)
        {
            float reach = shape.BaseRadius + Reach;
            foreach (TornadoPath path in paths)
            {
                if (StormTargets.FlatDistance(root.transform.position, path.Position) <= reach)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Toss(Character root, float distance)
        {
            ZNetView view = root.m_nview;
            if (view == null || !view.IsValid())
            {
                return;
            }
            if (!view.IsOwner())
            {
                view.ClaimOwnership(); // only the owner's physics moves it; the Elder's roots are usually ours already
            }
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            root.ForceJump(Throw(direction, Random.Range(distance * Shortest, distance)), effects: false);
        }

        // The launch that carries it `reach` metres across flat ground, peaking `Lift` metres up under the game's gravity.
        // A root has no speed of its own, so nothing in the air slows it.
        private static Vector3 Throw(Vector3 direction, float reach)
        {
            float gravity = Mathf.Max(1f, -Physics.gravity.y);
            float up = Mathf.Sqrt(2f * gravity * Lift);
            float flight = 2f * up / gravity;
            return direction * (reach / flight) + Vector3.up * up;
        }
    }
}
