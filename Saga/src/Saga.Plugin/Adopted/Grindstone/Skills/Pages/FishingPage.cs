// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Skills/Pages/FishingPage.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Fishing's page in the info pane: the game's own reel speed and stamina (read from the fishing float prefab, whose
    /// values the game serializes rather than codes), then the bites, the fight and the catch at the player's level, and
    /// the angler's perks with the level each needs. The numbers are the synced settings scaled by the level exactly as
    /// the features scale them (<see cref="FishSkill.Share"/>, <see cref="Strike.Window"/>, <see cref="Tension.Change"/>,
    /// <see cref="Snags.Chance"/>, and <see cref="BonusItems"/>'s blend from each fish's own chance).
    /// </summary>
    public static class FishingPage
    {
        private static ZNetScene searched;
        private static FishingFloat floatPrefab;

        public static void Write(SkillPage page)
        {
            Game(page);
            if (!FishSkill.Active)
            {
                page.Line(SagaText.Text("gs_13a3a1ebfb9d8753"));
                return;
            }
            page.About = SagaText.Text("gs_1478cf9dc5d484c5");
            Fight(page);
            Catch(page);
            Perks(page);
        }

        /// <summary>What the game itself does with the level: FishingFloat.FixedUpdate lerps these by the skill factor.</summary>
        private static void Game(SkillPage page)
        {
            FishingFloat prefab = FloatPrefab();
            if (prefab == null)
                return;
            float factor = page.Factor;
            float speed = Mathf.Lerp(prefab.m_pullLineSpeed, prefab.m_pullLineSpeedMaxSkill, factor);
            page.Line(SagaText.Format("gs_2591555d34ee4933", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.#}", speed)), SagaText.Text("gs_2efc306c96f4b57f"),
                SagaText.Text("gs_a6cd7291678fc0d9"));
            float saved = 1f - Mathf.Lerp(1f, prefab.m_pullStaminaUseMaxSkillMultiplier, factor);
            float hold = Mathf.Lerp(prefab.m_hookedStaminaPerSec, prefab.m_hookedStaminaPerSecMaxSkill, factor);
            page.Line(SagaText.Format("gs_bcbd0ac63c4c5aa9", SkillPage.Percent(saved)), SagaText.Text("gs_e9b2fca7c3d4f4fc"),
                SagaText.Format("gs_891e71c5ed504242", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##}", hold)));
        }

        private static void Fight(SkillPage page)
        {
            float level = page.Level;
            page.Line(SagaText.Format("gs_b8e3d70d9e76ce8e", Share(FishingBiteSettings.BiteChanceAt100.Value, level)), SagaText.Text("gs_218ca700354d19d1"), BiteTip());
            page.Heading(SagaText.Text("gs_f017bede6e90f41e"));
            page.Line(SagaText.Format("gs_40c6aad243c2d0e9", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##}", Strike.Window(level))), SagaText.Text("gs_38574833c7d66117"),
                SagaText.Text("gs_7dfb73c5c5fc98a4"));
            if (FishingFightSettings.TensionEnabled.Value)
                page.Line(SagaText.Format("gs_c8729aa378761c61", SkillPage.Percent(Tension.Change(true, true, level))), SagaText.Text("gs_56f09078a754a639"), TensionTip());
        }

        private static void Catch(SkillPage page)
        {
            float level = page.Level;
            page.Heading(SagaText.Text("gs_46e625c2053df471"));
            if (FishingBigFishSettings.BigOneChanceAt100.Value > 0f)
                page.Line(SagaText.Format("gs_3217f462f3f0b458", Share(FishingBigFishSettings.BigOneChanceAt100.Value, level)), SagaText.Text("gs_86bc281b3d2ff560"), BigOneTip());
            string bonus = BonusChance(level);
            if (bonus.Length > 0)
                page.Line(SagaText.Format("gs_aec28b4c6b59b915", bonus), SagaText.Text("gs_ad117c9953388b72"), SagaText.Text("gs_2c2c75c10b1a929b"));
            if (FishingCatchSettings.BaitSaverAt100.Value > 0f)
                page.Line(SagaText.Format("gs_e59551dfb001b373", Share(FishingCatchSettings.BaitSaverAt100.Value, level)), SagaText.Text("gs_28752453b781c3d1"),
                    SagaText.Text("gs_af738a5e100f06db"));
            float snag = Snags.Chance(level);
            if (snag > 0f)
                page.Line(SagaText.Format("gs_810af1db8a996b6a", SkillPage.Percent(snag)), SagaText.Text("gs_e3bd07ed1963c185"), SnagTip());
            if (FishingCatchSettings.CastDistanceAt100.Value > 0f || FishingCatchSettings.LineLengthAt100.Value > 0f)
                page.Line(SagaText.Format("gs_feeef1f38ff2a337", Share(FishingCatchSettings.CastDistanceAt100.Value, level), Share(FishingCatchSettings.LineLengthAt100.Value, level)),
                    "line", LineTip(level));
        }

        private static void Perks(SkillPage page)
        {
            float perfect = FishingFightSettings.PerfectStrikeWindow.Value;
            if (perfect > 0f)
                page.Perk(SagaText.Text("gs_a00b47415b096419"), 0f, SagaText.Format("gs_d9e27e08daf93543", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##}", perfect)));
            page.Perk(SagaText.Text("gs_406d947d1b246d00"), 0f, LogTip());
            page.Perk(SagaText.Text("gs_8a2e19973c5fa7ff"), FishingBiteSettings.SpeciesSenseLevel.Value,
                SagaText.Text("gs_adf3480e6adceb6a"));
            page.Perk(SagaText.Text("gs_15a4ba09562b90ed"), FishingBiteSettings.SizeSenseLevel.Value,
                SagaText.Text("gs_b5d9d7ff176a9abb"));
            page.Perk(SagaText.Text("gs_e004ebdda26a46d7"), FishingCatchSettings.DoubleBonusLevel.Value, SagaText.Text("gs_b814f50ea435423c"));
            float legendary = FishingBigFishSettings.LegendaryChance.Value > 0f ? FishingBigFishSettings.LegendaryLevel.Value : FishSkill.MaxLevel + 1f;
            page.Perk(SagaText.Text("gs_d951d996a4d3eb57"), legendary, LegendaryTip());
            page.Perk(SagaText.Text("gs_f2465f78e06e9352"), FishingFightSettings.GraceLevel.Value,
                SagaText.Format("gs_b4554c590dc62bc6", SkillPage.Duration(FishingFightSettings.GraceSeconds.Value)));
            page.Perk(SagaText.Text("gs_b201b89f9bf0f2b7"), FishingBiteSettings.WaterSenseLevel.Value,
                SagaText.Text("gs_034cbcc186dd2171"));
        }

        private static string BiteTip()
        {
            string tip = SagaText.Format("gs_8f64aae2bbc9e0ac", SkillPage.Number(FishingBiteSettings.DawnDuskBonus.Value));
            string rain = SagaText.Format("gs_bd816cb1abdc25b6", SkillPage.Number(FishingBiteSettings.RainBonus.Value));
            string chum = ChumNames();
            if (chum.Length == 0)
                return tip + SagaText.Format("gs_5ebf06c7cd409992", rain);
            return tip + SagaText.Format("gs_3da29b63afb83b79", rain, SkillPage.Number(FishingBiteSettings.ChumBonus.Value), SkillPage.Number(FishingBiteSettings.ChumRadius.Value), chum);
        }

        private static string TensionTip()
        {
            string ease = SkillPage.Percent(FishSkill.Percent(FishingFightSettings.TensionEase.Value));
            string tip = SagaText.Format("gs_589347068998d560", ease);
            int thrashes = FishingFightSettings.ThrashesToTire.Value;
            return thrashes > 0
                ? tip + SagaText.Format("gs_790e758157119cb9", thrashes, SkillPage.Number(FishingFightSettings.SpentReelSpeed.Value))
                : tip;
        }

        private static string BigOneTip() =>
            SagaText.Format("gs_cf9dd6df4c292cdd", FishInfo.MaxNaturalLevel, SkillPage.Number(FishingBigFishSettings.NightBonus.Value));

        private static string SnagTip() =>
            SagaText.Format("gs_7c6e7ace4ceacb40", SkillPage.Duration(FishingCatchSettings.SnagWait.Value))
            + SagaText.Text("gs_6a0621159c9274d7");

        private static string LineTip(float level)
        {
            FishingFloat prefab = FloatPrefab();
            if (prefab == null)
                return SagaText.Text("gs_b0f69ca9bdff383c");
            float reach = prefab.m_maxDistance * (1f + FishSkill.Share(FishingCatchSettings.LineLengthAt100.Value, level));
            return SagaText.Format("gs_2d03756b4852b183", SkillPage.Number(reach));
        }

        private static string LogTip()
        {
            string tip = SagaText.Text("gs_c6dbc0503a5d8a01");
            return FishingExperienceSettings.Discovery.Value > 0f || FishingExperienceSettings.NewSize.Value > 0f
                ? tip + SagaText.Text("gs_e3c4ea656a75354e")
                : tip;
        }

        private static string LegendaryTip()
        {
            int thrashes = FishingBigFishSettings.LegendaryThrashes.Value;
            string tires = thrashes > 0 ? SagaText.Format("gs_e7cc1239c97acc22", thrashes) : SagaText.Text("gs_250bbb03558fb265");
            return SagaText.Format("gs_9e2befdd796790f4", SkillPage.Number(FishingBigFishSettings.LegendaryChance.Value), FishInfo.LegendaryLevel, tires)
                + SagaText.Text("gs_7783aa9073af9661");
        }

        /// <summary>
        /// A landed fish's bonus-item chance at this level, as <see cref="BonusItems.Prepare"/> raises each species' own:
        /// from the fish's chance at 0 to Bonus Item Chance At 100, never below its own. A range when species differ; ""
        /// when no fish has a bonus table.
        /// </summary>
        private static string BonusChance(float level)
        {
            float target = FishSkill.Percent(FishingCatchSettings.BonusItemChanceAt100.Value);
            float low = 1f;
            float high = 0f;
            foreach (GameObject prefab in FishInfo.Species())
            {
                DropTable table = prefab.GetComponent<Fish>().m_extraDrops;
                if (table == null || table.IsEmpty())
                    continue;
                float chance = Mathf.Max(table.m_dropChance, Mathf.Lerp(table.m_dropChance, target, FishSkill.Factor(level)));
                low = Mathf.Min(low, chance);
                high = Mathf.Max(high, chance);
            }
            if (high < low)
                return "";
            return Mathf.Approximately(low, high) ? SkillPage.Percent(low) : $"{SkillPage.Percent(low)}-{SkillPage.Percent(high)}";
        }

        /// <summary>The Chum Items setting as item names players read ("Entrails, Blood bag"); "" when chum is off.</summary>
        private static string ChumNames()
        {
            List<string> names = new List<string>();
            foreach (string part in (FishingBiteSettings.ChumItems.Value ?? "").Split(','))
            {
                if (part.Trim().Length > 0)
                    names.Add(ItemName(part.Trim()));
            }
            return string.Join(", ", names);
        }

        private static string ItemName(string prefabName)
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
            ItemDrop item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return item != null ? FishInfo.Localize(item.m_itemData.m_shared.m_name) : prefabName;
        }

        /// <summary>
        /// The game's fishing float prefab, looked up once per scene: its reel speed, stamina and line length are fields the
        /// prefab serializes, so they are read rather than written down here. Null when there is none.
        /// </summary>
        private static FishingFloat FloatPrefab()
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null || scene == searched)
                return floatPrefab;
            searched = scene;
            floatPrefab = null;
            foreach (GameObject prefab in scene.m_prefabs)
            {
                floatPrefab = prefab != null ? prefab.GetComponent<FishingFloat>() : null;
                if (floatPrefab != null)
                    break;
            }
            return floatPrefab;
        }

        private static string Share(float percentAt100, float level) => SkillPage.Percent(FishSkill.Share(percentAt100, level));
    }
}
