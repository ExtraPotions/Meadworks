// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Yield/Butchering.cs and ButcherContext.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: 9da28eb58adbe065be1e4baef779a448acd38e0f (GrindstoneSkills 0.16.0)
// Changed by expDARE, 2026: namespace; NativeHusbandry exception guard.
// SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// A tamed animal dying, on its owner: read once when Character.OnDeath starts, before the game generates the drop
    /// list and destroys the creature.
    /// </summary>
    public sealed class ButcherContext
    {
        public Character Creature { get; set; } = null!;

        /// <summary>The creature's prefab name, for example "Boar".</summary>
        public string Prefab { get; set; } = "";

        /// <summary>The player whose hit killed it (the game's last hit on the owner); null for any other death.</summary>
        public Player? Killer { get; set; }

        /// <summary>The killer's Husbandry level (from the killer's ZDO when the owner is another machine); 0 without a killer.</summary>
        public float KillerLevel { get; set; }
    }

    /// <summary>
    /// A tamed animal dying, on the creature's ZDO owner. Character.OnDeath does the whole death there: it sets up the
    /// ragdoll, whose Setup generates the drop list and stores it on the ragdoll's ZDO, then calls the death callbacks,
    /// where CharacterDrop.OnDeath generates and spawns the drops at once for a creature without a dropping ragdoll. A
    /// prefix opens a <see cref="ButcherContext"/> for the whole call while Husbandry is on, so both drop paths see it; a
    /// finalizer puts back the one before (none). The killer is the attacker of the game's last hit. A player killer gets
    /// the butchering experience by <see cref="HusbandryCredit"/>, since only the killer's client can raise the killer's
    /// skill. Non-owners also run the start of OnDeath; they never open a context.
    /// </summary>
    public static class Butchering
    {
        /// <summary>The death being handled right now; null outside Character.OnDeath of a tamed animal on its owner.</summary>
        public static ButcherContext? Open { get; private set; }

        [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
        private static class Death
        {
            [HarmonyPrefix]
            private static void Prefix(Character __instance, out ButcherContext? __state)
            {
                __state = Open;
                Open = HusbandrySkill.Active ? NativeHusbandry.Run("butchering", static character => Begin(character), __instance, (ButcherContext?)null) : null;
            }

            [HarmonyFinalizer]
            private static void Finalizer(ButcherContext? __state) => Open = __state;
        }

        private static ButcherContext? Begin(Character creature)
        {
            if (!IsOwnedTame(creature))
                return null;
            Player? killer = creature.m_lastHit != null ? creature.m_lastHit.GetAttacker() as Player : null;
            var context = new ButcherContext
            {
                Creature = creature,
                Prefab = Herd.PrefabName(creature),
                Killer = killer,
                KillerLevel = killer != null ? HusbandrySkill.Of(killer) : 0f,
            };
            if (killer != null)
                HusbandryCredit.Send(killer.GetPlayerID(), HusbandryCredit.Kind.Butchering, context.Prefab);
            return context;
        }

        private static bool IsOwnedTame(Character creature) =>
            creature != null && !creature.IsPlayer() && creature.m_nview != null && creature.m_nview.IsValid()
            && creature.m_nview.IsOwner() && creature.IsTamed();
    }
}
