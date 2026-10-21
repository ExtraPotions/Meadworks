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
    /// How much of Nightfall's storming midnight this client shows, from 0 (the game's own sky) to 1, stepped once
    /// per step of the game's environment (<see cref="Patches.NightfallSkyPatch"/>). It rises while the local player
    /// is in the fight - within `range` of any living Nightfall boss loaded here, along the ground; while they lie
    /// dead, the camera stands in for them - and falls once they are not, over <see cref="Fade"/> seconds each way, so
    /// night falls and lifts rather than snapping. A player in the fight stays in it until <see cref="Margin"/> m past
    /// the range, so standing at the edge never flickers. Every Nightfall boss shares this one sky: two at once draw
    /// it once, and it lifts after the last lets go. It steps aside at once for any weather something else holds - a
    /// dungeon's or an arena's own zone, an environment the game or another mod forces, the console's `env` or time
    /// of day - and for a player inside a dungeon, and falls again as night once that ends. Nothing in the game is
    /// changed here, so there is nothing to put back: a step with nothing shown is the game's own sky.
    /// </summary>
    internal static class NightfallBlend
    {
        /// <summary>The game's thunderstorm: rain, lightning and thunder, wet, and cold at night. The aspects block of
        /// the rule file holds numbers only, so the weather is named here rather than in a rule field.</summary>
        public const string StormName = "ThunderStorm";

        /// <summary>Seconds night takes to fall as a player joins the fight, and to lift after it.</summary>
        private const float Fade = 6f;

        /// <summary>Metres past `range` a player in the fight must go before it lets them go.</summary>
        private const float Margin = 10f;

        /// <summary>The longest single step counted, so a hitch does not jump the sky.</summary>
        private const float MaxStep = 0.25f;

        private static readonly List<NightfallSky> Bosses = new List<NightfallSky>();
        private static EnvMan? _for;
        private static float _weight;
        private static float _lastStep;
        private static bool _inFight;

        /// <summary>No Nightfall boss loaded here and no night left to lift: the patch's whole cost then.</summary>
        public static bool Idle => Bosses.Count == 0 && _weight <= 0f;

        /// <summary>The share of the storm drawn this step, eased at both ends; 0 while another holds it.</summary>
        public static float Shown { get; private set; }

        /// <summary>The storm's setup, looked up each step it is drawn; null if the game has no such one.</summary>
        public static EnvSetup? Storm { get; private set; }

        /// <summary>At least half drawn: where the game's own weather blend hands over its weather flags.</summary>
        public static bool Storming => Shown >= 0.5f && Storm != null;

        public static void Join(NightfallSky boss)
        {
            if (!Bosses.Contains(boss))
            {
                Bosses.Add(boss);
            }
        }

        public static void Leave(NightfallSky boss) => Bosses.Remove(boss);

        /// <summary>One environment step: move toward the fight or away from it, and say how much to draw.</summary>
        public static float Step(EnvMan man)
        {
            if (man != _for)
            {
                Reset(man); // a new world: the last one's sky went with it
            }
            float dt = Mathf.Clamp(Time.time - _lastStep, 0f, MaxStep);
            _lastStep = Time.time;
            bool deferred = Deferred(man);
            _inFight = !deferred && InFight(Reach());
            _weight = Mathf.MoveTowards(_weight, _inFight ? 1f : 0f, dt / Fade);
            Shown = deferred ? 0f : Mathf.SmoothStep(0f, 1f, _weight);
            Storm = Shown > 0f ? man.GetEnv(StormName) : null;
            return Shown;
        }

        private static void Reset(EnvMan man)
        {
            _for = man;
            _weight = 0f;
            _inFight = false;
            _lastStep = Time.time;
        }

        // Weather someone else holds: a zone's own (forced, like a dungeon's, or not), a forced one, or the console's.
        private static bool Deferred(EnvMan man) =>
            !string.IsNullOrEmpty(man.m_forceEnv) || !string.IsNullOrEmpty(man.m_debugEnv) || man.m_debugTimeOfDay
            || EnvZone.GetEnvironment() != null;

        // `range`, plus the margin for a player already in the fight; 0 turns the sky off.
        private static float Reach()
        {
            float range = AspectMath.Power(Aspect.Nightfall, Fields.Range);
            return range <= 0f ? 0f : range + (_inFight ? Margin : 0f);
        }

        // Whether any living Nightfall boss loaded here holds the viewer; a boss destroyed unseen leaves the list.
        private static bool InFight(float reach)
        {
            if (reach <= 0f || !Viewer(out Vector3 at) || Character.InInterior(at))
            {
                return false;
            }
            bool held = false;
            for (int i = Bosses.Count - 1; i >= 0; i--)
            {
                if (Bosses[i] == null)
                {
                    Bosses.RemoveAt(i);
                }
                else if (Bosses[i].Holds(at, reach))
                {
                    held = true;
                }
            }
            return held;
        }

        // Where the fight is judged from: the local player, or while they lie dead or have not yet spawned, the camera.
        private static bool Viewer(out Vector3 at)
        {
            Player player = Player.m_localPlayer;
            if (player != null && !player.IsDead())
            {
                at = player.transform.position;
                return true;
            }
            Camera camera = Utils.GetMainCamera();
            at = camera != null ? camera.transform.position : Vector3.zero;
            return camera != null;
        }
    }
}
