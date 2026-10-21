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
    /// Fixated: the boss marks one player, hits them harder and everyone else softer. Attached by AspectInstaller on every
    /// machine, never to a Phantom copy. On the boss's owner it decides where the mark goes (<see cref="FixatedOwner"/>),
    /// which self-gates on live ownership so the decision moves with the boss; on every client with a screen it draws the
    /// eye and says the line (<see cref="FixatedView"/>). The hit itself is scaled on the victim's owner
    /// (<see cref="FixatedDamage"/>). All three meet only in the mark in the boss's ZDO.
    /// </summary>
    public sealed class FixatedBehaviour : MonoBehaviour
    {
        private FixatedOwner? _owner;
        private FixatedView? _view;

        private void Start() => Guard.Run("FixatedBehaviour.Start", Setup);

        private void Setup()
        {
            EliteController controller = GetComponent<EliteController>();
            Character character = GetComponent<Character>();
            if (controller == null || character == null || controller.View == null)
            {
                enabled = false;
                return;
            }
            _owner = new FixatedOwner(controller, character);
            if (ZNet.instance != null && !ZNet.instance.IsDedicated())
            {
                _view = new FixatedView(character, controller.View);
            }
        }

        private void Update() => Guard.Run("FixatedBehaviour.Update", static self => self.Decide(), this);

        private void Decide() => _owner?.Tick();

        private void LateUpdate() => Guard.Run("FixatedBehaviour.LateUpdate", static self => self.Draw(), this);

        private void Draw() => _view?.Draw();

        private void OnDisable() => _view?.Dispose();

        private void OnDestroy() => _view?.Dispose();
    }
}
