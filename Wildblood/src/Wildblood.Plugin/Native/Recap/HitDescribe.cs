// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Display;
using ExtraPotions.Wildblood.Adopted.Runtime;

namespace ExtraPotions.Wildblood.Adopted.Recap
{
    /// <summary>
    /// A hit in words: who dealt it (a creature's whole name with its mutation words and stars, read the moment it hit,
    /// since the creature may be gone when the recap is watched), how it came, and its damage by type. Plain facts only.
    /// </summary>
    internal static class HitDescribe
    {
        /// <summary>A creature's name with its mutation and aspect words and stars ("Mad Greydwarf 2★"), or a player's name.</summary>
        public static string Attacker(Character? attacker)
        {
            if (attacker == null)
            {
                return "";
            }
            if (attacker is Player player)
            {
                return player.GetPlayerName();
            }
            string name = global::Localization.instance != null ? global::Localization.instance.Localize(attacker.m_name) : attacker.m_name;
            EliteController controller = attacker.GetComponent<EliteController>();
            if (controller == null || !controller.Ready)
            {
                return name + Stars(attacker.GetLevel() - 1);
            }
            int stars = controller.Traits.Stars > 0 ? controller.Traits.Stars : attacker.GetLevel() - 1; // a kept level's
            return Naming.Decorate(controller.Traits, name) + Stars(stars);
        }

        public static string Stars(int stars) => stars > 0 ? " " + stars + "\u2605" : "";

        /// <summary>How the hit came, in the game's own causes; a creature's or player's hit is "ranged" or "hit".</summary>
        public static string Source(HitData hit) => hit.m_hitType switch
        {
            HitData.HitType.Burning => "burning",
            HitData.HitType.Poisoned => "poison",
            HitData.HitType.Freezing => "freezing",
            HitData.HitType.Fall => "fall",
            HitData.HitType.Drowning => "drowning",
            HitData.HitType.EdgeOfWorld => "edge of the world",
            HitData.HitType.Tree => "falling tree",
            HitData.HitType.Structural => "falling building",
            HitData.HitType.Boat => "ship",
            HitData.HitType.AshlandsOcean => "boiling sea",
            HitData.HitType.AshlandsLava => "lava",
            HitData.HitType.CinderFire => "cinder fire",
            HitData.HitType.Undefined or HitData.HitType.EnemyHit or HitData.HitType.PlayerHit => hit.m_ranged ? "ranged" : "hit",
            _ => hit.m_hitType.ToString().ToLowerInvariant(),
        };

        /// <summary>
        /// The damage by type, largest first. The game's untyped damage (a fall, drowning) is named after its cause.
        /// </summary>
        public static List<KeyValuePair<string, float>> Parts(HitData hit, string source)
        {
            HitData.DamageTypes d = hit.m_damage;
            List<KeyValuePair<string, float>> parts = new List<KeyValuePair<string, float>>();
            Add(parts, source == "hit" || source == "ranged" ? "true" : source, d.m_damage);
            Add(parts, "blunt", d.m_blunt);
            Add(parts, "slash", d.m_slash);
            Add(parts, "pierce", d.m_pierce);
            Add(parts, "chop", d.m_chop);
            Add(parts, "pickaxe", d.m_pickaxe);
            Add(parts, "fire", d.m_fire);
            Add(parts, "frost", d.m_frost);
            Add(parts, "lightning", d.m_lightning);
            Add(parts, "poison", d.m_poison);
            Add(parts, "spirit", d.m_spirit);
            parts.Sort((a, b) => b.Value.CompareTo(a.Value));
            return parts;
        }

        private static void Add(List<KeyValuePair<string, float>> parts, string name, float value)
        {
            if (value > 0.1f)
            {
                parts.Add(new KeyValuePair<string, float>(name, value));
            }
        }
    }
}
