// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Setting names, defaults and ranges follow design section 4; the SecondaryAttacks, Slope Combat
// Assistance and KeepWeaponsAfterSwim options they replace are recorded in the design's Source column (names only).
using System;
using System.Collections.Generic;
using static ExtraPotions.Edge.Config.SettingScope;

namespace ExtraPotions.Edge.Config
{
    /// <summary>
    /// Every Edge setting of design section 4 in file order, then the product-managed hidden entries: one synchronized
    /// carrier per YAML family (the C01 text push of design 4.5) and the converter's import marker. The plugin binds
    /// exactly this list; the portable tests compare it with the spec tables.
    /// </summary>
    public static class EdgeSettings
    {
        public static class General
        {
            public const string Section = "1 - General";

            public static readonly EdgeSetting<bool> Enabled = EdgeSetting.Toggle(Section, "Enabled", true, ServerAuthoritative, "Whole product. Off: attacks, swimming and slopes behave as the game does.");
            public static readonly EdgeSetting<bool> SecondariesEnabled = EdgeSetting.Toggle(Section, "Secondaries Enabled", true, ServerAuthoritative, "Adds a secondary attack to weapons, chosen by the presets below. Off: weapons use only the game's attacks.");
            public static readonly EdgeSetting<bool> QuickstepEnabled = EdgeSetting.Toggle(Section, "Quickstep Enabled", true, ServerAuthoritative, "Quickstep dash for knives and Unarmed-skill weapons.");
            public static readonly EdgeSetting<bool> SlopeAssistEnabled = EdgeSetting.Toggle(Section, "Slope Assist Enabled", true, ServerAuthoritative, "Melee swings connect with creatures up or down a slope.");
            public static readonly EdgeSetting<bool> EquipmentEnabled = EdgeSetting.Toggle(Section, "Equipment Enabled", true, Local, "Weapons back in hand after a swim and the last shield coming back with a one-handed weapon.");
            public static readonly EdgeSetting<bool> DodgeCancel = EdgeSetting.Toggle(Section, "Dodge Cancel", false, ServerAuthoritative, "Dodge or press block during an attack to cancel it. Uses the game's controls and normal dodge cost.");
            public static readonly EdgeSetting<bool> DodgeCancelOnlyBeforeHit = EdgeSetting.Toggle(Section, "Dodge Cancel Only Before The Hit", false, ServerAuthoritative, "Dodge Cancel works only until the attack lands its hit. Off: an attack can also be canceled during its follow-through. Host or server; default off; applies at once.");
            public static readonly EdgeSetting<float> DodgeCancelExtraStaminaCost = EdgeSetting.Number(Section, "Dodge Cancel Extra Stamina Cost", 0f, 0f, 100f, ServerAuthoritative, "Stamina a Dodge Cancel costs on top of the dodge. Without enough stamina the attack is not canceled. 0 adds no cost. Host or server; applies at once.");
            public static readonly EdgeSetting<bool> InscriptionCooldownBonus = EdgeSetting.Toggle(Section, "Inscription Cooldown Bonus", true, ServerAuthoritative, "Quickened and Swift Casting on a weapon (Inscribe inscriptions) shorten its secondary cooldowns, at most by half. Off: secondary cooldowns ignore inscriptions. Inscribe's Inscription Effects setting also turns this off. Host or server; default on; applies at once.");
        }

        public static class Ranged
        {
            public const string Section = "2 - Ranged";

            private const string RangedChoices = " Barrage: fires several projectiles in a spread. Volley: lobs several projectiles that land around the aimed point. "
                + "Piercing: shots pass through several targets, weaker after each hit. Scatter: splits into ricocheting projectiles on its first surface hit. "
                + "Spiral: fires projectiles in a turning spiral around the aim. Sentinel: hovering projectiles hunt nearby enemies. "
                + "Meteor: calls projectiles down from above around the aimed point. Burst: repeats the weapon's full shot several times in a row. "
                + "Off: no Edge secondary; the weapon keeps its own secondary attack, if it has one.";

            public static readonly EdgeSetting<RangedPreset> OneHandedElementalPreset = EdgeSetting.Choice(Section, "One-Handed Elemental Weapon Preset", RangedPreset.Barrage, ServerAuthoritative, "Secondary attack of one-handed elemental weapons." + RangedChoices);
            public static readonly EdgeSetting<RangedPreset> FireballStaffPreset = EdgeSetting.Choice(Section, "Fireball Staff Preset", RangedPreset.Sentinel, ServerAuthoritative, "Secondary attack of fireball staves." + RangedChoices);
            public static readonly EdgeSetting<RangedPreset> RapidfireStaffPreset = EdgeSetting.Choice(Section, "Rapidfire Staff Preset", RangedPreset.Spiral, ServerAuthoritative, "Secondary attack of rapid-fire staves." + RangedChoices);
            public static readonly EdgeSetting<RangedPreset> LightningStaffPreset = EdgeSetting.Choice(Section, "Lightning Staff Preset", RangedPreset.Burst, ServerAuthoritative, "Secondary attack of lightning staves." + RangedChoices);
            public static readonly EdgeSetting<RangedPreset> BowPreset = EdgeSetting.Choice(Section, "Bow Preset", RangedPreset.Barrage, ServerAuthoritative, "Secondary attack of bows." + RangedChoices);
            public static readonly EdgeSetting<RangedPreset> CrossbowPreset = EdgeSetting.Choice(Section, "Crossbow Preset", RangedPreset.Burst, ServerAuthoritative, "Secondary attack of crossbows." + RangedChoices);
            public static readonly EdgeSetting<BombPreset> BombPreset = EdgeSetting.Choice(Section, "Bomb Preset", Config.BombPreset.Auto, ServerAuthoritative, "Secondary attack of bombs. StickyDetonator: throws charges that stick; block to detonate them. OverchargedBomb: spends extra bombs for a bigger, stronger blast. Auto: Overcharged Bomb when the bomb has an area, else Sticky Detonator. Off: no Edge secondary; the bomb keeps its own secondary attack, if it has one.");
            public static readonly EdgeSetting<int> ArrowRecoveryChance = EdgeSetting.Integer(Section, "Arrow Recovery Chance", 25, 0, 100, ServerAuthoritative, "Percent chance that an arrow you shot can be picked up where it hit the ground or an object. Arrows that hit creatures, land in water or burn are lost. 0 turns it off.");
            public static readonly EdgeSetting<bool> ArrowsToCrosshair = EdgeSetting.Toggle(Section, "Arrows To Crosshair", false, ServerAuthoritative, "Your arrows, bolts, thrown weapons and staff shots leave toward the point under the crosshair instead of parallel to the camera, so near targets are hit where you aim. Only the launch direction changes; gravity, speed and spread stay the game's. Host or server; default off; applies at once. Your own value applies on servers without Edge.");
        }

        public static class BloodMagic
        {
            public const string Section = "3 - Blood Magic";

            public static readonly EdgeSetting<SummonQualityPreset> SummonQualityPreset = EdgeSetting.Choice(Section, "Summon Quality Preset", Config.SummonQualityPreset.LevelByQuality, ServerAuthoritative, "Staff quality raises summon level or summon count.");
            public static readonly EdgeSetting<int> SummonLifetimeSeconds = EdgeSetting.Integer(Section, "Summon Lifetime Seconds", 1200, 0, 86400, ServerAuthoritative, "Base summon lifetime in seconds; 0 turns lifetimes off.");
            public static readonly EdgeSetting<float> LifetimeMultiplierAtSkill100 = EdgeSetting.Number(Section, "Lifetime Multiplier At Skill 100", 2f, 1f, 10f, ServerAuthoritative, "Lifetime multiplier at Blood Magic 100, scaled linearly from 1 at skill 0.");
            public static readonly EdgeSetting<bool> HealthCostUsesMaxHealth = EdgeSetting.Toggle(Section, "Health Cost Uses Max Health", true, ServerAuthoritative, "Blood Magic health costs are a share of maximum health.");
            public static readonly EdgeSetting<float> HealthCostSkillRaiseFactor = EdgeSetting.Number(Section, "Health Cost Skill Raise Factor", 0.01f, 0f, 0.1f, ServerAuthoritative, "Blood Magic experience per health point consumed.");
        }

        public static class Slope
        {
            public const string Section = "4 - Slope";

            public static readonly EdgeSetting<SlopeMode> Mode = EdgeSetting.Choice(Section, "Mode", SlopeMode.Fixed, ServerAuthoritative, "Off, Fixed (attack height and offset) or Adaptive (also follows the camera pitch on slopes and toward targets above or below).");
            public static readonly EdgeSetting<float> AttackHeight = EdgeSetting.Number(Section, "Attack Height", 1f, 0f, 5f, ServerAuthoritative, "Height in meters the player's melee sweep starts from (the game uses 0.6).");
            public static readonly EdgeSetting<float> AttackOffset = EdgeSetting.Number(Section, "Attack Offset", 0f, 0f, 5f, ServerAuthoritative, "Forward offset in meters of the player's melee sweep.");
            public static readonly EdgeSetting<float> AdaptiveSlopeThreshold = EdgeSetting.Number(Section, "Adaptive Slope Threshold", 12f, 0f, 45f, ServerAuthoritative, "Adaptive: ground steeper than this many degrees lets swings follow the camera pitch.");
            public static readonly EdgeSetting<float> AdaptiveMaxPitch = EdgeSetting.Number(Section, "Adaptive Max Pitch", 35f, 0f, 70f, ServerAuthoritative, "Adaptive: most degrees a swing may tilt up or down.");
        }

        public static class Equipment
        {
            public const string Section = "5 - Equipment";

            public static readonly EdgeSetting<SwimMode> SwimMode = EdgeSetting.Choice(Section, "Swim Mode", Config.SwimMode.AutoRestore, Local, "AutoRestore: weapons hidden by swimming come back after leaving the water. KeepInHand: never hidden while swimming. Off: as the game does.");
            public static readonly EdgeSetting<float> RestoreDelay = EdgeSetting.Number(Section, "Restore Delay", 0.5f, 0f, 5f, Local, "Seconds out of the water before weapons come back (avoids flicker at the shoreline).");
            public static readonly EdgeSetting<bool> AutoEquipLastShield = EdgeSetting.Toggle(Section, "Auto Equip Last Shield", true, Local, "Equipping a one-handed weapon with an empty left hand also equips the shield you used last.");
            public static readonly EdgeSetting<bool> KeepCrossbowLoaded = EdgeSetting.Toggle(Section, "Keep Crossbow Loaded", true, ServerAuthoritative, "A crossbow put away loaded stays loaded when drawn again. Firing still empties it.");
            public static readonly EdgeSetting<bool> ReturnThrownWeapons = EdgeSetting.Toggle(Section, "Return Thrown Weapons", true, ServerAuthoritative, "A thrown weapon that is not used up, such as a spear or a secondary-attack throw, goes back into your inventory in the slot it left when it lands. If that slot is taken it uses a free slot; with no room it falls where it landed.");
        }

        public static class Interface
        {
            public const string Section = "6 - Interface";

            public static readonly EdgeSetting<bool> CooldownHud = EdgeSetting.Toggle(Section, "Cooldown HUD", true, Local, "Show secondary cooldowns and charges.");
            public static readonly EdgeSetting<bool> SecondaryTooltips = EdgeSetting.Toggle(Section, "Secondary Tooltips", true, Local, "Weapon tooltips name the secondary preset.");
            public static readonly EdgeSetting<bool> KeyHints = EdgeSetting.Toggle(Section, "Key Hints", true, Local, "Show secondary and detonate key hints.");
            public static readonly EdgeSetting<bool> BowZoom = EdgeSetting.Toggle(Section, "Bow Zoom", true, Local, "The camera zooms in as you draw a bow and back out when you release.");
            public static readonly EdgeSetting<float> BowZoomFactor = EdgeSetting.Number(Section, "Bow Zoom Factor", 1.5f, 1f, 3f, Local, "Magnification at full draw (1 means no zoom).");
            public static readonly EdgeSetting<bool> DrawIndicator = EdgeSetting.Toggle(Section, "Draw Indicator", true, Local, "A small bar under the crosshair fills as you draw a bow and lights up at full draw.");
        }

        public static class Cheats
        {
            public const string Section = "7 - Cheats";

            public static readonly EdgeSetting<bool> NoSecondaryCooldowns = EdgeSetting.Toggle(Section, "No Secondary Cooldowns", false, Cheat,
                "Cheat. Secondary attacks have no cooldown. Each hit still keeps its short built-in delay.", safe: false);
        }

        /// <summary>Hidden synchronized carriers of the YAML families (design 4.5, C01 server push).</summary>
        public static class Data
        {
            public const string Section = "8 - Data";

            public static readonly EdgeSetting<string> RangedRules = EdgeSetting.Text(Section, "Ranged Rules", "", ServerAuthoritative, "Managed by Edge: the server's Edge.Ranged rules.", TextRules.Verbatim, hidden: true);
            public static readonly EdgeSetting<string> MeleeRules = EdgeSetting.Text(Section, "Melee Rules", "", ServerAuthoritative, "Managed by Edge: the server's Edge.Melee rules.", TextRules.Verbatim, hidden: true);
            public static readonly EdgeSetting<string> BloodMagicRules = EdgeSetting.Text(Section, "Blood Magic Rules", "", ServerAuthoritative, "Managed by Edge: the server's Edge.BloodMagic rules.", TextRules.Verbatim, hidden: true);
        }

        /// <summary>Hidden converter marker (C09): the sources this machine already imported.</summary>
        public static class Import
        {
            public const string Section = "9 - Import";

            public static readonly EdgeSetting<string> ImportedSources = EdgeSetting.Text(Section, "Imported Sources", "", Local, "Managed by Edge.", TextRules.Verbatim, hidden: true);
        }

        /// <summary>Every setting in file order.</summary>
        public static IReadOnlyList<EdgeSetting> All { get; } = Array.AsReadOnly(new EdgeSetting[]
        {
            General.Enabled, General.SecondariesEnabled, General.QuickstepEnabled, General.SlopeAssistEnabled, General.EquipmentEnabled, General.DodgeCancel, General.DodgeCancelOnlyBeforeHit, General.DodgeCancelExtraStaminaCost, General.InscriptionCooldownBonus,
            Ranged.OneHandedElementalPreset, Ranged.FireballStaffPreset, Ranged.RapidfireStaffPreset, Ranged.LightningStaffPreset,
            Ranged.BowPreset, Ranged.CrossbowPreset, Ranged.BombPreset, Ranged.ArrowRecoveryChance, Ranged.ArrowsToCrosshair,
            BloodMagic.SummonQualityPreset, BloodMagic.SummonLifetimeSeconds, BloodMagic.LifetimeMultiplierAtSkill100,
            BloodMagic.HealthCostUsesMaxHealth, BloodMagic.HealthCostSkillRaiseFactor,
            Slope.Mode, Slope.AttackHeight, Slope.AttackOffset, Slope.AdaptiveSlopeThreshold, Slope.AdaptiveMaxPitch,
            Equipment.SwimMode, Equipment.RestoreDelay, Equipment.AutoEquipLastShield, Equipment.KeepCrossbowLoaded, Equipment.ReturnThrownWeapons,
            Interface.CooldownHud, Interface.SecondaryTooltips, Interface.KeyHints,
            Interface.BowZoom, Interface.BowZoomFactor, Interface.DrawIndicator,
            Cheats.NoSecondaryCooldowns,
            Data.RangedRules, Data.MeleeRules, Data.BloodMagicRules,
            Import.ImportedSources,
        });
    }
}
