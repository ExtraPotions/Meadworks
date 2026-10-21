// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Portalbound: the boss throws through portals. As it winds up its throw (the Elder's vines, Bonemass's slime) a
    /// portal opens somewhere near its target - at least `min height` above the ground, `clearance` from anything,
    /// within `range` of the target and in sight of it - and as it lets go a second portal opens on its throwing hand;
    /// the throw comes out of the far portal at the target instead of from the hand. Only the Elder and Bonemass have
    /// an attack the portals carry (<see cref="PortalAttacks"/>), so only they roll it. Attached by AspectInstaller on
    /// every machine, never to a Phantom copy. On the boss's owner - the only machine that runs its attacks - it decides
    /// where the far portal opens and sends the throw from it (<see cref="PortalOwner"/>, reached through
    /// <see cref="Patches.PortalStartPatch"/> and <see cref="Patches.PortalSpawnPointPatch"/>), self-gating on live
    /// ownership; on every client with a screen it draws both portals (<see cref="PortalView"/>). The two meet only in
    /// what the owner writes into the boss's ZDO (<see cref="PortalStore"/>).
    /// </summary>
    public sealed class PortalboundBehaviour : MonoBehaviour
    {
        private PortalOwner? _owner;
        private PortalView? _view;

        private void Start() => Guard.Run("PortalboundBehaviour.Start", Setup);

        private void Setup()
        {
            EliteController controller = GetComponent<EliteController>();
            Humanoid boss = GetComponent<Humanoid>();
            string prefab = Utils.GetPrefabName(gameObject);
            if (controller == null || boss == null || controller.View == null || !controller.View.IsValid()
                || !controller.Traits.HasAspect(Aspect.Portalbound) || !PortalAttacks.Supports(prefab))
            {
                enabled = false; // a boss with no attack the portals carry throws as the game made it
                return;
            }
            _owner = new PortalOwner(controller, boss, prefab);
            if (!StormEffects.Headless())
            {
                _view = new PortalView(controller.View, boss, prefab);
            }
        }

        /// <summary>From the attack-start patch: the boss has just started an attack.</summary>
        public void Started(Attack attack) => _owner?.Started(attack);

        /// <summary>From the spawn-point patch: the game is placing one of this boss's projectiles.</summary>
        public void Redirect(Attack attack, ref Vector3 spawnPoint, ref Vector3 aimDir)
        {
            if (_owner != null)
            {
                _owner.Redirect(attack, ref spawnPoint, ref aimDir);
            }
        }

        private void Update() => Guard.Run("PortalboundBehaviour.Update", static self => self.Step(), this);

        private void Step()
        {
            _owner?.Tick();
            _view?.Tick();
        }

        private void OnDisable() => _view?.Dispose();

        private void OnDestroy() => _view?.Dispose();
    }
}
