// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Flame visuals adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>
    /// Attaches the per-frame mutation behaviours a creature's traits call for, on EVERY machine. The old build split
    /// these owner-only vs everywhere; the multiplayer rule is instead "decide on the owner, draw on every client", so
    /// each behaviour is attached everywhere and self-gates its authoritative writes on live ownership. That makes
    /// ownership changing hands free: the machine that owns the creature at any moment is the one whose gates open.
    /// The event-shaped mutations (Warding, Plated, Bloated, Splintering, Mad, Juggernaut, Corrodent, Piercing, Binding) need
    /// no component - patches handle them.
    /// </summary>
    public static class BehaviourInstaller
    {
        public static void Install(EliteController controller)
        {
            CreatureTraits traits = controller.Traits;
            if (traits.Has(Mutation.Cloaked))
            {
                controller.InstallBehaviour<CloakBehaviour>();
            }
            if (traits.Has(Mutation.Leeching))
            {
                controller.InstallBehaviour<LeechBehaviour>();
            }
            if (traits.Has(Mutation.Miasmic))
            {
                controller.InstallBehaviour<MiasmaField>();
                controller.InstallBehaviour<MiasmaBody>(); // permanent worn poison visual, every machine
            }
            if (traits.Has(Mutation.Devouring))
            {
                controller.InstallBehaviour<DevourBehaviour>();
            }
            InstallLater(controller, traits);
        }

        private static void InstallLater(EliteController controller, CreatureTraits traits)
        {
            if (traits.Has(Mutation.Gilded))
            {
                controller.InstallBehaviour<GildedBehaviour>();
            }
            if (traits.Has(Mutation.Blinking))
            {
                controller.InstallBehaviour<BlinkBehaviour>();
            }
            if (traits.Has(Mutation.Relentless))
            {
                controller.InstallBehaviour<RelentlessBehaviour>();
            }
            if (traits.Has(Mutation.Screecher))
            {
                controller.InstallBehaviour<ScreecherBehaviour>();
            }
            if (traits.Has(Mutation.Howling))
            {
                controller.InstallBehaviour<HowlBehaviour>();
            }
            InstallWinter(controller, traits);
        }

        private static void InstallWinter(EliteController controller, CreatureTraits traits)
        {
            if (traits.Has(Mutation.Frostbound))
            {
                controller.InstallBehaviour<FrostAura>();
                controller.InstallBehaviour<FrostTrail>();
            }
            if (traits.Has(Mutation.Mudbound))
            {
                controller.InstallBehaviour<MudTrail>();
            }
            if (traits.Has(Mutation.Flamebound))
            {
                controller.InstallBehaviour<FlameTrail>();
                controller.InstallBehaviour<FlameAuraLook>();
            }
            InstallCloning(controller, traits);
        }

        // A decoy Has no mutation, so this is all it gets: its own leaving, and the frost look its creature wore before
        // the swap, so the body the player watches does not lose its mist and light in that frame.
        private static void InstallCloning(EliteController controller, CreatureTraits traits)
        {
            if (traits.Has(Mutation.Cloning))
            {
                controller.InstallBehaviour<CloneBehaviour>();
            }
            else if (traits.Decoy)
            {
                controller.InstallBehaviour<CloneDecoy>();
                if (traits.Wears(Mutation.Frostbound))
                {
                    controller.InstallBehaviour<FrostAuraLook>();
                }
                if (traits.Wears(Mutation.Flamebound))
                {
                    controller.InstallBehaviour<FlameAuraLook>();
                }
            }
        }
    }
}
