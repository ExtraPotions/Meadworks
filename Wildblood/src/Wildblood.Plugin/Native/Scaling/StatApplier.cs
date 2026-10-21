// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Horde adoption adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; Scale Saddle-able Creatures off skips the star size of a saddle-able creature; the health the Distance Scaling band gave at the roll multiplies the starred maximum.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;
using HarmonyLib;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Scaling
{
    /// <summary>
    /// Applies the one-shot scaling that is not a live multiplier: the creature's visual size and its maximum health.
    /// Both replace vanilla's level scaling rather than stacking on it: a creature publishes stars + 1
    /// to the native level, while health starts from the unlevelled base for the source star table. A creature that keeps its game level (this mod's stars off for its kind) is
    /// the exception: its health starts from the game's figure for that level, and its size from the game's own look.
    /// A boss's aspect multiplies the starred health (Twin), or replaces it outright
    /// on a Phantom copy. Movement and swing speed are live and handled by the controller and a patch.
    /// </summary>
    public static class StatApplier
    {
        public static void ApplySize(Character character, BiomeRules rules, CreatureTraits traits)
        {
            // Scale Saddle-able Creatures off: a creature that takes a saddle keeps its size at any star count (CreatureManager's rule).
            if (!ExtraPotions.Wildblood.Stars.SaddleScaling.Applies(
                    Plugin.Settings == null || Plugin.Settings.Get(ExtraPotions.Wildblood.Config.WildbloodSettings.Stars.ScaleSaddleable), ExtraPotions.Wildblood.Runtime.SaddleCreatures.IsSaddleable(character)))
            {
                return;
            }
            float size = StatMath.SizeMultiplier(rules, traits) * AspectMath.SizeFactor(traits);
            if (!Mathf.Approximately(size, 1f))
            {
                character.transform.localScale *= size;
            }
        }

        // OWNER ONLY: SetMaxHealth writes the ZDO's s_maxHealth, which the game replicates - so every other machine reads
        // the same scaled maximum (and a correct health-bar fraction) without this ever running there. The refill on a
        // fresh roll is likewise owner-only. A non-owner call would be dropped by the game, so callers gate. A creature
        // that keeps its game level and gets nothing from this mod's health keeps the maximum the game (or another mod)
        // gave it.
        public static void ApplyHealth(Character character, BiomeRules rules, CreatureTraits traits, bool freshlyResolved)
        {
            bool held = ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.SuspendReapingRuntime(character, health: true, scale: false);
            try
            {
                ZDO zdo = character.GetComponent<ZNetView>().GetZDO();
                EliteController? controller = character.GetComponent<EliteController>();
                if (TraitStore.KeepsNativeLevel(zdo) || controller != null && controller.HordeOnly)
                {
                    ExtraPotions.Wildblood.AdoptedCm.CreatureLevelManager.SynchronizePreservedNativeState(character, zdo);
                    controller?.BeginOwnedHealthApply();
                    float baseline = controller?.OwnedHealthBase ?? character.GetMaxHealth();
                    float fraction = Mathf.Clamp01(character.GetHealth() / Mathf.Max(1f, character.GetMaxHealth()));
                    float health = baseline * ExtraPotions.Wildblood.Adopted.Horde.HordeFactors.Health(character, zdo);
                    if (health != character.GetMaxHealth()) { character.SetMaxHealth(health); character.SetHealth(health * fraction); }
                    controller?.RememberOwnedHealth();
                    return;
                }
                controller?.BeginOwnedHealthApply();
                float devoured = zdo != null ? TraitStore.GetDevouredHealth(zdo) : 0f;
                float levelled = LevelledBase(character, traits);
                float max = traits.PhantomCopy
                    ? AspectMath.PhantomHealth()
                    : levelled * StatMath.HealthMultiplier(rules, traits) * AspectMath.HealthFactor(traits)
                        * (zdo != null ? TraitStore.GetDistanceHealth(zdo) : 1f)
                        * ExtraPotions.Wildblood.Adopted.Horde.HordeFactors.Health(character, zdo) + devoured;
                bool releaseHealth = freshlyResolved || zdo != null && PinnedAt(zdo, max);
                float fractionBefore = Mathf.Clamp01(character.GetHealth() / Mathf.Max(1f, character.GetMaxHealth()));
                if (!RuleState.Active.KeepsLevel(traits.Stars, character.IsBoss()) || !Mathf.Approximately(max, levelled))
                {
                    character.SetMaxHealth(max);
                    if (!releaseHealth) character.SetHealth(max * fractionBefore);
                }
                if (zdo != null && releaseHealth)
                {
                    zdo.RemoveFloat(ZDOVars.s_health);
                }
                controller?.RememberOwnedHealth();
            }
            finally
            {
                if (held) ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.ResumeReapingRuntime(character, health: true, scale: false);
            }
        }

        // The game's own maximum for a creature with none of this mod's stars: its base times its level, as the game sets
        // it up. Wildblood-owned stars publish stars + 1 but replace native growth, so use the base alone.
        private static float LevelledBase(Character character, CreatureTraits traits) =>
            character.GetMaxHealthBase() * ((TraitStore.KeepsNativeLevel(character.GetComponent<ZNetView>().GetZDO())
                || RuleState.Active.KeepsLevel(traits.Stars, character.IsBoss()))
                ? Mathf.Max(1, character.GetLevel()) : 1);

        // Full the game's way: no stored health, so health follows the maximum. Up to 3.7.0 a fresh roll wrote the
        // number instead, which pinned it, and a mod that raised the maximum afterwards left the creature part-hurt. A
        // creature still holding exactly that number is released when it next loads.
        private static bool PinnedAt(ZDO zdo, float max) =>
            Mathf.Approximately(zdo.GetFloat(ZDOVars.s_health, -1f), max);
    }

    /// <summary>Keep native level refreshes from replacing a resolved creature's source hp table.</summary>
    [HarmonyPatch(typeof(Character), "SetupMaxHealth")]
    public static class NativeLevelHealthPatch
    {
        internal struct HealthRefresh
        {
            internal bool Held;
            internal float? Fraction;
        }

        private static void Prefix(Character __instance, out HealthRefresh __state)
        {
            __state = default;
            EliteController? controller = __instance != null ? __instance.GetComponent<EliteController>() : null;
            bool active = controller != null && (controller.Ready || controller.HordeOnly && controller.LootReady)
                && controller.OwnsCurrentView();
            // Resume only this refresh's hold; snapshot only the exact health baseline this controller owns.
            __state.Held = active
                && ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.SuspendReapingRuntime(__instance, health: true, scale: false);
            if (active && controller!.ReapingHealthBaselineOwned)
                __state.Fraction = Mathf.Clamp01(__instance.GetHealth() / Mathf.Max(1f, __instance.GetMaxHealth()));
        }

        private static System.Exception? Finalizer(Character __instance, HealthRefresh __state, System.Exception? __exception)
        {
            if (__state.Held)
                ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.ResumeReapingRuntime(__instance, health: true, scale: false);
            return __exception;
        }

        private static void Postfix(Character __instance, HealthRefresh __state)
        {
            if (!(WildbloodModules.Active(WildbloodModule.Stars)
                || WildbloodModules.Active(WildbloodModule.Traits)
                || WildbloodModules.Active(WildbloodModule.Bosses) || WildbloodModules.Active(WildbloodModule.Horde))) return;
            EliteController? controller = __instance != null ? __instance.GetComponent<EliteController>() : null;
            if (controller == null || !(controller.Ready || controller.HordeOnly && controller.LootReady) || controller.View == null
                || !controller.View.IsValid() || !controller.View.IsOwner()) return;
            Guard.Run("Character.SetupMaxHealth native level", static c =>
            {
                StatApplier.ApplyHealth(c.Creature, c.Rules, c.Traits, freshlyResolved: false);
                if (c.KeepsNativeLevel) ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.ApplyStoredRuntimeState(c.Creature);
            }, controller);
            if (__state.Fraction.HasValue && controller.ReapingHealthBaselineOwned)
                __instance.SetHealth(__instance.GetMaxHealth() * __state.Fraction.Value);
        }
    }

}
