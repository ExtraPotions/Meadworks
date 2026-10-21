// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/ItemValues.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using ExtraPotions.Larder.Runtime.Adopted.FeastMaster.ItemCopies;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Writes the configured values (per item value times the global multiplier) into the items' shared data and
    /// the meads' status effect assets, when the item database loads and whenever a setting changes. Tooltips
    /// and the consume code then read the configured numbers; the consume-time patches stay as a safety net.
    /// Foods are written through ItemCopies: the prefab and every live copy of its shared data (world drops, the
    /// inventory, the foods already eaten, the open container), and new copies as they appear. Meads are written
    /// into the asset and into the clone the local player is currently under.
    /// </summary>
    public static class ItemValues
    {
        private static readonly HashSet<string> foods = new HashSet<string>();
        private static readonly Dictionary<string, SE_Stats> meads = new Dictionary<string, SE_Stats>();
        // Foods and meads written at least once this session; see ShouldWrite.
        private static readonly HashSet<Dictionary<string, ConfigEntry<float>>> writtenFoods = new HashSet<Dictionary<string, ConfigEntry<float>>>();
        private static readonly HashSet<MeadEffectConfig> writtenMeads = new HashSet<MeadEffectConfig>();
        // Changed since the last frame, applied together by Tick: foods and meads by name, or everything.
        private static readonly HashSet<string> changedFoods = new HashSet<string>();
        private static readonly HashSet<string> changedMeads = new HashSet<string>();
        private static bool changedAll;
        private static bool suspended;
        private static Harmony harmony;
        private static bool spawnsHooked;

        /// <summary>The Harmony instance the spawn hooks go on, once a food value is first changed.</summary>
        public static void UseHarmony(Harmony instance) => harmony = instance;

        /// <summary>
        /// Called by <see cref="ChangedRules.Refresh"/> while food values are changed: hooks new item copies (world
        /// items, items entering an inventory) so they get the values too. At defaults the game is left without the
        /// hooks; once installed they stay, and cost one flag test while every food value is back at its default.
        /// </summary>
        public static void HookSpawns()
        {
            if (spawnsHooked || harmony == null)
                return;
            spawnsHooked = true;
            Copies.HookSpawns(harmony, ApplyCopy);
        }

        /// <summary>
        /// Binding an entry that exists in the .cfg raises SettingChanged; while the item database is bound the
        /// handler is switched off and <see cref="ApplyAll"/> runs once at the end instead.
        /// </summary>
        public static void SuspendWhileBinding(bool suspend) => suspended = suspend;

        /// <summary>
        /// Re-applies the items on any setting change: edits in game, file reloads and server pushes. A change only
        /// marks what it touched; <see cref="Tick"/> applies them all on the next frame, so a server pushing many
        /// values one at a time (Charter, on joining) or a file reload costs one pass, with the final values.
        /// </summary>
        public static void HookConfig(ConfigFile config)
        {
            config.SettingChanged += OnSettingChanged;
            config.ConfigReloaded += OnConfigReloaded;
        }

        private static void OnConfigReloaded(object sender, System.EventArgs args) => changedAll = true;
        public static void MarkAll() => changedAll = true;
        internal static void Reset()
        {
            ForgetChanges(); foods.Clear(); meads.Clear(); writtenFoods.Clear(); writtenMeads.Clear(); suspended=false;
        }
        public static void UnhookConfig(ConfigFile config)
        {
            config.SettingChanged -= OnSettingChanged;
            config.ConfigReloaded -= OnConfigReloaded;
            spawnsHooked = false; harmony = null;
            Copies.UnhookSpawns();
        }
        private static void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (suspended)
                return;
            // Foods and meads are both keyed by prefab name, which is also their section name.
            string section = args.ChangedSetting.Definition.Section;
            if (section.StartsWith("Food: ", System.StringComparison.Ordinal)) section = section.Substring(6);
            else if (section.StartsWith("Mead: ", System.StringComparison.Ordinal)) section = section.Substring(6);
            if (foods.Contains(section))
                changedFoods.Add(section);
            else if (meads.ContainsKey(section))
                changedMeads.Add(section);
            else
                changedAll = true;
        }

        /// <summary>Called every frame by <see cref="SwitchTicker"/>: one pass for whatever changed since the last frame.</summary>
        public static void Tick()
        {
            if (changedAll)
                ApplyAll();
            else if (changedFoods.Count > 0 || changedMeads.Count > 0)
                ApplyChanged();
        }

        public static void RegisterFood(string prefabName) => foods.Add(prefabName);

        public static void RegisterMead(string prefabName, SE_Stats effect) => meads[prefabName] = effect;

        public static void ApplyAll()
        {
            ForgetChanges();
            Copies.ApplyAll(ApplyNamed);
            foreach (KeyValuePair<string, SE_Stats> mead in meads)
                ApplyMead(mead.Key, mead.Value);
            Rested.ApplyAll();
            RefreshEatenFood();
        }

        /// <summary>The changed foods in one walk of the prefabs and live copies, then the player's food update; the changed meads.</summary>
        private static void ApplyChanged()
        {
            try
            {
                if (changedFoods.Count > 0)
                {
                    Copies.Apply(changedFoods, ApplyNamed);
                    RefreshEatenFood();
                }
                foreach (string mead in changedMeads)
                    ApplyMead(mead, meads[mead]);
            }
            finally
            {
                ForgetChanges();
            }
        }

        // Cleared before (ApplyAll) or after (ApplyChanged) a pass, so one that throws is not tried again every frame.
        private static void ForgetChanges()
        {
            changedAll = false;
            changedFoods.Clear();
            changedMeads.Clear();
        }

        /// <summary>The ItemCopies.ApplyAll write: foods get their values, every other item is left alone.</summary>
        private static void ApplyNamed(string prefabName, ItemDrop.ItemData.SharedData shared)
        {
            if (FeastMasterData.FoodConfigs.TryGetValue(prefabName, out Dictionary<string, ConfigEntry<float>> configs))
                Apply(shared, configs);
        }

        /// <summary>The ItemCopies.HookSpawns callback: a new world item or an item entering an inventory.</summary>
        public static void ApplyCopy(ItemDrop.ItemData item)
        {
            if (ChangedRules.FoodValues && FeastMasterData.TryGetFood(item, out Dictionary<string, ConfigEntry<float>> configs))
                Apply(item.m_shared, configs);
        }

        /// <summary>
        /// Writes the configured values of one food into its shared data, once any of them (or a global multiplier) is
        /// changed: a food at its defaults keeps whatever the game or another mod gave it. A food set back to its
        /// defaults is still written, with those defaults (the values it had when it was bound), so it returns to them.
        /// </summary>
        public static void Apply(ItemDrop.ItemData.SharedData shared, Dictionary<string, ConfigEntry<float>> configs)
        {
            if (!ShouldWrite(writtenFoods, configs, Customized.FoodValues(configs)))
                return;
            shared.m_food = NativeFeastMaster.Value(configs[FeastMasterData.Health]) * NativeFeastMaster.Value(FeastMasterData.HealthModifier);
            shared.m_foodStamina = NativeFeastMaster.Value(configs[FeastMasterData.Stamina]) * NativeFeastMaster.Value(FeastMasterData.StaminaModifier);
            shared.m_foodBurnTime = NativeFeastMaster.Value(configs[FeastMasterData.Duration]) * NativeFeastMaster.Value(FeastMasterData.DurationModifier);
            shared.m_foodRegen = NativeFeastMaster.Value(configs[FeastMasterData.HealthRegen]) * NativeFeastMaster.Value(FeastMasterData.HealthRegenModifier);
            shared.m_foodEitr = NativeFeastMaster.Value(configs[FeastMasterData.Eitr]) * NativeFeastMaster.Value(FeastMasterData.EitrModifier);
        }

        /// <summary>
        /// The eaten foods point at their own shared data copies, written above; the game recomputes max health,
        /// stamina and eitr from them only on its next food tick, so it is forced now (the call the game itself
        /// makes after eating; UpdateFood is private, the assembly is publicized).
        /// </summary>
        private static void RefreshEatenFood()
        {
            Player player = Player.m_localPlayer;
            if (player != null && player.GetFoods().Count > 0)
                player.UpdateFood(0f, true);
        }

        /// <summary>One mead: its status effect asset, then the clone the local player may be under right now.</summary>
        private static void ApplyMead(string prefabName, SE_Stats effect)
        {
            if (effect == null || !FeastMasterData.MeadConfigs.TryGetValue(prefabName, out MeadEffectConfig config))
                return;
            Apply(effect, config);
            RefreshActiveMead(effect.NameHash(), config);
        }

        /// <summary>
        /// SEMan.AddStatusEffect clones the asset onto the player, so the running effect is a separate object;
        /// the configured values are written into that clone too, keeping its progress through the duration.
        /// </summary>
        private static void RefreshActiveMead(int nameHash, MeadEffectConfig config)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return;
            foreach (StatusEffect active in player.GetSEMan().GetStatusEffects())
            {
                if (active is SE_Stats stats && stats.NameHash() == nameHash)
                    ApplyKeepingProgress(stats, config);
            }
        }

        /// <summary>
        /// The effect counts m_time up from 0 and ends when it passes m_ttl. The same fraction of the duration
        /// stays remaining after a Duration change: m_time is rescaled by new ttl / old ttl instead of reset.
        /// </summary>
        private static void ApplyKeepingProgress(SE_Stats stats, MeadEffectConfig config)
        {
            float elapsedFraction = stats.m_ttl > 0f ? stats.m_time / stats.m_ttl : 0f;
            Apply(stats, config);
            stats.m_time = elapsedFraction * stats.m_ttl;
        }

        /// <summary>Writes the configured values of one mead into its status effect (the asset or a clone), like a food's.</summary>
        public static void Apply(SE_Stats effect, MeadEffectConfig config)
        {
            if (!ShouldWrite(writtenMeads, config, Customized.Mead(config)))
                return;
            effect.m_ttl = NativeFeastMaster.Value(config.Duration);
            effect.m_healthOverTime = NativeFeastMaster.Value(config.HealthOverTime);
            effect.m_staminaOverTime = NativeFeastMaster.Value(config.StaminaOverTime);
            effect.m_eitrOverTime = NativeFeastMaster.Value(config.EitrOverTime);
            effect.m_healthRegenMultiplier = NativeFeastMaster.Value(config.HealthRegenMultiplier);
            effect.m_staminaRegenMultiplier = NativeFeastMaster.Value(config.StaminaRegenMultiplier);
            effect.m_eitrRegenMultiplier = NativeFeastMaster.Value(config.EitrRegenMultiplier);
            effect.m_runStaminaDrainModifier = NativeFeastMaster.Value(config.RunStaminaModifier);
            effect.m_jumpStaminaUseModifier = NativeFeastMaster.Value(config.JumpStaminaModifier);
        }

        private static bool ShouldWrite<T>(HashSet<T> written, T item, bool customized)
        {
            if (customized)
                written.Add(item);
            return customized || written.Contains(item);
        }

        /// <summary>
        /// The Vigor of a food in percent: its own Vigor entry plus its configured Stamina times Vigor Per Stamina
        /// Point, scaled by Vigor Multiplier. Every term is read live, so all three hot reload. 0 for unknown items.
        /// </summary>
        public static float VigorOf(ItemDrop.ItemData item)
        {
            if (!FeastMasterData.TryGetFood(item, out Dictionary<string, ConfigEntry<float>> configs))
                return 0f;
            float stamina = NativeFeastMaster.Value(configs[FeastMasterData.Stamina]) * NativeFeastMaster.Value(FeastMasterData.StaminaModifier);
            float derived = stamina * NativeFeastMaster.Value(Settings.VigorPerStaminaPoint);
            return (NativeFeastMaster.Value(configs[FeastMasterData.Vigor]) + derived) * NativeFeastMaster.Value(Settings.VigorMultiplier);
        }

        /// <summary>The Eitr Vigor of a food in percent, built like <see cref="VigorOf"/> from its configured Eitr.</summary>
        public static float EitrVigorOf(ItemDrop.ItemData item)
        {
            if (!FeastMasterData.TryGetFood(item, out Dictionary<string, ConfigEntry<float>> configs))
                return 0f;
            float eitr = NativeFeastMaster.Value(configs[FeastMasterData.Eitr]) * NativeFeastMaster.Value(FeastMasterData.EitrModifier);
            float derived = eitr * NativeFeastMaster.Value(Settings.EitrVigorPerEitrPoint);
            return (NativeFeastMaster.Value(configs[FeastMasterData.EitrVigor]) + derived) * NativeFeastMaster.Value(Settings.EitrVigorMultiplier);
        }
    }
}
