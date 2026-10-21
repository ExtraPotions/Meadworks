// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/Patches.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>Binds food and mead entries once the item database is available (start scene and main scene).</summary>
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    public static class ObjectDBAwakePatch
    {
        [HarmonyPostfix]
        public static void Postfix() => FeastMasterData.LoadConfigurations();
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
    public static class ObjectDBCopyOtherDBPatch
    {
        [HarmonyPostfix]
        public static void Postfix() => FeastMasterData.LoadConfigurations();
    }

    /// <summary>
    /// Applies configured food values at consumption time as a safety net for item data the database load did not
    /// reach (the values are normally already in the shared data, see <see cref="ItemValues"/>).
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.EatFood))]
    public static class PlayerEatFoodPatch
    {
        public static bool Prepare() => Customized.AnyFoodValues();

        [HarmonyPrefix]
        public static void Prefix(ItemDrop.ItemData item)
        {
            if (FeastMasterData.TryGetFood(item, out var configs))
                ItemValues.Apply(item.m_shared, configs);
        }
    }

    /// <summary>
    /// Applies mead values right before the status effect is cloned onto the player. The target is the
    /// AddStatusEffect overload taking a StatusEffect, picked by its first parameter so trailing parameters
    /// the game adds (as it did with 'variant') do not break the patch.
    /// </summary>
    [HarmonyPatch]
    public static class SEManAddStatusEffectPatch
    {
        public static bool Prepare() => Customized.AnyMead();

        public static MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(SEMan)).First(m =>
                m.Name == nameof(SEMan.AddStatusEffect)
                && m.GetParameters().Length > 0
                && m.GetParameters()[0].ParameterType == typeof(StatusEffect));
        }

        [HarmonyPrefix]
        public static void Prefix(StatusEffect statusEffect)
        {
            if (statusEffect is SE_Stats stats && FeastMasterData.MeadsByEffectHash.TryGetValue(stats.NameHash(), out MeadEffectConfig config))
                ItemValues.Apply(stats, config);
        }
    }
}
