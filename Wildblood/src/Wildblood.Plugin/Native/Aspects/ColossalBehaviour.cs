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
    /// Colossal: bigger, with more health and slower (applied with the boss's other stats, see <see cref="AspectMath"/>),
    /// and its heavy attacks send out a shockwave that knocks players down. The game runs a boss's attacks on its owner,
    /// so that is where a landed blow is weighed (<see cref="HeavyAttack"/>, reached through
    /// <see cref="Patches.ColossalAttackPatch"/>); a heavy one sends a single message through the boss's own network view,
    /// the impact point and `shockwave radius`, so it reaches exactly the machines that hold the boss. Each of those draws
    /// the shockwave (<see cref="ColossalLook"/>) and judges its own player (<see cref="ColossalKnockdown"/>), because a
    /// player's body lives on that player's client. A flying boss sends none: the wave runs through the ground. Nor does
    /// a boss send one within a few seconds of its last, so a boss that swings heavily every few seconds (the Frozen King,
    /// the Queen at close range) paces its shockwaves rather than pinning players to the floor. A hand-over needs nothing:
    /// a blow lands only where it was started and only while that machine still owns the boss - a swing cut short by a
    /// hand-over hits nothing and shakes nothing - and the handler is registered everywhere, so the new owner's next
    /// heavy blow is heard like any other.
    /// </summary>
    public sealed class ColossalBehaviour : MonoBehaviour
    {
        /// <summary>The shockwave, to every machine holding the boss: the impact point and the radius.</summary>
        public const string Rpc = "ecr_colossal_shock";

        /// <summary>The widest shockwave a rule file may ask for, in metres; 0 turns the shockwave off.</summary>
        private const float MaxRadius = 30f;

        /// <summary>The fewest seconds between one boss's shockwaves: a knocked-down player gets up and gets a turn.</summary>
        private const float Gap = 5f;

        private Character _character = null!;
        private EliteController? _controller;
        private float _quietUntil;

        private void Start() => Guard.Run("ColossalBehaviour.Start", Setup);

        private void Setup()
        {
            _character = GetComponent<Character>();
            EliteController controller = GetComponent<EliteController>();
            if (_character == null || controller == null || controller.View == null || !controller.View.IsValid())
            {
                enabled = false;
                return;
            }
            _controller = controller;
            if (!NativeRpcOwnership.View(controller.View, NativeRpcOwner.CreatureColossal, () => controller.View.Register<Vector3, float>(Rpc, OnShockwave), Rpc))
            {
                enabled = false;
                return;
            }
        }

        /// <summary>Owner side, from the attack patch: a blow has just landed; a heavy one sends the wave.</summary>
        public void Landed(Attack attack)
        {
            if (_controller == null || !NativeRpcOwnership.OwnsView(_controller.View, NativeRpcOwner.CreatureColossal) || !_controller.IsOwner() || Time.time < _quietUntil || _character.IsFlying()
                || !HeavyAttack.IsHeavy(attack))
            {
                return;
            }
            float radius = Mathf.Clamp(AspectMath.Power(Aspect.Colossal, Fields.ShockwaveRadius), 0f, MaxRadius);
            if (radius > 0f)
            {
                _quietUntil = Time.time + Gap;
                Vector3 point = HeavyAttack.ImpactPoint(attack, _character);
                _controller.View.InvokeRPC(ZNetView.Everybody, Rpc, point, radius); // here at once, and to every peer
            }
        }

        private void OnShockwave(long sender, Vector3 point, float radius) =>
            Guard.Run("ColossalBehaviour.Shockwave", () =>
            {
                if (isActiveAndEnabled && _controller != null && _controller.Ready) Shockwave(point, radius);
            });

        private static void Shockwave(Vector3 point, float radius)
        {
            ColossalLook.Draw(point, radius);
            ColossalKnockdown.Judge(point, radius);
        }
    }
}
