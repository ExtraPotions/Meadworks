// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Companions/Petting.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; Crucible boundaries; owned native lifecycle; contentment keys on the game's own
// commandable flag (Follow Command makes more tames commandable).
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Shepherd.Husbandry;
using ExtraPotions.Shepherd.Runtime.Companions;
using HarmonyLib;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Petting and contentment. Using a tamed animal the game does not let you command (boar, lox, hen and the like; a
    /// wolf gets commands instead) pets it, at most once a second: <c>Tameable.Interact</c> stamps m_lastPetTime.
    /// <list type="bullet">
    /// <item>On the petting player's client, a prefix and postfix see the stamp change on a non-commandable tamed animal.
    /// While Content Duration is above 0, a pet on an animal that is not content yet earns the petter Petting Experience
    /// times its tier, and every pet sends <see cref="Keys.RpcPet"/> to the animal's owner.</item>
    /// <item>The owner writes <see cref="Keys.ContentUntil"/> (world time now + Content Duration, in ticks) to the ZDO,
    /// which replicates; breeding and the lore hover read it (<see cref="NativeHusbandry.IsContent"/>). Petting a content animal
    /// again starts its time over but earns nothing, so petting is no experience farm.</item>
    /// </list>
    /// Content Duration 0 turns contentment and the petting experience off; nothing happens while Husbandry is off. The
    /// RPC is registered on a tameable's view when the first pet arrives for it (<see cref="LazyRpcs"/>), not on every
    /// one that wakes.
    /// </summary>
    public static class Petting
    {
        /// <summary>Registers the pet RPC on a tameable's view, when the first pet arrives for it (<see cref="LazyRpcs"/>).</summary>
        public static void RegisterOn(ZNetView nview)
        {
            Tameable tameable = nview.GetComponent<Tameable>();
            if (tameable != null)
                NativeHusbandry.RegisterPet(nview, sender => Receive(tameable, sender));
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
        private static class Pet
        {
            [HarmonyPrefix]
            private static void Prefix(Tameable __instance, out float __state) => __state = __instance.m_lastPetTime;

            [HarmonyPostfix]
            private static void Postfix(Tameable __instance, Humanoid user, float __state)
            {
                if (__instance.m_lastPetTime != __state)
                    NativeHusbandry.Run("petting", () => Petted(__instance, user as Player));
            }
        }

        private static void Petted(Tameable tameable, Player player)
        {
            if (player == null || player != Player.m_localPlayer || !CanBeContent(tameable))
                return;
            if (!NativeHusbandry.IsContent(tameable.m_nview))
                HusbandryXp.RaiseForCreature(player, HusbandryExperienceSettings.Petting.Value, Herd.PrefabName(tameable));
            tameable.m_nview.InvokeRPC(Keys.RpcPet);
        }

        /// <summary>Husbandry on, contentment on, and a valid tamed animal that is not commandable.</summary>
        private static bool CanBeContent(Tameable tameable) =>
            HusbandrySkill.Active && HusbandryBreedingSettings.ContentDuration.Value > 0f
            && tameable != null && FollowRules.PetsForContent(FollowCommand.NativelyCommandable(tameable))
            && tameable.m_character != null && tameable.IsTamed()
            && tameable.m_nview != null && tameable.m_nview.IsValid();

        private static void Receive(Tameable tameable, long sender) => NativeHusbandry.Run(Keys.RpcPet, () => MakeContent(tameable, sender));

        /// <summary>On the animal's owner: content from now for Content Duration.</summary>
        private static void MakeContent(Tameable tameable, long sender)
        {
            if (!CanBeContent(tameable) || !tameable.m_nview.IsOwner() || !NativeHusbandry.ValidPetter(tameable, sender))
                return;
            long until = Herd.TicksIn(HusbandryBreedingSettings.ContentDuration.Value);
            tameable.m_nview.GetZDO().Set(Keys.ContentUntil, until / System.TimeSpan.TicksPerMillisecond);
        }
    }
}
