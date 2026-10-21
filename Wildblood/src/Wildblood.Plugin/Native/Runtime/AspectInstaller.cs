// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Aspects;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>
    /// A boss's counterpart to <see cref="BehaviourInstaller"/>: attaches the per-frame behaviour each of its aspects calls for on
    /// EVERY machine, each self-gating its writes on live ownership (Phantom's splits among them), and - on the owner, on
    /// the boss's first roll only - brings in its twin or tethered partner. A copy is born already resolved, so it never
    /// reaches that branch, and never brings a twin, a partner or splits of its own. A Phantom copy is hollowed here on every machine the
    /// moment it resolves, then takes the behaviours of the other aspects it carries (a Bountiful boss's, see
    /// <see cref="Aspects.PhantomSpawner"/>).
    /// The hit-shaped aspects (Reflective, Shielded, Elementalist, Enraged) need no component - patches handle them.
    /// Adaptive, Fixated and Tethered have both: a component for their state and look, and a hook in
    /// <see cref="Scaling.AspectDamage"/>.
    /// </summary>
    public static class AspectInstaller
    {
        public static void Install(EliteController controller)
        {
            CreatureTraits traits = controller.Traits;
            if (traits.PhantomCopy)
            {
                PhantomBody.Hollow(controller.Creature);
            }
            foreach (Aspect aspect in traits.Aspects())
            {
                if (traits.PhantomCopy && (aspect == Aspect.Phantom || aspect == Aspect.Echoing))
                {
                    continue; // a copy wears a Bountiful boss's other aspects, never Phantom's splits or an echo of its own
                }
                Attach(controller, aspect);
                if (controller.FreshlyResolved && controller.IsOwner())
                {
                    Arrive(controller, aspect);
                }
            }
        }

        private static void Attach(EliteController controller, Aspect aspect)
        {
            switch (aspect)
            {
                case Aspect.Mending: controller.InstallBehaviour<MendingBehaviour>(); break;
                case Aspect.Summoner: controller.InstallBehaviour<SummonerBehaviour>(); break;
                case Aspect.Twin: controller.InstallBehaviour<TwinLink>(); break;
                case Aspect.Phantom: AttachPhantom(controller); break;
                case Aspect.Adaptive: controller.InstallBehaviour<AdaptiveBehaviour>(); break;
                case Aspect.Fixated: controller.InstallBehaviour<FixatedBehaviour>(); break;
                case Aspect.Stormbound: controller.InstallBehaviour<StormboundBehaviour>(); break;
                case Aspect.Gravitic: controller.InstallBehaviour<GraviticBehaviour>(); break;
                case Aspect.Colossal: controller.InstallBehaviour<ColossalBehaviour>(); break;
                case Aspect.Tethered: controller.InstallBehaviour<TetherLink>(); break;
                case Aspect.Portalbound: controller.InstallBehaviour<PortalboundBehaviour>(); break;
                case Aspect.Nightfall: AttachNightfall(controller); break;
                case Aspect.Brutal: controller.InstallBehaviour<BrutalBehaviour>(); break;
                case Aspect.Echoing: controller.InstallBehaviour<EchoBehaviour>(); break;
            }
        }

        private static void AttachNightfall(EliteController boss)
        {
            boss.InstallBehaviour<NightfallSky>();
            boss.InstallBehaviour<NightfallStorm>();
        }

        private static void AttachPhantom(EliteController boss)
        {
            boss.InstallBehaviour<PhantomBehaviour>();
            boss.InstallBehaviour<PhantomShuffle>();
        }

        private static void Arrive(EliteController controller, Aspect aspect)
        {
            if (aspect == Aspect.Twin)
            {
                TwinSpawner.Spawn(controller);
            }
            else if (aspect == Aspect.Tethered)
            {
                TetherSpawner.Spawn(controller);
            }
        }
    }
}
