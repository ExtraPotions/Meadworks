// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Core/Herd.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; Crucible boundaries; owned native lifecycle.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Creature facts every Husbandry feature reads: its Tameable, whether it is being tamed, taming time left, world
    /// time, and its tier. Read-only and valid on any machine that has the creature loaded (ZDO values replicate).
    /// </summary>
    public static class Herd
    {
        private static readonly Dictionary<string, float> tiers = new Dictionary<string, float>();

        /// <summary>The creature's Tameable, cached by its AI; null for players and creatures that cannot be tamed.</summary>
        public static Tameable TameableOf(Character character)
        {
            if (character == null || character.IsPlayer())
                return null;
            BaseAI ai = character.GetBaseAI();
            return ai != null ? ai.m_tamable : null;
        }

        /// <summary>Seconds of taming left (the game's 1800 s for an untouched creature); 0 for a tamed one.</summary>
        public static float TamingLeft(Tameable tameable)
        {
            ZNetView nview = tameable != null ? tameable.m_nview : null;
            if (nview == null || !nview.IsValid() || tameable.IsTamed())
                return 0f;
            return nview.GetZDO().GetFloat(ZDOVars.s_tameTimeLeft, tameable.m_tamingTime);
        }

        /// <summary>An untamed creature whose taming has started: tameness above 0%.</summary>
        public static bool IsBeingTamed(Tameable tameable) =>
            tameable != null && tameable.m_character != null && !tameable.IsTamed()
            && tameable.m_nview != null && tameable.m_nview.IsValid() && TamingLeft(tameable) < tameable.m_tamingTime;

        /// <summary>The world's time now, the clock the game's taming and breeding use.</summary>
        public static DateTime Now => ZNet.instance.GetTime();

        /// <summary>Seconds from world time <paramref name="ticks"/> until now (negative when it lies ahead).</summary>
        public static double SecondsSince(long ticks) => (Now - new DateTime(ticks)).TotalSeconds;

        /// <summary>World time <paramref name="seconds"/> from now, in ticks.</summary>
        public static long TicksIn(double seconds) => Now.AddSeconds(seconds).Ticks;

        private static readonly ConditionalWeakTable<GameObject, string> prefabNames = new ConditionalWeakTable<GameObject, string>();

        /// <summary>The object's prefab name, worked out once per object: the herd timers ask for every creature every few seconds.</summary>
        public static string PrefabName(Component component) =>
            prefabNames.GetValue(component.gameObject, gameObject => Utils.GetPrefabName(gameObject));

        /// <summary>The creature's tier for experience: 1 + half a step per doubling of its health over 10, from 1 to 5.</summary>
        public static float Tier(Character character) => character == null ? 1f : Tier(PrefabName(character));

        /// <summary>The tier of a creature prefab: boar and hen 1, wolf about 2.5, lox, asksvin and moose about 4.3.</summary>
        public static float Tier(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
                return 1f;
            if (tiers.TryGetValue(prefabName, out float tier))
                return tier;
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
            Character character = prefab != null ? prefab.GetComponent<Character>() : null;
            float health = character != null ? Mathf.Max(10f, character.m_health) : 10f;
            tier = Mathf.Clamp(1f + 0.5f * Mathf.Log(health / 10f, 2f), 1f, 5f);
            tiers[prefabName] = tier;
            return tier;
        }
    }
}
