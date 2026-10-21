// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Experience/MineXp.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Pickaxes experience, on top of the game's own, all on the miner's own client (skills live there).
    /// <list type="bullet">
    /// <item><b>Swings.</b> Attack.DoMeleeAttack raises Pickaxes once per swing that hit rock (m_raiseSkillAmount, ×1.5
    /// when it also hit a creature). <see cref="MineSwing"/>'s prefix on global::Skills.RaiseSkill multiplies that raise by
    /// <see cref="SwingScale"/>: the highest <see cref="Scale"/> among the rocks the swing hit, times the Experience
    /// Multiplier. The game counts a hit on rock whether or not it does damage (the tool tier is checked on the rock's
    /// owner), so a rock too hard for the pickaxe gets the game's amount and no bonus, or it would be an endless farm.</item>
    /// <item><b>Clean strikes.</b> The Seams feature calls <see cref="OnCleanStrike"/> inside the swing.</item>
    /// <item><b>Discovery.</b> <see cref="OnRockHit"/>: the first hit on each kind of ore deposit
    /// (<see cref="MineDiscovery"/>).</item>
    /// </list>
    /// Credits go through <see cref="MineSwing.RaiseUnscoped"/>, so an open swing scope never scales them again; they
    /// still go through Player.RaiseSkill (Rested +50%) and the world's skill-gain rate (Game.m_skillGainRate).
    /// With Experience Per Biome Step 0, Ore Experience Bonus 0 and Experience Multiplier 1, a swing earns exactly
    /// what the game gives.
    /// </summary>
    public static class MineXp
    {
        /// <summary>Tests a pickaxe's tool tier with the game's own check; reused, single-threaded like all game code.</summary>
        private static readonly HitData Probe = new HitData();

        /// <summary>The Experience Multiplier, never below 0.</summary>
        public static float Multiplier => Mathf.Max(0f, PickaxeExperienceSettings.Multiplier.Value);

        /// <summary>
        /// Called by <see cref="MineSwing"/> on the miner's own client when the game raises Pickaxes for a swing: the
        /// multiplier for that raise. The highest <see cref="Scale"/> among <paramref name="rocks"/> (1 when the swing
        /// raised Pickaxes without hitting rock), each checked against the swinging pickaxe's tool tier, times the
        /// Experience Multiplier. 1 while Pickaxes is off.
        /// </summary>
        public static float SwingScale(IReadOnlyList<Rock> rocks, Attack attack)
        {
            if (!PickSkill.Active)
                return 1f;
            ItemDrop.ItemData weapon = attack != null ? attack.m_weapon : null;
            float highest = 1f;
            for (int i = 0; rocks != null && i < rocks.Count; i++)
            {
                if (rocks[i] != null)
                    highest = Mathf.Max(highest, Scale(rocks[i], CanMine(weapon, rocks[i].Tier)));
            }
            return highest * Multiplier;
        }

        /// <summary>
        /// Called by <see cref="MineHit"/> on the miner's own client for every local pickaxe hit on a rock, before it is
        /// sent: the first hit on an ore deposit this character has never mined (by <see cref="RockInfo.Identity"/>: its
        /// name, so kinds that read the same count once) is a discovery. It earns
        /// Discovery Experience × the deposit's scale × the multiplier, is recorded in the character's custom data and
        /// floats "Discovered &lt;name&gt;!" for the miner. Plain stone is never discovered. A deposit too hard for the
        /// pickaxe is not recorded, so its discovery waits until the miner can mine it; nor is one while the credit is
        /// 0 (Discovery Experience or the multiplier at 0), so turning it on later still pays.
        /// </summary>
        public static void OnRockHit(Rock rock, HitData hit)
        {
            Player player = Player.m_localPlayer;
            if (!PickSkill.Active || player == null || rock == null || hit == null || !rock.IsOre)
                return;
            string identity = rock.Info.Identity;
            if (MineDiscovery.HasMined(player, identity) || !hit.CheckToolTier(rock.Tier))
                return;
            float amount = Credit(PickaxeExperienceSettings.Discovery.Value, Scale(rock, true));
            if (amount <= 0f || !MineDiscovery.TryRecord(player, identity))
                return;
            MineSwing.RaiseUnscoped(player, amount);
            MineCallout.ShowLocal(hit.m_point, $"Discovered {rock.DisplayName}!");
        }

        /// <summary>
        /// Called by the Seams feature on the miner's own client for each clean strike, inside the swing: Clean Strike
        /// Experience × the rock's scale × the multiplier, credited at once. The tool tier check uses the swing's
        /// pickaxe, or the local player's current weapon outside a swing.
        /// </summary>
        public static void OnCleanStrike(Rock rock)
        {
            Player player = Player.m_localPlayer;
            if (!PickSkill.Active || player == null || rock == null)
                return;
            Attack attack = MineSwing.Attack;
            ItemDrop.ItemData weapon = attack != null && attack.m_weapon != null ? attack.m_weapon : player.GetCurrentWeapon();
            float amount = Credit(PickaxeExperienceSettings.CleanStrike.Value, Scale(rock, CanMine(weapon, rock.Tier)));
            MineSwing.RaiseUnscoped(player, amount);
        }

        /// <summary>
        /// A rock's experience scale, before the multiplier: 1 + Experience Per Biome Step per step of its biome
        /// (<see cref="BiomeStep"/>), times 1 + Ore Experience Bonus for an ore deposit. 1 when the pickaxe cannot mine it.
        /// </summary>
        public static float Scale(Rock rock, bool canMine)
        {
            if (rock == null || !canMine)
                return 1f;
            float biome = 1f + Percent(PickaxeExperienceSettings.BiomeStep.Value) * BiomeStep(rock.Biome);
            return rock.IsOre ? biome * (1f + Percent(PickaxeExperienceSettings.OreBonus.Value)) : biome;
        }

        /// <summary>
        /// The biome's step in the game's progression: Meadows 0, Black Forest 1, Swamp 2, Mountain 3, Plains 4,
        /// Mistlands 5, Ashlands and Deep North 6. The Ocean (Leviathans, rocks standing in the sea), no biome and any
        /// biome a mod adds count as Meadows: no bonus rather than a guess.
        /// </summary>
        public static int BiomeStep(Heightmap.Biome biome)
        {
            switch (biome)
            {
                case Heightmap.Biome.BlackForest:
                    return 1;
                case Heightmap.Biome.Swamp:
                    return 2;
                case Heightmap.Biome.Mountain:
                    return 3;
                case Heightmap.Biome.Plains:
                    return 4;
                case Heightmap.Biome.Mistlands:
                    return 5;
                case Heightmap.Biome.AshLands:
                case Heightmap.Biome.DeepNorth:
                    return 6;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Whether a pickaxe can damage a rock that needs <paramref name="tier"/>: the game's own HitData.CheckToolTier
        /// (the check MineRock5, MineRock and Destructible make on the owner), world-level-locked tools included. True
        /// when the weapon is unknown.
        /// </summary>
        public static bool CanMine(ItemDrop.ItemData weapon, int tier)
        {
            if (weapon == null || weapon.m_shared == null)
                return true;
            Probe.m_toolTier = (short)weapon.m_shared.m_toolTier;
            Probe.m_itemWorldLevel = (byte)weapon.m_worldLevel;
            return Probe.CheckToolTier(tier);
        }

        private static float Credit(float setting, float scale) => Mathf.Max(0f, setting) * scale * Multiplier;

        private static float Percent(float value) => Mathf.Max(0f, value) / 100f;
    }
}
