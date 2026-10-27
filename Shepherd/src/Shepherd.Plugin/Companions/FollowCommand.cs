// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
//
// Follow Command (design D6, approved 2026-10-07): reimplemented from the documented behavior of Cartur's Follow
// Command (https://github.com/jekkle/cartur-follow-command, one Tameable.Awake postfix that sets m_commandable). That
// repository had no license file on 2026-10-07, so no code was copied. Decisions in the portable FollowRules.
using System;
using System.Runtime.CompilerServices;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Husbandry;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using HarmonyLib;

namespace ExtraPotions.Shepherd.Runtime.Companions
{
    /// <summary>
    /// Native follow/stay for supported tameables: while Shepherd and its Husbandry module run, a Tameable.Awake postfix sets
    /// the game's own <c>m_commandable</c> on tameables the game left uncommandable (boar, lox, hen, ...), so Use on a
    /// tame toggles the game's follow/stay command. The game keeps the command RPC, ownership checks, patrol behavior and
    /// the saved follow target; no command transport, species list or UI is added. Summons are left alone. When Shepherd
    /// stops, only the flags it flipped are put back. Petting contentment keys on the game's own flag
    /// (<see cref="NativelyCommandable"/>), so a following boar can still be petted content. Pack leader then applies to
    /// every follower. Tameables without the native MonsterAI command handler are left alone.
    /// </summary>
    public static class FollowCommand
    {
        public const string HarmonyId = Plugin.Guid + ".follow";
        private sealed class Flipped { }
        private static readonly ConditionalWeakTable<Tameable, Flipped> flipped = new ConditionalWeakTable<Tameable, Flipped>();
        private static Harmony? harmony;
        private static bool installed;
        // A failed install waits for the module to be switched off and on (or a restart) instead of re-patching every frame.
        private static bool installFailed;

        private static bool Wanted => harmony != null && Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out _)
            && ShepherdSettings.Enabled.Value && HusbandrySettings.Enabled.Value;

        /// <summary>The game's own commandable flag, before Shepherd changed it.</summary>
        public static bool NativelyCommandable(Tameable tameable) =>
            tameable != null && tameable.m_commandable && !flipped.TryGetValue(tameable, out _);

        public static void Initialize()
        {
            Shutdown();
            harmony = new Harmony(HarmonyId);
        }

        public static void Tick()
        {
            if (harmony == null) return;
            bool wanted = Wanted;
            if (!wanted) installFailed = false;
            if (wanted == installed || (wanted && installFailed)) return;
            if (wanted)
            {
                try { harmony.CreateClassProcessor(typeof(Awake)).Patch(); }
                catch { harmony.UnpatchSelf(); installFailed = true; throw; }
                installed = true;
            }
            else
            {
                harmony.UnpatchSelf();
                installed = false;
            }
            foreach (Tameable tameable in UnityEngine.Object.FindObjectsOfType<Tameable>()) Apply(tameable);
        }

        public static void Shutdown()
        {
            harmony?.UnpatchSelf();
            harmony = null;
            installFailed = false;
            if (installed)
            {
                installed = false;
                foreach (Tameable tameable in UnityEngine.Object.FindObjectsOfType<Tameable>()) Apply(tameable);
            }
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Awake))]
        private static class Awake
        {
            [HarmonyPostfix]
            private static void Postfix(Tameable __instance) => NativeHusbandry.Run("follow command", Apply, __instance);
        }

        private static void Apply(Tameable tameable)
        {
            if (tameable == null) return;
            bool ours = flipped.TryGetValue(tameable, out _);
            if (FollowRules.Enables(installed, tameable.m_commandable, PackLeader.IsSummon(tameable), tameable.m_monsterAI != null))
            {
                tameable.m_commandable = true;
                flipped.Add(tameable, new Flipped());
            }
            else if (FollowRules.Restores(ours, installed))
            {
                tameable.m_commandable = false;
                flipped.Remove(tameable);
            }
        }
    }
}
