// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Read-only projection of the qualified ECR controller and TetherPair state for Inscribe's existing death-facts contract.
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Wildblood.Adopted.Aspects;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Runtime;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using WildbloodPlugin = ExtraPotions.Wildblood.Runtime.Plugin;

namespace ExtraPotions.Wildblood.Adopted.Adapters
{
    internal static class NativeLootCapability
    {
        internal const string Id = "meadworks.loot.drops";
        internal static void Register(ProductRegistration registration)
        {
            registration.AddCapability<Func<object, Tuple<int, bool, bool, bool, int>?>>(
                Id, new System.Version(1, 0), Read);
            registration.AddCapability<Func<float>>("meadworks.loot.creature_multiplier", new System.Version(1, 0), CreatureMultiplier);
        }

        // Magpie's Q2 overlap query is separate from the per-creature death-facts delegate.
        internal static float CreatureMultiplier()
        {
            if (!CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady || CruciblePlugin.Session.WorldId == 0
                || ZNet.instance == null || ZNet.instance.GetWorldUID() != CruciblePlugin.Session.WorldId
                || !WildbloodModules.Active(WildbloodModule.Loot) || WildbloodPlugin.Settings == null)
                return 1f;
            return WildbloodPlugin.Settings.Multipliers.Creature;
        }

        // Tuple order matches Inscribe: stars, elite, boss, worthless, creature's saved roll tier.
        internal static Tuple<int, bool, bool, bool, int>? Read(object target)
        {
            if (!(target is Character creature) || creature == null || creature.IsPlayer()
                || !WildbloodPlugin.Ready || !WildbloodModules.ProductActive(out _)
                || !CruciblePlugin.Session.AuthorityReady || CruciblePlugin.Session.WorldId == 0
                || ZNet.instance == null || ZNet.instance.GetWorldUID() != CruciblePlugin.Session.WorldId)
                return null;
            var view = creature.m_nview;
            if (view == null || !view.IsValid()) return null;
            var controller = creature.GetComponent<EliteController>();
            if (controller == null || !controller.LootReady || controller.Creature != creature
                || controller.View != view) return null;
            var zdo = view.GetZDO();
            if (zdo == null || !zdo.IsValid()) return null;
            var traits = controller.Traits;
            // Use the persisted native level, not GenerateDropList's temporary level hold.
            int stars = Math.Max(1, zdo.GetInt(ZDOVars.s_level, 1)) - 1;
            bool worthless = traits.PhantomCopy || traits.Decoy
                || traits.HasAspect(Aspect.Tethered) && TetherPair.Standing(TraitKeys.Id(zdo, TraitKeys.TetherPartner));
            return Tuple.Create(stars, traits.Any, creature.IsBoss(), worthless, Math.Max(0, traits.Tier));
        }
    }
}
