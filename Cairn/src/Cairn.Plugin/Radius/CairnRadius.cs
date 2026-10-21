// Cairn - ExtraPotions Meadworks exploration radius (runtime).
// Portions Copyright 2026 Crystal Ferrai, Pathfinder 2.2.5 (Pathfinder/PathfinderPlugin.cs at
// f8a2cb3edca0417d54281d1353680dff46c4ed62, https://github.com/CrystalFerrai/ValheimMods).
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Changed by expDARE, 2026: the UpdateExplore transpiler keeps the Minimap instance on the stack and calls Cairn's radius
// with it (the radius-off rule needs the game's value); the inputs are gathered here and computed by Cairn.Core's
// RadiusModel; Q1 hold, Keel's sea factor through the Crucible contract meadworks.explore.radius 1.0 (else GrindstoneSkills'
// Sailing API, looked up silently when absent) and Exploration skill factors added; the computed radius is kept for the contract
// meadworks.explore.current 1.0; the HUD text objects and ConditionalConfigSync removed. Copyright (C) 2026 expDARE
// (ExtraPotions Meadworks) for the changes.
// SPDX-License-Identifier: GPL-3.0-or-later AND Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Radius;
using ExtraPotions.Cairn.Skill;
using ExtraPotions.Meadworks.Contracts;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Cairn.Runtime
{
    /// <summary>Cairn's map exploration radius (design A1 to A8). Main thread only.</summary>
    public static class CairnRadius
    {
        static readonly List<Player> Nearby = new List<Player>();
        static Func<float>? grindstoneFactor;
        static bool grindstoneLooked;
        static Func<float>? keelFactor;
        static bool keelLooked;
        static bool failureLogged;
        static bool computed;
        static float current;

        /// <summary>The game's fog density from the last <c>EnvMan.SetEnv</c>, before Cairn's removal (design A5).</summary>
        public static float GameFogDensity { get; set; } = float.NaN;

        /// <summary>The last computed radius and its terms (Keel's ship panel and the planned debug lines read it).</summary>
        public static RadiusResult? Last { get; private set; }

        public static void Reset()
        {
            Last = null;
            GameFogDensity = float.NaN;
            grindstoneFactor = null;
            grindstoneLooked = false;
            keelFactor = null;
            keelLooked = false;
            failureLogged = false;
            computed = false;
            current = 0f;
        }

        /// <summary>The <c>meadworks.explore.current</c> 1.0 provider: the radius last computed for the local player, or an
        /// invalid value before the first computation so readers use their own.</summary>
        public static float Current() => ExploreCurrentValue.Of(computed, current);

        private static float Keep(float radius)
        {
            current = radius;
            computed = true;
            return radius;
        }

        /// <summary>The radius <c>Minimap.UpdateExplore</c> uses for <paramref name="player"/>.</summary>
        public static float Get(Minimap minimap, Player player)
        {
            var game = minimap != null ? minimap.m_exploreRadius : 100f;
            var settings = Plugin.Settings;
            if (!Plugin.Ready || settings == null || player == null || player != Player.m_localPlayer) return game;
            try
            {
                var held = GatePolicy.Holds(CairnModules.Facts());
                var shipNearby = ShipNearby(player);
                var factors = ExplorationSkill.Factors(SkillLevel(player), settings.Skill, shipNearby ? SeaFactor() : 1f);
                if (!settings.Get(CairnSettings.Radius.Enabled))
                {
                    Last = null;
                    return Keep(RadiusModel.Disabled(game, shipNearby, factors));
                }
                var result = RadiusModel.Compute(Inputs(player, shipNearby), settings.Radius(held), factors);
                Last = result;
                return Keep(result.Radius);
            }
            catch (Exception error)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    UnityEngine.Debug.LogWarning("[Cairn] Exploration radius fell back to the game's value: " + error.GetType().Name + ": " + error.Message);
                }
                return game;
            }
        }

        private static RadiusInputs Inputs(Player player, bool shipNearby)
        {
            var env = EnvMan.instance;
            var position = player.transform.position;
            var dir = env != null && env.m_dirLight != null ? env.m_dirLight.color * env.m_dirLight.intensity : Color.black;
            var ambient = RenderSettings.ambientLight;
            var setup = env != null ? env.GetCurrentEnvironment() : null;
            var names = setup?.m_psystems?.Where(p => p != null).Select(p => p.name).ToArray() ?? Array.Empty<string>();
            var fog = float.IsNaN(GameFogDensity) ? RenderSettings.fogDensity : GameFogDensity;
            var water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            var biome = player.GetCurrentBiome();
            return new RadiusInputs
            {
                Interior = player.InInterior(),
                ShipNearby = shipNearby,
                DirectionalLight = new Rgb(dir.r, dir.g, dir.b),
                AmbientLight = new Rgb(ambient.r, ambient.g, ambient.b),
                FogDensity = fog,
                ParticleSystems = names,
                Altitude = position.y - water,
                Biome = biome == Heightmap.Biome.BlackForest ? RadiusBiome.BlackForest
                    : biome == Heightmap.Biome.Meadows ? RadiusBiome.Meadows
                    : biome == Heightmap.Biome.Plains ? RadiusBiome.Plains : RadiusBiome.Other,
                InForest = biome == Heightmap.Biome.Meadows && WorldGenerator.InForest(position),
                ForestFactor = biome == Heightmap.Biome.Plains ? WorldGenerator.GetForestFactor(position) : 1f,
            };
        }

        /// <summary>Pathfinder: riding in a ship that has a pilot within 21 m also counts as at sea.</summary>
        private static bool ShipNearby(Player player)
        {
            Nearby.Clear();
            Player.GetPlayersInRange(player.transform.position, RadiusModel.ShipShareRange, Nearby);
            var any = Nearby.Any(p => p != null && p.IsAttachedToShip());
            Nearby.Clear();
            return any;
        }

        /// <summary>
        /// The sea factor: Keel's <c>meadworks.explore.radius</c> 1.0 provider (read through
        /// <see cref="ExploreRadiusContract.ReadFactor"/>, neutral when it fails) when it gives more than neutral, else
        /// GrindstoneSkills' public Sailing API (version 1) while aboard, so its perk still counts once the transpiler replaces
        /// the field GrindstoneSkills scales (Keel's Sailing steps aside while GrindstoneSkills runs its own).
        /// </summary>
        private static float SeaFactor()
        {
            if (!keelLooked)
            {
                keelLooked = true;
                try { ExtraPotions.Meadworks.Crucible.Plugin.Services.Capabilities.TryResolve(ExploreRadiusContract.Id, ExploreRadiusContract.ContractVersion, out keelFactor); }
                catch (Exception) { keelFactor = null; }
            }
            if (!grindstoneLooked)
            {
                grindstoneLooked = true;
                var api = OptionalSailingApi.FindType(AppDomain.CurrentDomain.GetAssemblies(), "GrindstoneSkills.Api.SailingApi");
                var version = api == null ? null : AccessTools.Method(api, "GetApiVersion");
                var factor = api == null ? null : AccessTools.Method(api, "GetExploreRadiusFactor");
                if (version != null && factor != null && version.Invoke(null, null) is int v && v == 1)
                    grindstoneFactor = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), factor);
            }
            var keel = keelFactor != null ? ExploreRadiusContract.ReadFactor(keelFactor) : ExploreRadiusContract.NeutralFactor;
            var aboard = Ship.GetLocalShip() != null;
            var grindstone = grindstoneFactor != null && aboard && keel <= 1f ? grindstoneFactor() : 1f;
            return SeaFactorSource.Pick(keelFactor != null, keel, grindstoneFactor != null, aboard, grindstone);
        }

        /// <summary>The Exploration skill level through CS1 (CA04b); 0 when the skill is not registered.</summary>
        private static float SkillLevel(Player player) => CairnExploration.Level(player);

        [HarmonyPatch(typeof(Minimap))]
        internal static class Patches
        {
            private enum State { Searching, Checking, Finishing }

            /// <summary>Replaces the one <c>this.m_exploreRadius</c> read with <c>CairnRadius.Get(this, player)</c>.</summary>
            [HarmonyPatch(nameof(Minimap.UpdateExplore)), HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> UpdateExplore(IEnumerable<CodeInstruction> instructions)
            {
                var state = State.Searching;
                CodeInstruction? previous = null;
                var get = AccessTools.Method(typeof(CairnRadius), nameof(Get), new[] { typeof(Minimap), typeof(Player) });
                foreach (var instruction in instructions)
                {
                    switch (state)
                    {
                        case State.Searching:
                            if (instruction.opcode == OpCodes.Ldarg_0)
                            {
                                previous = instruction;
                                state = State.Checking;
                            }
                            else yield return instruction;
                            break;
                        case State.Checking:
                            if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo field && field.Name == nameof(Minimap.m_exploreRadius))
                            {
                                yield return previous!;
                                yield return new CodeInstruction(OpCodes.Ldarg_2);
                                yield return new CodeInstruction(OpCodes.Call, get);
                                state = State.Finishing;
                            }
                            else if (instruction.opcode == OpCodes.Ldarg_0)
                            {
                                yield return previous!;
                                previous = instruction;
                            }
                            else
                            {
                                yield return previous!;
                                yield return instruction;
                                state = State.Searching;
                            }
                            break;
                        default:
                            yield return instruction;
                            break;
                    }
                }
                if (state == State.Checking && previous != null) yield return previous;
            }
        }
    }
}
