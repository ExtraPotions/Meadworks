// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Unity-free view of an item prefab that the family and preset rules read. The plugin fills it
// from ItemDrop.ItemData.SharedData (m_skillType, m_itemType, m_attack, m_secondaryAttack, m_ammoType).
using System;

namespace ExtraPotions.Edge.Secondaries
{
    /// <summary>Mirror of the game's <c>Skills.SkillType</c> values Edge distinguishes, by name; Other for the rest.</summary>
    public enum WeaponSkill { None, Swords, Knives, Clubs, Polearms, Spears, Blocking, Axes, Bows, ElementalMagic, BloodMagic, Unarmed, Pickaxes, WoodCutting, Crossbows, Farming, Other }

    /// <summary>Mirror of the game's <c>ItemDrop.ItemData.ItemType</c> values Edge distinguishes, by name; Other for the rest.</summary>
    public enum WeaponItemType { OneHandedWeapon, TwoHandedWeapon, TwoHandedWeaponLeft, Bow, Shield, Torch, Tool, Ammo, AmmoNonEquipable, Other }

    /// <summary>Mirror of the game's <c>Attack.AttackType</c> by name; None also stands for a missing attack.</summary>
    public enum AttackKind { None, Horizontal, Vertical, Projectile, Area, TriggerProjectile }

    public sealed class WeaponFacts
    {
        public string PrefabName { get; set; } = "";
        public WeaponSkill Skill { get; set; }
        public WeaponItemType ItemType { get; set; }

        /// <summary>The item has a primary attack (<c>m_attack</c>).</summary>
        public bool HasPrimary { get; set; }
        public string PrimaryAnimation { get; set; } = "";
        public AttackKind PrimaryAttackKind { get; set; }

        /// <summary>The primary attack's projectile prefab carries a <c>Projectile</c> component.</summary>
        public bool PrimaryHasProjectile { get; set; }

        /// <summary>The primary projectile has an Aoe component in its hierarchy, including inactive children.</summary>
        public bool PrimaryProjectileHasAoe { get; set; }

        /// <summary>The primary projectile has a SpawnAbility in its hierarchy, including inactive children.</summary>
        public bool PrimaryProjectileHasSpawnAbility { get; set; }

        /// <summary>The primary attack's projectile prefab carries an <c>IProjectile</c> implementation.</summary>
        public bool PrimaryProjectileIsIProjectile { get; set; }

        /// <summary>The primary attack's projectile carries a <c>SpawnAbility</c> with at least one spawn prefab.</summary>
        public bool PrimarySpawnsSummon { get; set; }
        public bool PrimaryRequiresReload { get; set; }
        public string AmmoType { get; set; } = "";

        /// <summary>The item has a secondary attack (<c>m_secondaryAttack</c>) of any origin.</summary>
        public bool HasSecondary { get; set; }
        public string SecondaryAnimation { get; set; } = "";
        public AttackKind SecondaryAttackKind { get; set; }

        /// <summary>A bomb whose projectile explodes over an area (design A2 Auto rule).</summary>
        public bool BombHasArea { get; set; }

        public WeaponFacts Clone() => (WeaponFacts)MemberwiseClone();

        /// <summary>A copy changed by <paramref name="change"/>.</summary>
        public WeaponFacts With(Action<WeaponFacts> change)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));
            var copy = Clone();
            change(copy);
            return copy;
        }
    }
}
