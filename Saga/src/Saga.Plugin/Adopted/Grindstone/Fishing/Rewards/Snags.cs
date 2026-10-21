// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Rewards/Snags.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Snags: a cast left in the water can catch on something. All on the angler's client, from <see cref="FloatScope"/>.
    /// <list type="bullet">
    /// <item>Once a cast has sat in the water for Snag Wait seconds without a fish on the line, the snag chance is rolled,
    /// once per cast: from Snag Chance At 0 to At 100 by the angler's level at the cast.</item>
    /// <item>A snagged line is heavy: it reels in at half speed, costs extra stamina while reeling and drags the float
    /// down; no fish nibbles a snagged hook (<see cref="Strike"/>).</item>
    /// <item>When the game lands the empty line (its step returns the bait and removes the float at 0.5 m of line), the
    /// snag comes in: a find picked from Saga.Snags.yml for the biome where the float snagged
    /// (<see cref="SnagFile"/>), put into the inventory (<see cref="SnagLoot"/>), with "Landed &lt;name&gt;!" for the
    /// angler and "Found &lt;name&gt;!" floating for everyone near.</item>
    /// </list>
    /// Snags give no experience: casting and waiting would otherwise pay without fishing.
    /// </summary>
    public static class Snags
    {
        private const float SnaggedReel = 0.5f;
        private const float SnaggedStaminaPerSecond = 6f;
        private const float Drag = 4f;
        private const float LandedLine = 0.5f;

        public static void Step(FishingFloat fishingFloat, FloatFight fight, Player angler, float dt)
        {
            if (fight.Snagged)
            {
                Heavy(fishingFloat, angler, dt);
                return;
            }
            if (fight.SnagRolled || !fishingFloat.IsInWater())
                return;
            fight.WaterTime += dt;
            if (fight.WaterTime < FishingCatchSettings.SnagWait.Value)
                return;
            fight.SnagRolled = true;
            if (Random.value >= Chance(fight.Level))
                return;
            fight.Snagged = true;
            fight.SnagBiome = BiomeAt(fishingFloat.transform.position);
            angler.Message(MessageHud.MessageType.Center, SagaText.Text("gs_6db4ee3bae4adaf3"));
        }

        /// <summary>The chance, 0..1, that a cast snags, at the angler's level.</summary>
        public static float Chance(float level) =>
            Mathf.Clamp01(FishSkill.Percent(FishSkill.Between(FishingCatchSettings.SnagChanceAt0.Value, FishingCatchSettings.SnagChanceAt100.Value, level)));

        /// <summary>The reel speed factor without a fish on the line: slower while snagged.</summary>
        public static float ReelFactor(FloatFight fight) => fight.Snagged ? SnaggedReel : 1f;

        public static bool BlocksNibble(FloatFight fight) => fight != null && fight.Snagged;

        /// <summary>After a step of a snagged float: when the game landed the empty line, the snag comes in.</summary>
        public static void AfterStep(FishingFloat fishingFloat, FloatFight fight, Player angler)
        {
            if (angler == null || fight == null || fishingFloat.m_nview.IsValid() || fishingFloat.m_lineLength > LandedLine)
                return;
            Land(angler, fight.SnagBiome);
        }

        private static void Heavy(FishingFloat fishingFloat, Player angler, float dt)
        {
            if (angler.IsBlocking() && angler.HaveStamina())
                angler.UseStamina(SnaggedStaminaPerSecond * dt);
            fishingFloat.m_body.AddForce(Vector3.down * Drag, ForceMode.Acceleration);
        }

        private static Heightmap.Biome BiomeAt(Vector3 where) =>
            WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(where) : Heightmap.Biome.None;

        private static void Land(Player angler, Heightmap.Biome biome)
        {
            FindEntry find = SnagFile.Pick(biome);
            if (find == null || SnagLoot.Give(angler, find) == 0)
            {
                angler.Message(MessageHud.MessageType.Center, SagaText.Text("gs_b51f88e78aeb102e"));
                return;
            }
            angler.Message(MessageHud.MessageType.Center, SagaText.Format("gs_33f4ef53feab2a9d", find.Name));
            FishCallout.Broadcast(angler.transform.position + Vector3.up * 2f, SagaText.Packet("gs_2dadc5689ae69efb", find.Name));
        }
    }
}
