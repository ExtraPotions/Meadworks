// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Grave-Lord's Command and Grave Vigor: creatures a blood staff summons are tagged at spawn with the staff's two
    /// values, in the creature's own ZDO (keys <c>ecf_summon_damage</c>, <c>ecf_summon_health</c>, fractions), so they
    /// survive an owner change and a reload. The tag is written by the caster's client, which creates the creature and
    /// owns it at that moment (SpawnAbility spawns on the caster's side).
    /// <list type="bullet">
    /// <item>Health: applied at once and to every later maximum-health write for the creature (its owner, on load at
    /// full health and on a level change).</item>
    /// <item>Damage: on whichever peer builds the summon's hit (its owner), the hit is raised before it is sent.</item>
    /// </list>
    /// </summary>
    internal static class Summons
    {
        private static readonly int DamageKey = "ecf_summon_damage".GetStableHashCode();
        private static readonly int HealthKey = "ecf_summon_health".GetStableHashCode();

        /// <summary>SpawnAbility.SetupAoe on the caster's client, with the creature just spawned.</summary>
        public static void OnSpawned(SpawnAbility ability, Character? summon)
        {
            ItemLocalSums? sums = ReferenceEquals(ability.m_owner, Player.m_localPlayer) ? ItemLocalCache.Get(ability.m_weapon) : null;
            ZDO? zdo = sums != null && summon != null && summon.m_nview.IsValid() && summon.m_nview.IsOwner() ? summon.m_nview.GetZDO() : null;
            if (zdo == null)
            {
                return;
            }
            float damage = sums!.Get(EffectKind.SummonDamage), health = sums.Get(EffectKind.SummonHealth);
            if (damage > 0f)
            {
                zdo.Set(DamageKey, damage);
            }
            if (health > 0f)
            {
                zdo.Set(HealthKey, health);
                summon!.SetMaxHealth(summon.GetMaxHealthBase() * summon.GetLevel());
                summon.SetHealth(summon.GetMaxHealth());
            }
        }

        /// <summary>Every maximum-health write for a tagged creature (its owner) is raised by the tag.</summary>
        public static float Raised(Character character, float maxHealth)
        {
            if (character.IsPlayer())
            {
                return maxHealth;
            }
            ZDO? zdo = character.m_nview != null && character.m_nview.IsValid() ? character.m_nview.GetZDO() : null;
            float health = zdo != null ? zdo.GetFloat(HealthKey, 0f) : 0f;
            return maxHealth * (1f + health);
        }

        /// <summary>A hit being built for a character, not by the local player (<see cref="OutgoingDamageDispatch"/>): raise it when the attacker is a tagged summon.</summary>
        public static void OnHit(HitData hit)
        {
            if (hit.m_attacker.IsNone() || ZDOMan.instance == null)
            {
                return;
            }
            ZDO? attacker = ZDOMan.instance.GetZDO(hit.m_attacker);
            float damage = attacker != null ? attacker.GetFloat(DamageKey, 0f) : 0f;
            if (damage > 0f)
            {
                hit.m_damage.Modify(1f + damage);
            }
        }
    }

    [HarmonyPatch]
    internal static class SummonPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SpawnAbility), nameof(SpawnAbility.SetupAoe))]
        private static void Spawned(SpawnAbility __instance, Character owner) => Summons.OnSpawned(__instance, owner);

        // The game writes a creature's maximum health from its base and level (on spawn, on load at full health, on a
        // level change); this one setter covers every path, whichever peer owns the creature then.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), nameof(Character.SetMaxHealth))]
        private static void MaxHealth(Character __instance, ref float health) => health = Summons.Raised(__instance, health);
    }
}
