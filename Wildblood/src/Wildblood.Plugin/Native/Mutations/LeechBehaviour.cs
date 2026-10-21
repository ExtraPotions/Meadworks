// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Leeching's passive half: regenerates a share of maximum health every second, provided it has taken no damage
    /// in the last `combat cooldown` seconds - a top-up while it disengages, not a way to out-heal a fight in
    /// progress. `regen cap` bounds how much HP/s a single tick can grant, so a huge-health creature cannot out-heal
    /// a player's DPS just by having more health. The regen rate is never large-star enhanced; only its lifesteal
    /// half - healing off damage dealt, applied in the damage patch where the amount dealt is known - is. Attached on
    /// every machine but heals only while it owns the creature (a heal is an owner-only write), so ownership
    /// changing hands moves the regen to the new owner with it.
    /// </summary>
    public sealed class LeechBehaviour : MonoBehaviour
    {
        private Character _character = null!;
        private EliteController _controller = null!;
        private float _regenPercent;
        private float _regenCap;
        private float _combatCooldown;
        private float _lastHealth;
        private float _cooldownRemaining;
        private float _timer;

        private void Start()
        {
            _character = GetComponent<Character>();
            _controller = GetComponent<EliteController>();
            _lastHealth = _character.GetHealth();
            if (_controller == null)
            {
                return;
            }
            BiomeRules rules = _controller.Rules;
            _regenPercent = rules.PowerOf(Mutation.Leeching, Fields.Regen); // never large-star enhanced
            _regenCap = rules.PowerOf(Mutation.Leeching, Fields.RegenCap);
            _combatCooldown = rules.PowerOf(Mutation.Leeching, Fields.CombatCooldown);
        }

        private void Update() => Guard.Run("LeechBehaviour.Update", static self => self.Step(), this);

        private void Step()
        {
            if (_character == null || _character.IsDead() || _controller == null || !_controller.IsOwner())
            {
                return; // owner-only: a Heal is a write and only sticks on the owner
            }
            TrackCombat();
            if (_cooldownRemaining > 0f)
            {
                return;
            }
            _timer += Time.deltaTime;
            if (_timer < 1f)
            {
                return;
            }
            _timer -= 1f;
            Regen();
        }

        /// <summary>Notices damage taken (health dropping since last frame) and restarts the combat cooldown.</summary>
        private void TrackCombat()
        {
            float health = _character.GetHealth();
            if (health < _lastHealth - 0.01f)
            {
                _cooldownRemaining = _combatCooldown;
            }
            _lastHealth = health;
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Time.deltaTime);
        }

        private void Regen()
        {
            float amount = Mathf.Min(_character.GetMaxHealth() * (_regenPercent / 100f), _regenCap);
            if (amount > 0f && _character.GetHealth() < _character.GetMaxHealth())
            {
                _character.Heal(amount, showText: false);
            }
        }
    }
}
