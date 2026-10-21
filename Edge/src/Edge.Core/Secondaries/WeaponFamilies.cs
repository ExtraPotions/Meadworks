// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackWeaponFamilyResolver.cs (blood magic, ranged and melee automatic families, their detection rules and
// order, the family cooldown group names).
// Changed by expDARE, 2026: reads the Unity-free WeaponFacts instead of ItemDrop/Attack/SpawnAbility components, so the
// rules run in the portable tests; method names shortened; behaviour unchanged.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;

namespace ExtraPotions.Edge.Secondaries
{
    public enum BloodFamily { None, Summon, Shield }

    public enum RangedFamily { None, Bomb, FireballStaff, RapidStaff, ReloadStaff, Crossbow, Bow, OneHandedElemental }

    public enum MeleeFamily { None, Knives, TwoHandedSword, OneHandedSword, OneHandedClub, Unarmed, Sledge, Polearm, Farming, Pickaxe, Spear, Battleaxe, OneHandedAxe }

    public static class WeaponFamilies
    {
        public const string BloodSummonCooldownGroup = "family:blood:summon";
        public const string BloodShieldCooldownGroup = "family:blood:shield";

        /// <summary>The automatic cooldown group of a weapon: blood magic first, then ranged, then melee; empty when none.</summary>
        public static string CooldownGroup(WeaponFacts weapon)
        {
            switch (Blood(weapon))
            {
                case BloodFamily.Summon: return BloodSummonCooldownGroup;
                case BloodFamily.Shield: return BloodShieldCooldownGroup;
            }
            switch (Ranged(weapon))
            {
                case RangedFamily.Bomb: return "family:ranged:bomb";
                case RangedFamily.FireballStaff: return "family:ranged:staff-fireball";
                case RangedFamily.RapidStaff: return "family:ranged:staff-rapid";
                case RangedFamily.ReloadStaff: return "family:ranged:staff-reload";
                case RangedFamily.Crossbow: return "family:ranged:crossbow";
                case RangedFamily.Bow: return "family:ranged:bow";
                case RangedFamily.OneHandedElemental: return "family:ranged:magic-onehanded";
            }
            switch (Melee(weapon))
            {
                case MeleeFamily.Knives: return "family:melee:knives";
                case MeleeFamily.TwoHandedSword: return "family:melee:sword-2h";
                case MeleeFamily.OneHandedSword: return "family:melee:sword-1h";
                case MeleeFamily.OneHandedClub: return "family:melee:club-1h";
                case MeleeFamily.Unarmed: return "family:melee:unarmed";
                case MeleeFamily.Sledge: return "family:melee:sledge";
                case MeleeFamily.Polearm: return "family:melee:polearm";
                case MeleeFamily.Farming: return "family:melee:farming";
                case MeleeFamily.Pickaxe: return "family:melee:pickaxe";
                case MeleeFamily.Spear: return "family:melee:spear";
                case MeleeFamily.Battleaxe: return "family:melee:battleaxe";
                case MeleeFamily.OneHandedAxe: return "family:melee:axe-1h";
                default: return "";
            }
        }

        public static BloodFamily Blood(WeaponFacts weapon)
        {
            if (weapon == null || !weapon.HasPrimary || weapon.Skill != WeaponSkill.BloodMagic) return BloodFamily.None;
            if (string.Equals(weapon.PrefabName, "StaffShield", StringComparison.OrdinalIgnoreCase)) return BloodFamily.Shield;
            return weapon.PrimarySpawnsSummon ? BloodFamily.Summon : BloodFamily.None;
        }

        public static RangedFamily Ranged(WeaponFacts weapon)
        {
            if (weapon == null || !weapon.HasPrimary || IsAmmo(weapon.ItemType) || string.IsNullOrWhiteSpace(weapon.PrimaryAnimation))
                return RangedFamily.None;
            if (IsBomb(weapon)) return RangedFamily.Bomb;
            if (weapon.Skill == WeaponSkill.ElementalMagic)
            {
                if (weapon.ItemType == WeaponItemType.OneHandedWeapon)
                    return IsOneHandedElementalProjectile(weapon) ? RangedFamily.OneHandedElemental : RangedFamily.None;
                return StaffFamily(weapon.PrimaryAnimation);
            }
            if (IsOffensiveBloodMagicStaff(weapon)) return StaffFamily(weapon.PrimaryAnimation);
            if (weapon.Skill == WeaponSkill.Crossbows
                || (weapon.PrimaryRequiresReload && weapon.PrimaryAttackKind == AttackKind.Projectile && !string.IsNullOrWhiteSpace(weapon.AmmoType)))
                return RangedFamily.Crossbow;
            return weapon.ItemType == WeaponItemType.Bow || weapon.Skill == WeaponSkill.Bows ? RangedFamily.Bow : RangedFamily.None;
        }

        public static MeleeFamily Melee(WeaponFacts weapon)
        {
            if (weapon == null) return MeleeFamily.None;
            if (IsSpear(weapon)) return MeleeFamily.Spear;
            if (IsBattleaxe(weapon)) return MeleeFamily.Battleaxe;
            if (weapon.Skill == WeaponSkill.Axes && weapon.ItemType == WeaponItemType.OneHandedWeapon) return MeleeFamily.OneHandedAxe;
            if (weapon.Skill == WeaponSkill.Polearms && weapon.HasSecondary && Eq(weapon.SecondaryAnimation, "atgeir_secondary")) return MeleeFamily.Polearm;
            if (weapon.Skill == WeaponSkill.Swords && weapon.ItemType == WeaponItemType.TwoHandedWeapon) return MeleeFamily.TwoHandedSword;
            if (weapon.Skill == WeaponSkill.Swords && weapon.ItemType == WeaponItemType.OneHandedWeapon && weapon.HasSecondary && Eq(weapon.SecondaryAnimation, "sword_secondary"))
                return MeleeFamily.OneHandedSword;
            if (weapon.Skill == WeaponSkill.Clubs && weapon.ItemType == WeaponItemType.OneHandedWeapon) return MeleeFamily.OneHandedClub;
            if (weapon.Skill == WeaponSkill.Unarmed && weapon.HasSecondary && Eq(weapon.SecondaryAnimation, "unarmed_kick")) return MeleeFamily.Unarmed;
            if (IsSledge(weapon)) return MeleeFamily.Sledge;
            if (weapon.Skill == WeaponSkill.Pickaxes) return MeleeFamily.Pickaxe;
            if (weapon.Skill == WeaponSkill.Knives) return MeleeFamily.Knives;
            return weapon.Skill == WeaponSkill.Farming ? MeleeFamily.Farming : MeleeFamily.None;
        }

        public static bool IsBomb(WeaponFacts weapon) =>
            weapon.PrimaryHasProjectile
            && (weapon.PrimaryAttackKind == AttackKind.Projectile || weapon.PrimaryProjectileIsIProjectile)
            && Eq(weapon.PrimaryAnimation, "throw_bomb");

        public static bool IsOneHandedElementalProjectile(WeaponFacts weapon) =>
            weapon.Skill == WeaponSkill.ElementalMagic && weapon.ItemType == WeaponItemType.OneHandedWeapon && weapon.HasPrimary
            && !string.IsNullOrWhiteSpace(weapon.PrimaryAnimation) && weapon.PrimaryAttackKind == AttackKind.Projectile && weapon.PrimaryHasProjectile;

        public static bool IsOffensiveBloodMagicStaff(WeaponFacts weapon) =>
            weapon.Skill == WeaponSkill.BloodMagic
            && (weapon.ItemType == WeaponItemType.TwoHandedWeapon || weapon.ItemType == WeaponItemType.TwoHandedWeaponLeft)
            && weapon.HasPrimary && weapon.PrimaryAttackKind == AttackKind.Projectile
            && StaffFamily(weapon.PrimaryAnimation) != RangedFamily.None && weapon.PrimaryHasProjectile
            && !weapon.PrimaryProjectileHasAoe && !weapon.PrimaryProjectileHasSpawnAbility;

        private static RangedFamily StaffFamily(string? animation)
        {
            if (Eq(animation, "staff_fireball")) return RangedFamily.FireballStaff;
            if (Eq(animation, "staff_rapidfire")) return RangedFamily.RapidStaff;
            return Eq(animation, "staff_lightningshot") ? RangedFamily.ReloadStaff : RangedFamily.None;
        }

        private static bool IsSpear(WeaponFacts weapon) =>
            weapon.Skill == WeaponSkill.Spears && weapon.HasSecondary && !string.IsNullOrWhiteSpace(weapon.SecondaryAnimation);

        private static bool IsSledge(WeaponFacts weapon) =>
            weapon.Skill == WeaponSkill.Clubs && weapon.ItemType == WeaponItemType.TwoHandedWeapon
            && Contains(weapon.PrefabName, "Sledge")
            && (weapon.PrimaryAttackKind == AttackKind.Area || weapon.SecondaryAttackKind == AttackKind.Area);

        private static bool IsBattleaxe(WeaponFacts weapon)
        {
            if (weapon.Skill != WeaponSkill.Axes || weapon.ItemType != WeaponItemType.TwoHandedWeapon) return false;
            if (Contains(weapon.PrefabName, "DualAxe")) return false;
            var secondary = weapon.HasSecondary ? weapon.SecondaryAnimation ?? "" : "";
            if (!Eq(secondary, "battleaxe_secondary")) return false;
            if (Contains(weapon.PrefabName, "Battleaxe")) return true;
            var primary = weapon.PrimaryAnimation ?? "";
            return (Contains(primary, "battleaxe") || Contains(secondary, "battleaxe")) && !Contains(primary, "dualaxe") && !Contains(secondary, "dualaxe");
        }

        private static bool IsAmmo(WeaponItemType type) => type == WeaponItemType.Ammo || type == WeaponItemType.AmmoNonEquipable;

        private static bool Eq(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static bool Contains(string? text, string part) => (text ?? "").IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
