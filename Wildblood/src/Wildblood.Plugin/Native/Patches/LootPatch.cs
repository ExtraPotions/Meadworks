// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Horde adoption adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Adopted.Loot;
using ExtraPotions.Wildblood.Adopted.Runtime;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Hands a kill's freshly built drop list to the loot engine. For this call only, the native level field is held at 1,
    /// so the game applies no level loot bonus of its own, so the engine's result is the whole story - except for a
    /// creature that keeps its game level (this mod's stars off), whose level bonus the game has already paid. Runs on the dying
    /// creature's owner (where the game builds the drop list); reads its traits, replicated from the owner's roll, so
    /// the outcome is the same wherever the creature was rolled. A creature the mod has not resolved is left vanilla.
    /// Runs after every other mod's postfix, so a boss's trophy count is ours: Epic Loot's own boss trophy mode
    /// overwrote it with the player count when it ran after us.
    /// </summary>
    [HarmonyPatch(typeof(CharacterDrop), "GenerateDropList")]
    public static class LootPatch
    {
        // The temporary field never reaches the ZDO, SetLevel or health setup. A per-call state makes
        // nested generation restore its caller's value, and the finalizer also covers a thrown original.
        private static readonly Dictionary<Character, LevelHold> Holds = new Dictionary<Character, LevelHold>();

        public sealed class LevelHold
        {
            public Character Character = null!;
            public int Level;
            public int NativeLevel;
            public LevelHold? Previous;
            public bool Held;

            public void Restore()
            {
                if (!Held) return;
                Held = false;
                if (Character != null) Character.m_level = Level;
                if (!ReferenceEquals(Character, null)
                    && Holds.TryGetValue(Character, out LevelHold current) && ReferenceEquals(current, this))
                {
                    if (Previous == null) Holds.Remove(Character);
                    else Holds[Character] = Previous;
                }
            }
        }

        [HarmonyPriority(Priority.First)]
        private static void Prefix(CharacterDrop __instance, out LevelHold? __state)
        {
            __state = null;
            if (!WildbloodModules.Active(WildbloodModule.Loot)) return;
            Character character = __instance != null ? __instance.m_character : null!;
            EliteController? controller = character != null ? character.GetComponent<EliteController>() : null;
            if (controller == null || !controller.LootReady || controller.View == null
                || !controller.View.IsValid() || !controller.View.IsOwner()) return;
            Holds.TryGetValue(character, out LevelHold? prior);
            int nativeLevel = prior?.NativeLevel ?? character.m_level;
            if (controller.LootOnly)
            {
                // Snapshot before borrowing the field; a nested call inherits the outer supplied native level.
                controller.RefreshLootIdentity(nativeLevel);
                if (RuleState.Active.Loot.Mode == LootMode.Vanilla) return;
            }
            else if (controller.KeepsNativeLevel || RuleState.Active.KeepsLevel(controller.Traits.Stars, character.IsBoss())) return;
            __state = new LevelHold { Character = character, Level = character.m_level,
                NativeLevel = nativeLevel, Previous = prior, Held = true };
            Holds[character] = __state;
            character.m_level = 1;
        }

        private static Exception? Finalizer(Exception? __exception, LevelHold? __state)
        {
            __state?.Restore();
            return __exception;
        }

        // Restore before normal-priority consumers (including Inscribe) inspect the kill's level.
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        private static void RestoreLevel(LevelHold? __state) => __state?.Restore();

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result, LevelHold? __state)
        {
            __state?.Restore();
            Guard.Run("CharacterDrop.GenerateDropList", static (drop, result) => Rework(drop, result), __instance, __result);
        }

        private static void Rework(CharacterDrop drop, List<KeyValuePair<GameObject, int>> result)
        {
            if (drop == null || result == null) return;
            if (!WildbloodModules.Active(WildbloodModule.Loot)) { ExtraPotions.Wildblood.Adopted.Horde.HordeLoot.Apply(drop, result); return; }
            Character character = drop.m_character;
            EliteController? controller = character != null ? character.GetComponent<EliteController>() : null;
            if (controller == null || !controller.LootReady || result == null || controller.View == null
                || !controller.View.IsValid() || !controller.View.IsOwner())
            {
                ExtraPotions.Wildblood.Adopted.Horde.HordeLoot.Apply(drop, result);
                return;
            }
            if (!TetherLoot.Withhold(drop, controller, result)) // the first of a Tethered pair to fall drops nothing
            {
                LootEngine.Rework(drop, controller, result);
            }
            ExtraPotions.Wildblood.Adopted.Horde.HordeLoot.Apply(drop, result);
        }
    }
}
