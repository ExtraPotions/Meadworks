using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// A held weapon's damage on the stat sheet: a line per damage type with the range one primary hit does now, worked
    /// out as the game's attack does. The weapon's damage (quality and world level, <c>GetDamage</c>) plus the ammo's for
    /// a weapon that shoots (<see cref="HeldItems.Ammo"/>), times the attack's own multipliers (<c>Attack.ModifyDamage</c>:
    /// its damage multiplier, the character's level, the missing health ones some weapons have), times every status
    /// effect's <c>ModifyAttack</c> for the weapon's skill (meads, Epic Loot, other mods' effects), times the skill's
    /// random range (<c>Skills.GetRandomSkillRange</c>, the yellow numbers of the game's own tooltip). A bow is taken at
    /// full draw and a combo's last hit (double damage) is not counted. Each line's tooltip names those parts.
    /// </summary>
    public static class HeldDamage
    {
        private const float Probe = 100f;

        private static readonly HitData.DamageType[] Types =
        {
            HitData.DamageType.Damage, HitData.DamageType.Blunt, HitData.DamageType.Slash, HitData.DamageType.Pierce,
            HitData.DamageType.Fire, HitData.DamageType.Frost, HitData.DamageType.Lightning, HitData.DamageType.Poison,
            HitData.DamageType.Spirit,
        };

        private static readonly HitData.DamageTypes Probes = new HitData.DamageTypes
        {
            m_damage = Probe, m_blunt = Probe, m_slash = Probe, m_pierce = Probe, m_chop = Probe, m_pickaxe = Probe,
            m_fire = Probe, m_frost = Probe, m_lightning = Probe, m_poison = Probe, m_spirit = Probe,
        };

        public static void Lines(Player player, ItemDrop.ItemData weapon, StatSheet sheet)
        {
            ItemDrop.ItemData ammo = HeldItems.Ammo(player, weapon);
            HitData.DamageTypes hit = Hit(player, weapon, ammo);
            player.GetSkills().GetRandomSkillRange(out float min, out float max, weapon.m_shared.m_skillType);
            foreach (HitData.DamageType type in Types)
            {
                float value = Of(hit, type);
                if (Mathf.Abs(value) < 0.5f)
                    continue;
                string range = Mathf.RoundToInt(value * min) + "-" + Mathf.RoundToInt(value * max);
                sheet.Add("$inventory_" + type.ToString().ToLowerInvariant(), range, Tip(player, weapon, ammo, type));
            }
        }

        /// <summary>The hit before the skill's roll: weapon and ammo, the attack's multipliers, the status effects.</summary>
        private static HitData.DamageTypes Hit(Player player, ItemDrop.ItemData weapon, ItemDrop.ItemData ammo)
        {
            HitData hit = new HitData { m_damage = weapon.GetDamage(), m_skill = weapon.m_shared.m_skillType };
            hit.SetAttacker(player);
            if (ammo != null)
                hit.m_damage.Add(ammo.GetDamage());
            hit.m_damage.Modify(OwnFactor(player, weapon.m_shared.m_attack) * HealthFactor(player, weapon.m_shared.m_attack));
            player.GetSEMan().ModifyAttack(weapon.m_shared.m_skillType, ref hit);
            return hit.m_damage;
        }

        /// <summary>The attack's damage multiplier and the character's level (1 for a player, so no change).</summary>
        private static float OwnFactor(Player player, Attack attack) =>
            attack.m_damageMultiplier * (1f + Mathf.Max(0, player.GetLevel() - 1) * 0.5f);

        /// <summary>What the attack gains from the health the player is missing (a few weapons); 1 for the rest.</summary>
        private static float HealthFactor(Player player, Attack attack)
        {
            float factor = 1f;
            if (attack.m_damageMultiplierPerMissingHP > 0f)
                factor *= 1f + (player.GetMaxHealth() - player.GetHealth()) * attack.m_damageMultiplierPerMissingHP;
            if (attack.m_damageMultiplierByTotalHealthMissing > 0f)
                factor *= 1f + (1f - player.GetHealthPercentage()) * attack.m_damageMultiplierByTotalHealthMissing;
            return factor;
        }

        /// <summary>The weapon's and the ammo's part, the missing health, each effect that changes the type, the skill's roll.</summary>
        private static string Tip(Player player, ItemDrop.ItemData weapon, ItemDrop.ItemData ammo, HitData.DamageType type)
        {
            Attack attack = weapon.m_shared.m_attack;
            float own = OwnFactor(player, attack);
            TipText tip = new TipText();
            Source(tip, weapon, type, own, "");
            if (ammo != null)
                Source(tip, ammo, type, own, "+");
            float health = HealthFactor(player, attack);
            if (!Mathf.Approximately(health, 1f))
                tip.Part(StatTipWords.MissingHealth, "x" + health.ToString("0.##"));
            Effects(player, weapon.m_shared.m_skillType, type, tip);
            player.GetSkills().GetRandomSkillRange(out float min, out float max, weapon.m_shared.m_skillType);
            tip.Part(HeldTips.SkillLabel(player, weapon.m_shared.m_skillType), "x" + min.ToString("0.00") + "-" + max.ToString("0.00"));
            return tip.ToString();
        }

        private static void Source(TipText tip, ItemDrop.ItemData item, HitData.DamageType type, float factor, string sign)
        {
            float value = Of(item.GetDamage(), type) * factor;
            if (Mathf.Abs(value) >= 0.5f)
                tip.Part(item.m_shared.m_name, sign + TipText.Whole(value));
        }

        /// <summary>Each status effect that changes this damage type for this skill, with what it adds.</summary>
        private static void Effects(Player player, Skills.SkillType skill, HitData.DamageType type, TipText tip)
        {
            foreach (StatusEffect effect in player.GetSEMan().GetStatusEffects())
            {
                HitData probe = new HitData { m_damage = Probes, m_skill = skill };
                probe.SetAttacker(player);
                effect.ModifyAttack(skill, ref probe);
                float change = Of(probe.m_damage, type) / Probe - 1f;
                if (Mathf.Abs(change) >= 0.005f)
                    tip.Part(TipText.EffectName(effect), TipText.Percent(change));
            }
        }

        /// <summary>One damage type's amount; <c>Damage</c> (the game's untyped damage) for anything else.</summary>
        public static float Of(HitData.DamageTypes damage, HitData.DamageType type)
        {
            switch (type)
            {
                case HitData.DamageType.Blunt: return damage.m_blunt;
                case HitData.DamageType.Slash: return damage.m_slash;
                case HitData.DamageType.Pierce: return damage.m_pierce;
                case HitData.DamageType.Fire: return damage.m_fire;
                case HitData.DamageType.Frost: return damage.m_frost;
                case HitData.DamageType.Lightning: return damage.m_lightning;
                case HitData.DamageType.Poison: return damage.m_poison;
                case HitData.DamageType.Spirit: return damage.m_spirit;
                default: return damage.m_damage;
            }
        }
    }
}
