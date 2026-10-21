// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Exploration skill's CS1 registration and experience hooks (design D1, D2, D5), following
// Keel's Sailing adapter. Levels persist through CS1 and the game's own skills save; Cairn stores nothing of its own.
// The icon is the map table piece's own sprite, read at run time and never shipped.
using System;
using ExtraPotions.Cairn.Localization;
using ExtraPotions.Cairn.Skill;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Cairn.Runtime
{
    public static class CairnExploration
    {
        static readonly ExploreScope Scope = new ExploreScope();
        static readonly VantageTracker Vantage = new VantageTracker();
        static readonly DiscoveryRequests Requests = new DiscoveryRequests();
        static int interacting;
        static bool naturalResponse;
        static MeadworksSkill? skill;
        static ExploreSource tickPrevious;
        static bool failureLogged;

        /// <summary>The registered skill; null before Start or when registration failed.</summary>
        public static MeadworksSkill? Skill => skill;

        /// <summary>
        /// Registers Exploration through CS1 (no import: no source mod has this skill). The level is kept whether the
        /// skill is enabled or not; a failure is logged and leaves Cairn running with level 0.
        /// </summary>
        public static void Register(MeadworksLog log)
        {
            if (skill != null) return;
            try
            {
                var definition = new SkillDefinition(Plugin.Guid, ExplorationSkill.SkillId, ExplorationSkill.Name, ExplorationSkill.Description,
                    textKey: ExplorationSkill.TextKey);
                if (definition.Id.Value != ExplorationSkill.GameType)
                    throw new InvalidOperationException("CS1 gives Exploration the number " + definition.Id.Value + ", Cairn pinned " + ExplorationSkill.GameType + ".");
                skill = MeadworksSkills.Register(definition, MapTableIcon, CairnText.Translations);
            }
            catch (Exception error)
            {
                skill = null;
                log.Error("Exploration skill not registered: " + error.GetType().Name + ": " + error.Message);
            }
        }

        /// <summary>The local player's Exploration level as CS1 reports it (modifiers included); 0 without a skill or player.</summary>
        public static float Level(Player? player) =>
            skill == null || player == null ? 0f : MeadworksSkills.EffectiveLevel(player, (Skills.SkillType)ExplorationSkill.GameType);

        /// <summary>Vantage (design D5), once per frame on a graphical client.</summary>
        public static void Tick(float now, float deltaSeconds)
        {
            var player = Player.m_localPlayer;
            var minimap = Minimap.instance;
            var settings = Plugin.Settings;
            if (skill == null || settings == null || player == null || minimap == null || player.IsDead()) return;
            try
            {
                var values = settings.Skill;
                var position = player.transform.position;
                var water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
                var resets = ExplorationSkill.VantageResets(player.GetVelocity().magnitude, player.IsOnGround(), player.InInterior());
                var steppedAside = CairnModules.StepAside.SteppedAside(Integration.CairnModule.Radius);
                if (!Vantage.Tick(now, deltaSeconds, resets, position.y - water, Level(player), values, steppedAside)) return;
                var radius = ExplorationSkill.VantageRadius(ExplorationSkill.VantageBase(CairnRadius.Current(), minimap.m_exploreRadius));
                if (!(radius > 0f)) return;
                var previous = Scope.Enter(ExploreSource.Vantage);
                try { minimap.Explore(position, radius); }
                finally { Scope.Exit(previous); }
                player.Message(MessageHud.MessageType.TopLeft, CairnText.Text(CairnTextKeys.Vantage));
            }
            catch (Exception error)
            {
                Warn("Vantage", error);
            }
        }

        public static void Dispose()
        {
            skill?.Dispose();
            skill = null;
            Vantage.Reset();
            Requests.Clear();
            interacting = 0;
            naturalResponse = false;
            Scope.Exit(ExploreSource.Other);
            tickPrevious = ExploreSource.Other;
            failureLogged = false;
        }

        static Sprite? MapTableIcon()
        {
            var scene = ZNetScene.instance;
            var prefab = scene != null ? scene.GetPrefab(ExplorationSkill.IconPrefab) : null;
            var piece = prefab != null ? prefab.GetComponent<Piece>() : null;
            return piece != null ? piece.m_icon : null;
        }

        static void Award(float experience)
        {
            if (experience > 0f && skill != null && Player.m_localPlayer != null) skill.Raise(experience);
        }

        static void Warn(string what, Exception error)
        {
            if (failureLogged) return;
            failureLogged = true;
            UnityEngine.Debug.LogWarning("[Cairn] Exploration " + what + " skipped: " + error.GetType().Name + ": " + error.Message);
        }

        /// <summary>Experience hooks; client only. A cheat or another mod's reveal runs outside the scopes and earns nothing.</summary>
        internal static class Patches
        {
            /// <summary>The game's explore tick around the local player is natural exploration.</summary>
            [HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdateExplore)), HarmonyPrefix]
            private static void BeforeUpdateExplore(Player player)
            {
                tickPrevious = Scope.Enter(player != null && player == Player.m_localPlayer ? ExploreSource.Natural : ExploreSource.Other);
            }

            [HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdateExplore)), HarmonyFinalizer]
            private static void AfterUpdateExplore()
            {
                Scope.Exit(tickPrevious);
                tickPrevious = ExploreSource.Other;
            }

            /// <summary>Counts the texels this explore newly uncovers, before the game marks them.</summary>
            [HarmonyPatch(typeof(Minimap), nameof(Minimap.Explore), new[] { typeof(Vector3), typeof(float) }), HarmonyPrefix]
            private static void BeforeExplore(Minimap __instance, Vector3 p, float radius)
            {
                var source = Scope.Current;
                var settings = Plugin.Settings;
                if (source == ExploreSource.Other || skill == null || settings == null || __instance == null) return;
                try
                {
                    var values = settings.Skill;
                    if (!values.Enabled) return;
                    var explored = __instance.m_explored;
                    if (explored == null) return;
                    __instance.WorldToPixel(p, out var px, out var py);
                    var size = __instance.m_textureSize;
                    var cells = ExploreCells.CountNew(px, py, radius, __instance.m_pixelSize, size, i => i < explored.Length && explored[i]);
                    Award(ExplorationExperience.Area(source, cells, __instance.m_pixelSize, values));
                }
                catch (Exception error)
                {
                    Warn("area experience", error);
                }
            }

            /// <summary>The local player using a Vegvisir or runestone is the natural discovery interaction.</summary>
            [HarmonyPatch(typeof(Vegvisir), nameof(Vegvisir.Interact)), HarmonyPrefix]
            private static void BeforeVegvisir(Humanoid character) => EnterInteraction(character);

            [HarmonyPatch(typeof(Vegvisir), nameof(Vegvisir.Interact)), HarmonyFinalizer]
            private static void AfterVegvisir(Humanoid character) => ExitInteraction(character);

            [HarmonyPatch(typeof(RuneStone), nameof(RuneStone.Interact)), HarmonyPrefix]
            private static void BeforeRuneStone(Humanoid character) => EnterInteraction(character);

            [HarmonyPatch(typeof(RuneStone), nameof(RuneStone.Interact)), HarmonyFinalizer]
            private static void AfterRuneStone(Humanoid character) => ExitInteraction(character);

            private static void EnterInteraction(Humanoid character)
            {
                if (character != null && character == Player.m_localPlayer) interacting++;
            }

            private static void ExitInteraction(Humanoid character)
            {
                if (character != null && character == Player.m_localPlayer && interacting > 0) interacting--;
            }

            /// <summary>Records the request a natural interaction sends to the server.</summary>
            [HarmonyPatch(typeof(Game), nameof(Game.DiscoverClosestLocation)), HarmonyPrefix]
            private static void BeforeDiscoverClosest(string pinName, int pinType)
            {
                if (interacting > 0) Requests.Record(pinName, pinType, Time.time);
            }

            /// <summary>The server's answer adds the pin; it is natural when it answers a recorded request.</summary>
            [HarmonyPatch(typeof(Game), nameof(Game.RPC_DiscoverLocationResponse)), HarmonyPrefix]
            private static void BeforeDiscoverResponse(string pinName, int pinType)
            {
                naturalResponse = Requests.Natural(pinName, pinType, Time.time);
            }

            [HarmonyPatch(typeof(Game), nameof(Game.RPC_DiscoverLocationResponse)), HarmonyFinalizer]
            private static void AfterDiscoverResponse() => naturalResponse = false;

            /// <summary>Pays for a new pin only inside a natural response; a cheat, console or other mod pin earns nothing.</summary>
            [HarmonyPatch(typeof(Minimap), nameof(Minimap.DiscoverLocation)), HarmonyPostfix]
            private static void AfterDiscoverLocation(bool __result)
            {
                var settings = Plugin.Settings;
                if (!__result || skill == null || settings == null) return;
                Award(ExplorationExperience.Discovery(__result, naturalResponse, settings.Skill));
            }

            [HarmonyPatch(typeof(Player), nameof(Player.AddKnownBiome)), HarmonyPrefix]
            private static void BeforeAddKnownBiome(Player __instance, BiomeSector biome, out bool __state)
            {
                __state = true;
                try { __state = __instance != Player.m_localPlayer || __instance.IsBiomeKnown(biome); }
                catch (Exception error) { Warn("biome experience", error); }
            }

            [HarmonyPatch(typeof(Player), nameof(Player.AddKnownBiome)), HarmonyPostfix]
            private static void AfterAddKnownBiome(bool __state)
            {
                var settings = Plugin.Settings;
                if (__state || skill == null || settings == null) return;
                Award(ExplorationExperience.Biome(__state, settings.Skill));
            }
        }
    }
}
