// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Stormbound: every `every` seconds of a fight a glowing circle appears under each player within `range`, and
    /// `tell time` seconds later lightning strikes it - a little damage and a stagger for anyone still inside. Attached
    /// by AspectInstaller on every machine, never to a Phantom copy. On the boss's owner it decides when and where
    /// (<see cref="StormOwner"/>), self-gating on live ownership so the decision moves with the boss; on every client
    /// with a screen it draws the circles, strikes them and judges its own player (<see cref="StormView"/>). The two
    /// meet only in the storm the owner writes into the boss's ZDO (<see cref="StormStore"/>).
    /// </summary>
    public sealed class StormboundBehaviour : MonoBehaviour
    {
        private StormOwner? _owner;
        private StormView? _view;

        private void Start() => Guard.Run("StormboundBehaviour.Start", Setup);

        private void Setup()
        {
            EliteController controller = GetComponent<EliteController>();
            Character boss = GetComponent<Character>();
            if (controller == null || boss == null || controller.View == null || !controller.View.IsValid())
            {
                enabled = false;
                return;
            }
            _owner = new StormOwner(controller, boss, boss.GetBaseAI());
            if (!StormEffects.Headless())
            {
                _view = new StormView(controller.View, boss);
            }
        }

        private void Update() => Guard.Run("StormboundBehaviour.Update", static self => self.Step(), this);

        private void Step()
        {
            _owner?.Tick();
            _view?.Tick();
        }

        private void OnDisable() => _view?.Dispose();

        private void OnDestroy() => _view?.Dispose();
    }
}
