// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Mending: heals a share of its current health every second (ECR 3.22: 0.2% of current, was 0.3% of maximum), in
    /// combat and out - unlike Leeching's regeneration it never pauses when hit, because the aspect is a damage check, not a top-up. The game's own slow regeneration still runs
    /// underneath. Attached on every machine, heals only while it owns the boss (a heal is an owner-only write), so the
    /// healing moves with ownership. The rate is read live, so a rule-file edit retunes a fight in progress.
    /// </summary>
    public sealed class MendingBehaviour : MonoBehaviour
    {
        private Character _character = null!;
        private EliteController _controller = null!;
        private float _timer;

        private void Start()
        {
            _character = GetComponent<Character>();
            _controller = GetComponent<EliteController>();
        }

        private void Update() => Guard.Run("MendingBehaviour.Update", static self => self.Step(), this);

        private void Step()
        {
            if (_character == null || _controller == null || !_controller.IsOwner() || _character.IsDead())
            {
                return;
            }
            _timer += Time.deltaTime;
            if (_timer < 1f)
            {
                return;
            }
            _timer -= 1f;
            float amount = _character.GetHealth() * AspectMath.Power(Aspect.Mending, Fields.Regen) / 100f;
            if (amount > 0f && _character.GetHealth() < _character.GetMaxHealth())
            {
                _character.Heal(amount, showText: false);
            }
        }
    }
}
