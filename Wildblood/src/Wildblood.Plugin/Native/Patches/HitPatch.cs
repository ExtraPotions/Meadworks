// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Mutations;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The one patch on <c>Character.RPC_Damage</c> - the game's hit, run on the struck character's owner - for every
    /// feature of this mod that acts on a hit at the normal priority, asked in turn in the order they ran when each was a
    /// patch of its own. Before the hit: the Brutal mark (<see cref="BrutalHitPatch"/>), Warding's capture
    /// (<see cref="DamageReactionPatch"/>), the scaling (<see cref="DamageScalingPatch"/>), the devour bite
    /// (<see cref="DevourHitPatch"/>), the death recap's source (<see cref="RecapSourcePatch"/>), the Screecher's health
    /// (<see cref="ShriekDamagePatch"/>), the Thieving dodge test (<see cref="ThievingHitPatch"/>) and Piercing's armour cut
    /// (<see cref="PiercingArmour"/>). After it: the armour cut closed, the Brutal throw, the reactions (Warding, Leeching,
    /// Reflective), the forced devour, the shriek, the steal and Binding's root (<see cref="BindingRoot"/>). The
    /// parts they all ask for are looked up once a side (<see cref="Struck"/>), the attacker again after the reactions,
    /// which can kill it. Each step keeps its own guard: the ones that never throw still swallow and report, the rest
    /// still report and rethrow, which stops the steps after them exactly as a throwing patch stopped the patches after
    /// it. Frostbound's heal (<see cref="FrostHealPatch"/>, first before and last after) and the Cloning decoy's blow
    /// (<see cref="CloneHitPatch"/>, last before) keep their own patches, because their place is set by priority.
    /// </summary>
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    public static class HitPatch
    {
        /// <summary>What the steps before the hit hand to the steps after it.</summary>
        public struct Before
        {
            public bool Brutal;
            public WardingReflect.Before Warding;

            /// <summary>A deciding Screecher's health before the hit; -1 for anything else.</summary>
            public float ScreecherHealth;

            public bool Dodged;
        }

        private static void Prefix(Character __instance, HitData hit, out Before __state)
        {
            __state = default;
            __state.ScreecherHealth = -1f;
            __state.Brutal = BrutalHitPatch.Marked(__instance, hit);
            Struck struck = Struck.Of(__instance, hit);
            __state.Warding = DamageReactionPatch.Capture(struck);
            Guard.Run("Character.RPC_Damage scaling", static s => DamageScalingPatch.Scale(s), struck);
            Guard.Run("Character.RPC_Damage devour commit", static s => DevourHitPatch.Commit(s), struck);
            RecapSourcePatch.Note(__instance, hit);
            __state.ScreecherHealth = ShriekDamagePatch.Capture(struck);
            __state.Dodged = Guard.Run("Character.RPC_Damage thieving dodge", static s => ThievingHitPatch.Dodged(s), struck);
            Guard.Run("Character.RPC_Damage piercing", static s => PiercingArmour.Begin(s), struck);
        }

        private static void Postfix(Character __instance, HitData hit, Before __state)
        {
            PiercingArmour.End();
            BrutalHitPatch.Throw(__instance, hit, __state.Brutal);
            Struck struck = Struck.Of(__instance, hit);
            Guard.Run("Character.RPC_Damage reaction", static (s, warding) => DamageReactionPatch.React(s, warding), struck,
                __state.Warding);
            struck = struck.Again();
            Guard.Run("Character.RPC_Damage devour finish", static s => DevourHitPatch.Finish(s), struck);
            ShriekDamagePatch.Hurt(__instance, __state.ScreecherHealth);
            Guard.Run("Character.RPC_Damage thieving", static (s, dodged) => ThievingHitPatch.TrySteal(s, dodged), struck,
                __state.Dodged);
            Guard.Run("Character.RPC_Damage binding", static (s, dodged) => BindingRoot.TryRoot(s, dodged), struck,
                __state.Dodged);
        }
    }
}
