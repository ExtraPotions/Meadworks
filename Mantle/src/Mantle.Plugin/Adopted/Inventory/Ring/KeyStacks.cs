using System;
using System.Collections.Generic;
using HarmonyLib;
using ExtraPotions.Mantle.Embedded.ItemCopies;
using ExtraPotions.Mantle.Embedded.PatchGuard;
using Mantle.Core;

namespace Mantle.Ring
{
    /// <summary>
    /// Key Stack: every key in Key Items stacks to at least this many. One mod writes the keys' stack size. With OpenKeep
    /// 1.8.0 or later installed, its Stacks module writes every item's stack size and reads Key Items and Key Stack from
    /// this mod's config for the keys (<see cref="OpenKeepLink"/>), so an OpenKeep.Stacks.yml entry for a key still wins.
    /// Without it, this class writes them: at least Key Stack over the stack size the key had when first seen (the
    /// game's), into the prefab and every live copy (the ItemCopies library), when the item database is ready and when
    /// Key Stack or Key Items change. A prefab dropped from Key Items gets its first value back. It never lowers a stack
    /// below that first value, so a non-key listed in Key Items keeps its own, and it applies whatever the ring's and the
    /// master switch say: switching those off must not cut a stack of keys the next time a chest or the character loads.
    /// Mantle, 2026-10-10: Burrow's Stacks feature (Burrow 0.3.0 and later) writes the keys the same way from this mod's
    /// Key Items and Key Stack, so with it present this class steps aside too (<see cref="Mantle.Interop.BurrowStacksLink"/>).
    /// </summary>
    public static class KeyStacks
    {
        /// <summary>Each prefab's stack size before this class first raised it, by prefab name.</summary>
        private static readonly Dictionary<string, int> firstSeen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public static int Over(string prefab, int stack) =>
            KeyRing.NumberOf(prefab) > 0 ? Math.Max(stack, KeyRingSettings.KeyStack.Value) : stack;

        private static EventHandler stackHandler;
        private static EventHandler itemsHandler;

        public static void Watch()
        {
            if (stackHandler != null)
                return;
            stackHandler = Guard.Wrap("key stack", (_, _) => ApplyAll());
            itemsHandler = Guard.Wrap("key items", (_, _) => ApplyAll());
            KeyRingSettings.KeyStack.SettingChanged += stackHandler;
            KeyRingSettings.KeyItems.SettingChanged += itemsHandler;
        }

        /// <summary>Releases what <see cref="Watch"/> subscribed (module shutdown).</summary>
        public static void Unwatch()
        {
            if (stackHandler == null)
                return;
            KeyRingSettings.KeyStack.SettingChanged -= stackHandler;
            KeyRingSettings.KeyItems.SettingChanged -= itemsHandler;
            stackHandler = itemsHandler = null;
        }

        public static void ApplyAll()
        {
            if (OpenKeepLink.Present || Mantle.Interop.BurrowStacksLink.Present || ObjectDB.instance == null)
                return;
            Copies.ApplyAll(Write);
        }

        private static void Write(string prefab, ItemDrop.ItemData.SharedData shared)
        {
            if (!firstSeen.TryGetValue(prefab, out int first))
            {
                if (KeyRing.NumberOf(prefab) == 0)
                    return;
                first = shared.m_maxStackSize;
                firstSeen[prefab] = first;
            }
            shared.m_maxStackSize = Over(prefab, first);
        }

        /// <summary>The item database is there: ObjectDB.Awake in the game scene, ObjectDB.CopyOtherDB at the main menu.</summary>
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        private static class AwakePatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Low)]
            private static void Postfix() => ApplyAll();
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static class CopyPatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Low)]
            private static void Postfix() => ApplyAll();
        }
    }
}
