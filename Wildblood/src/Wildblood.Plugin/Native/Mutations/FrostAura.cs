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

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Frostbound's freezing aura. A player within `aura radius` metres of a living Frostbound creature it counts as an
    /// enemy is "Chilled" (<see cref="ChillStatus"/>): stamina regenerates `stamina regen` percent slower while they stay,
    /// and the chill ends about a second after they leave. Each machine decides for its own player only - where the
    /// stamina is spent and regained - from what it already has: the creature's position, traits and rules, so the aura
    /// sends nothing over the network and changing owners changes nothing. A few checks a second, staggered across
    /// creatures, and none on a dedicated server, which has no player of its own. The look it wears on every machine is
    /// <see cref="FrostAuraLook"/>. Attached on every machine.
    /// </summary>
    public sealed class FrostAura : MonoBehaviour
    {
        /// <summary>Seconds between two checks of the local player's distance.</summary>
        private const float CheckEvery = 0.25f;

        private Character _character = null!;
        private EliteController _controller = null!;
        private float _wait;

        private void Start() => Guard.Run("FrostAura.Start", Setup);

        private void Setup()
        {
            Character character = GetComponent<Character>();
            EliteController controller = GetComponent<EliteController>();
            if (character == null || controller == null || IsHeadless())
            {
                enabled = false; // a dedicated server draws nothing and has no player to chill
                return;
            }
            _character = character;
            _controller = controller;
            _wait = Random.Range(0f, CheckEvery); // many auras in one place check on different frames
            controller.InstallBehaviour<FrostAuraLook>();
        }

        private static bool IsHeadless() => ZNet.instance != null && ZNet.instance.IsDedicated();

        private void Update()
        {
            if (_controller == null)
            {
                return;
            }
            _wait -= Time.deltaTime;
            if (_wait <= 0f)
            {
                _wait = CheckEvery;
                Guard.Run("FrostAura.Check", static self => self.Check(), this);
            }
        }

        // Radius and cut are its gains, so a large star enhances both; the cut never passes 100% (no regeneration).
        private void Check()
        {
            Player local = Player.m_localPlayer;
            if (!_controller.Ready || !_controller.Traits.Has(Mutation.Frostbound) || local == null || local.IsDead() || local.InGhostMode() || _character.IsDead())
            {
                return;
            }
            BiomeRules rules = _controller.Rules;
            float radius = Enhance.Magnitude(rules, _controller.Traits, Mutation.Frostbound, Fields.AuraRadius);
            if ((local.transform.position - transform.position).sqrMagnitude > radius * radius
                || !BaseAI.IsEnemy(_character, local))
            {
                return; // out of reach, or a tamed one beside its friends
            }
            float cut = Enhance.Magnitude(rules, _controller.Traits, Mutation.Frostbound, Fields.StaminaRegen);
            if (cut > 0f)
            {
                ChillStatus.Hold(local, Mathf.Min(cut, 100f));
            }
        }
    }
}
